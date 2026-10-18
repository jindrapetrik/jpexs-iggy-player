using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace IggyViewer {
    internal unsafe sealed partial class ImageLoader {
        static string MovieUrl(Native.LoadedMovie movie) {return movie.SourceUrl??new Uri(movie.FileName).AbsoluteUri;}
        string StartDisplayRequest(DataJob transfer) {
            lock(sync) {
                if(closed||native.Activity.Current!=null&&native.Activity.Current.Stopped)return "error|The containing movie was closed";
                if(jobs.Count>=32)return "error|Too many pending image loads";
                if(!native.ValidMovieDomain(transfer.Domain))return "error|Invalid Loader application domain";
                Uri uri;if(!Uri.TryCreate(transfer.Url,UriKind.Absolute,out uri))uri=new Uri(new Uri(transfer.BaseUrl),transfer.Url);
                Job job=new Job {Token=Guid.NewGuid().ToString("N"),Url=transfer.Url,BaseFile=transfer.BaseUrl,Domain=transfer.Domain,AllowCode=transfer.AllowCode,Owner=native.Activity.Current};
                if(uri.Scheme=="http"||uri.Scheme=="https") {
                    if(dataJobs.Count>=32)return "error|Too many pending URLLoader requests";
                    long reserved=transfer.Upload.Length;foreach(DataJob other in dataJobs.Values)reserved+=other.Reserved+(other.Upload==null?0:other.Upload.Length);
                    if(reserved>128L*1024*1024)return "error|Pending URLLoader data exceeds 128 MiB";
                    transfer.Token=job.Token;transfer.Owner=job.Owner;transfer.Display=job;job.Transfer=transfer;
                    jobs.Add(job.Token,job);dataJobs.Add(job.Token,transfer);dataWaiting.Enqueue(transfer);StartDataReaders();
                } else {
                    if(transfer.Method!="GET"||transfer.Headers.Count!=0)return "error|HTTP method and headers cannot be used with a local file";
                    jobs.Add(job.Token,job);ThreadPool.QueueUserWorkItem(delegate {decoders.Wait();try{Decode(job);}finally{decoders.Release();}});
                }
                return job.Token;
            }
        }
        void CompleteDisplayRequest(DataJob transfer) {
            Job job=transfer.Display;
            try {
                byte[] bytes;
                lock(sync){if(closed||transfer.Cancelled||job.Cancelled)return;if(transfer.Error!=null){job.Status=transfer.Error;return;}bytes=transfer.Bytes;}
                if(bytes==null)throw new IOException("Display download did not finish");
                string sourceUrl=transfer.ResponseUrl??transfer.Url,file=MovieResources.FileName(directory,sourceUrl);
                lock(sync){
                    if(closed||transfer.Cancelled)return;
                    TrackDisplayInput(job,bytes.Length);
                    job.SourceBytes=bytes;job.Url=new Uri(file).AbsoluteUri;job.SourceUrl=sourceUrl;
                }
                Decode(job);
            } catch(Exception error){FailDisplay(job,error);}
            finally{lock(sync){transfer.Bytes=null;transfer.Reserved=0;}}
        }
        sealed class DependencyError:IOException {
            internal readonly bool Security;
            internal DependencyError(string error,string url):base("Iggy dependency "+url+": "+error.Substring(error.IndexOf('|')+1)){Security=error.StartsWith("security|",StringComparison.Ordinal);}
        }
        void FailDisplay(Job job,Exception error) {
            DependencyError dependency=error as DependencyError;
            lock(sync)if(!closed&&!job.Cancelled){job.Resources=null;job.InputBytes=0;job.Status=(dependency!=null&&dependency.Security?"security|":"error|")+error.Message.Replace('|',' ');}
        }
        void CheckDisplay(Job job) {lock(sync)if(closed||job.Cancelled)throw new OperationCanceledException("Display load was cancelled");}
        // Called under sync. Includes the root and all retained dependency buffers.
        void TrackDisplayInput(Job job,int count) {
            long pending=job.InputBytes+(long)count;foreach(Job other in jobs.Values)if(other!=job)pending+=other.InputBytes;
            if(pending>256L*1024*1024)throw new IOException("Pending Loader byte storage exceeds 256 MiB");
            job.InputBytes=checked(job.InputBytes+count);
        }
        static Uri NetworkUri(Uri parent,string url) {
            Uri uri=new Uri(parent,url.Replace('\\','/'));
            if((uri.Scheme!="http"&&uri.Scheme!="https")||uri.UserInfo.Length!=0)throw new DependencyError("security|Dependencies require HTTP/HTTPS URLs without credentials",uri.AbsoluteUri);
            UriBuilder builder=new UriBuilder(uri);builder.Fragment="";return builder.Uri;
        }
        static Uri ResourceExtension(Uri uri,string extension) {
            UriBuilder builder=new UriBuilder(uri);builder.Path=Path.ChangeExtension(uri.AbsolutePath,extension);builder.Fragment="";return builder.Uri;
        }
        DataJob DownloadDependency(Job owner,Uri url,bool optional,Dictionary<string,MovieResources.Movie> cache=null) {
            CheckDisplay(owner);
            DataJob request=new DataJob {Token=Guid.NewGuid().ToString("N"),Url=url.AbsoluteUri,Method="GET",ContentType="",Upload=new byte[0],Owner=owner.Owner,CachedMovies=cache};
            lock(sync) {
                CheckDisplay(owner);
                if(dataJobs.Count>=32)throw new IOException("Too many pending network requests for Iggy dependencies");
                dataJobs.Add(request.Token,request);owner.Dependencies.Add(request);
            }
            try {
                // Use this worker directly: queued dependencies must never wait
                // for one of the four workers occupied by their parent movies.
                ReadData(request);CheckDisplay(owner);
                if(request.Error!=null) {
                    if(optional&&request.ResponseCode==404)return null;
                    throw new DependencyError(request.Error,url.AbsoluteUri);
                }
                if(request.Bytes==null)throw new IOException("Iggy dependency did not finish: "+url);
                lock(sync) {
                    if(!request.Cached)TrackDisplayInput(owner,request.Bytes.Length);
                    if(owner.Transfer!=null)owner.Transfer.Reserved=checked(owner.Transfer.Reserved+request.Reserved);
                    request.Reserved=0;
                }
                return request;
            } finally {lock(sync){dataJobs.Remove(request.Token);owner.Dependencies.Remove(request);}}
        }
        MovieResources DownloadMovieResources(Job job,byte[] bytes) {
            if(job.Transfer==null)lock(sync) {
                CheckDisplay(job);
                long reserved=bytes.Length;foreach(DataJob other in dataJobs.Values)reserved+=other.Reserved+(other.Upload==null?0:other.Upload.Length);
                if(dataJobs.Count>=32||reserved>128L*1024*1024)throw new IOException("Pending network dependency storage exceeds its limit");
                job.Transfer=new DataJob {Token=Guid.NewGuid().ToString("N"),Url=job.SourceUrl,Reserved=bytes.Length,Loaded=bytes.Length,Total=bytes.Length,Upload=new byte[0]};
                dataJobs.Add(job.Transfer.Token,job.Transfer);
            }
            MovieResources resources=new MovieResources();
            Dictionary<string,MovieResources.Movie> urls=new Dictionary<string,MovieResources.Movie>(StringComparer.Ordinal);
            HashSet<string> visiting=new HashSet<string>(StringComparer.Ordinal);
            DownloadMovie(job,resources,urls,visiting,job.SourceUrl,bytes);
            return resources;
        }
        MovieResources.Movie DownloadMovie(Job job,MovieResources resources,Dictionary<string,MovieResources.Movie> urls,HashSet<string> visiting,string url,byte[] bytes) {
            CheckDisplay(job);
            if(visiting.Contains(url))throw new IOException("Cyclic Iggy imports: "+url);
            MovieResources.Movie movie;if(urls.TryGetValue(url,out movie))return movie;
            if(resources.Movies.Count>=32)throw new IOException("Remote Iggy exceeds the native runtime's 32-library limit");
            int textures=Container.TextureCount(bytes);
            movie=new MovieResources.Movie {FileName=MovieResources.FileName(directory,url),Url=url,Bytes=bytes};
            resources.Movies.Add(movie.FileName,movie);urls.Add(url,movie);visiting.Add(url);
            Uri origin=NetworkUri(new Uri(url),url);
            urls[origin.AbsoluteUri]=movie;
            foreach(string import in Container.Imports(bytes)) {
                Uri requested=NetworkUri(origin,import),compiled=ResourceExtension(requested,".iggy");MovieResources.Movie child;
                if(!urls.TryGetValue(compiled.AbsoluteUri,out child)&&!urls.TryGetValue(requested.AbsoluteUri,out child)) {
                    DataJob downloaded=DownloadDependency(job,compiled,true,urls);
                    if(downloaded==null&&compiled!=requested)downloaded=DownloadDependency(job,requested,true,urls);
                    if(downloaded==null)throw new IOException("Missing remote Iggy import: "+requested);
                    child=DownloadMovie(job,resources,urls,visiting,downloaded.ResponseUrl,downloaded.Bytes);
                    urls[compiled.AbsoluteUri]=child;urls[requested.AbsoluteUri]=child;
                }
                if(visiting.Contains(child.Url))throw new IOException("Cyclic Iggy imports: "+child.Url);
                movie.Imports[import]=child.FileName;
            }
            if(textures>0) {
                DataJob downloaded=DownloadDependency(job,ResourceExtension(origin,".raw.iggytex"),true)??DownloadDependency(job,ResourceExtension(origin,".iggytex"),true);
                if(downloaded==null)throw new IOException("Missing remote Iggy companion textures: "+url);
                movie.Textures=downloaded.Bytes;
            }
            visiting.Remove(url);MovieResources.Seal(movie,resources);return movie;
        }
        void CancelDisplay(Job job) {
            job.Cancelled=true;job.Pixels=null;job.MovieBytes=null;job.SourceBytes=null;job.Resources=null;job.InputBytes=0;jobs.Remove(job.Token);
            foreach(DataJob dependency in job.Dependencies)CancelData(dependency);
            if(job.Transfer!=null)CancelData(job.Transfer);
        }
    }
}
