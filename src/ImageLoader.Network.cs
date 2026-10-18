using System;
using System.IO;
using System.Security.Cryptography;
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
                if(bytes.Length>=32&&BitConverter.ToUInt32(bytes,0)==0xed0a6749&&Container.Imports(bytes).Length!=0)throw new IOException("Remote IGGY static imports are not supported yet");
                string hash;using(SHA256 sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(transfer.ResponseUrl??transfer.Url))).Replace("-","");
                string file=Path.Combine(directory,"__iggy_remote_"+hash+".iggy");
                lock(sync){
                    if(closed||transfer.Cancelled)return;
                    long pending=bytes.Length;foreach(Job other in jobs.Values)if(other!=job)pending+=other.InputBytes;
                    if(pending>256L*1024*1024)throw new IOException("Pending Loader byte storage exceeds 256 MiB");
                    job.SourceBytes=bytes;job.InputBytes=bytes.Length;job.Url=new Uri(file).AbsoluteUri;job.SourceUrl=transfer.ResponseUrl??transfer.Url;
                }
                Decode(job);
            } catch(Exception error){lock(sync)if(!closed&&!job.Cancelled)job.Status="error|"+error.Message.Replace('|',' ');}
            finally{lock(sync){transfer.Bytes=null;transfer.Reserved=0;}}
        }
        void CancelDisplay(Job job) {
            job.Cancelled=true;job.Pixels=null;job.MovieBytes=null;job.SourceBytes=null;jobs.Remove(job.Token);
            if(job.Transfer!=null)CancelData(job.Transfer);
        }
    }
}
