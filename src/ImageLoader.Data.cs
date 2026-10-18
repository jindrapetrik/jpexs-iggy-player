using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace IggyViewer {
    internal unsafe sealed partial class ImageLoader {
        const int DataLimit=32*1024*1024;
        sealed class DataJob {
            internal string Token,Url,BaseUrl,Error,Method,ContentType,ResponseUrl;
            internal Job Display;
            internal Dictionary<string,MovieResources.Movie> CachedMovies;
            internal int Domain;
            internal bool AllowCode;
            internal bool Sound,SoundReady;
            internal byte[] Pcm;
            internal int Rate,Channels,DecodedReservation;
            internal MovieActivity.Scope Owner;
            internal int Loaded,Total,Reserved,ResponseCode;
            internal bool Ready,Cancelled,Cached;
            internal byte[] Bytes,Upload;
            internal readonly List<KeyValuePair<string,string>> Headers=new List<KeyValuePair<string,string>>();
            internal HttpWebRequest Request;
            internal readonly Queue<string> Statuses=new Queue<string>();
        }
        readonly Dictionary<string,DataJob> dataJobs=new Dictionary<string,DataJob>();
        readonly Queue<DataJob> dataWaiting=new Queue<DataJob>();
        int dataReaders;
        internal int PendingDataLoads {get{lock(sync)return dataJobs.Count;}}
        string DataBaseUrl() {
            if(constructing!=null)return MovieUrl(constructing.Movie);
            if(native.Activity.Current!=null)foreach(MovieRoot root in roots) {
                MovieActivity.Scope scope;
                if(!native.Activity.TryOwner(Marshal.ReadIntPtr(root.Root),out scope)||scope==null||!MovieActivity.Within(native.Activity.Current,scope))continue;
                foreach(MovieTicket ticket in tickets)if(Marshal.ReadIntPtr(ticket.Info)==Marshal.ReadIntPtr(root.Info))return MovieUrl(ticket.Movie);
            }
            return new Uri(native.FileName).AbsoluteUri;
        }
        IntPtr DataContent(IntPtr context,AmfRequest request) {
            string operation=request.Operation,answer;
            if(operation=="data-base")return native.ReturnMovieString(context,DataBaseUrl());
            if(operation.StartsWith("data-sound-",StringComparison.Ordinal)&&operation!="data-sound-start")return SoundContent(context,request);
            if(operation=="data-start"||operation=="data-image-start"||operation=="data-sound-start") {
                DataJob pending=ReadDataRequest(Marshal.ReadIntPtr(request.Value),operation=="data-image-start");
                pending.BaseUrl=request.Extra;
                pending.Sound=operation=="data-sound-start";
                if(operation=="data-image-start")return native.ReturnMovieString(context,StartDisplayRequest(pending));
                lock(sync) {
                    if(closed||native.Activity.Current!=null&&native.Activity.Current.Stopped)answer="error|The containing movie was closed";
                    else if(dataJobs.Count>=32)answer="error|Too many pending URLLoader requests";
                    else {
                        DataJob job=pending;job.Token=Guid.NewGuid().ToString("N");job.BaseUrl=request.Extra;job.Owner=native.Activity.Current;
                        long reserved=job.Upload.Length;foreach(DataJob other in dataJobs.Values)reserved+=other.Reserved+(other.Upload==null?0:other.Upload.Length);
                        if(reserved>128L*1024*1024)throw new InvalidDataException("Pending URLLoader data exceeds 128 MiB");
                        dataJobs.Add(job.Token,job);answer=job.Token;
                        dataWaiting.Enqueue(job);StartDataReaders();
                    }
                }
                return native.ReturnMovieString(context,answer);
            }
            string value=operation=="data-bytes"?request.Extra:native.AmfString(Marshal.ReadIntPtr(request.Value),65536);
            byte[] bytes=null;
            lock(sync) {
                DataJob job;
                if(operation=="data-cancel") {if(dataJobs.TryGetValue(value,out job))CancelData(job);answer="ok";}
                else if(!dataJobs.TryGetValue(value,out job))answer="error|URLLoader request was cancelled";
                else if(operation=="data-status")answer=job.Statuses.Count==0?"":job.Statuses.Dequeue();
                else if(operation=="data-poll")answer=job.Error??((job.Ready&&(!job.Sound||job.SoundReady)?"ready":"pending")+"|"+job.Loaded+"|"+job.Total);
                else if((operation=="data-bytes"||operation=="data-text")&&job.Ready){bytes=job.Bytes;answer=null;}
                else throw new InvalidDataException("URLLoader data is not ready.");
            }
            if(bytes!=null) {
                if(operation=="data-bytes") {native.SetUrlLoaderBytes(Marshal.ReadIntPtr(request.Value),bytes);Marshal.WriteIntPtr(Marshal.ReadIntPtr(context,8),native.AmfBoolean(true));return IntPtr.Zero;}
                int skip=0;Encoding encoding=Encoding.UTF8;
                if(bytes.Length>=2&&bytes[0]==255&&bytes[1]==254){encoding=Encoding.Unicode;skip=2;}
                else if(bytes.Length>=2&&bytes[0]==254&&bytes[1]==255){encoding=Encoding.BigEndianUnicode;skip=2;}
                else if(bytes.Length>=3&&bytes[0]==239&&bytes[1]==187&&bytes[2]==191)skip=3;
                return native.ReturnMovieString(context,encoding.GetString(bytes,skip,bytes.Length-skip));
            }
            return native.ReturnMovieString(context,answer);
        }
        // Called under sync. Waiting requests must not occupy ThreadPool threads:
        // HttpWebRequest needs that same pool to complete connection callbacks.
        void StartDataReaders() {
            while(dataReaders<4&&dataWaiting.Count!=0) {
                DataJob job=dataWaiting.Dequeue();if(closed||job.Cancelled)continue;
                dataReaders++;
                ThreadPool.QueueUserWorkItem(delegate {
                    try{ReadData(job);if(job.Sound)DecodeSound(job);if(job.Display!=null)CompleteDisplayRequest(job);}finally{lock(sync){dataReaders--;StartDataReaders();}}
                });
            }
        }
        void ReadData(DataJob job) {
            try {
                lock(sync)if(closed||job.Cancelled)return;
                Uri uri;
                if(!Uri.TryCreate(job.Url,UriKind.Absolute,out uri))uri=new Uri(new Uri(job.BaseUrl),job.Url);
                if(uri.Scheme=="http"||uri.Scheme=="https"){ReadHttpData(job,uri);return;}
                if(!uri.IsFile||uri.IsUnc){FailData(job,"security|URLLoader supports local files and HTTP/HTTPS URLs");return;}
                if(job.Method!="GET"||job.Headers.Count!=0){FailData(job,"error|HTTP method and headers cannot be used with a local file");return;}
                int separator=job.Url.IndexOfAny(new char[]{'?','#'});
                if(separator>=0){string location=job.Url.Substring(0,separator);if(!Uri.TryCreate(location,UriKind.Absolute,out uri))uri=new Uri(new Uri(job.BaseUrl),location);}
                using(FileStream input=new FileStream(uri.LocalPath,FileMode.Open,FileAccess.Read,FileShare.Read)) {
                    ReadDataStream(job,input,input.Length);
                }
            } catch(Exception ex) {FailData(job,"error|"+ex.Message.Replace('|',' '));}
        }
        bool ReserveData(DataJob job,int length) {
            lock(sync) {
                if(closed||job.Cancelled)return false;
                if(length>(job.Sound?96*1024*1024:DataLimit))throw new IOException("URLLoader data exceeds its storage limit");
                long reserved=length+(job.Upload==null?0:job.Upload.Length);foreach(DataJob other in dataJobs.Values)if(other!=job)reserved+=other.Reserved+(other.Upload==null?0:other.Upload.Length);
                if(reserved>128L*1024*1024)throw new IOException("Pending URLLoader data exceeds 128 MiB");
                job.Reserved=length;return true;
            }
        }
        void ReadDataStream(DataJob job,Stream input,long length) {
            if(length>DataLimit)throw new IOException("URLLoader data exceeds 32 MiB");
            if(!ReserveData(job,length<0?0:(int)length))return;
            lock(sync)job.Total=length<0?0:(int)length;
            using(MemoryStream output=new MemoryStream(length<0?0:(int)length)) {
                byte[] buffer=new byte[65536];int offset=0;
                for(;;) {
                    lock(sync)if(closed||job.Cancelled)return;
                    int read=input.Read(buffer,0,buffer.Length);if(read==0)break;
                    offset=checked(offset+read);
                    if(!ReserveData(job,length<0?offset:Math.Max((int)length,offset)))return;
                    output.Write(buffer,0,read);lock(sync)job.Loaded=offset;
                }
                if(length>=0&&offset!=length)throw new EndOfStreamException("Unexpected end of URLLoader data");
                lock(sync)if(!closed&&!job.Cancelled){job.Bytes=output.ToArray();job.Total=offset;job.Ready=true;}
            }
        }
        void ReadHttpData(DataJob job,Uri uri) {
            // The executable targets .NET 4; opt in to TLS 1.2 on newer Windows
            // without replacing the platform's certificate validation.
            ServicePointManager.SecurityProtocol|=(SecurityProtocolType)3072;
            bool received=false,redirected=false,forwardHeaders=true;
            string method=job.Method,contentType=job.ContentType;byte[] upload=job.Upload;
            foreach(KeyValuePair<string,string> header in job.Headers)if(string.Equals(header.Key,"Content-Type",StringComparison.OrdinalIgnoreCase))contentType=header.Value;
            try {
                for(int hop=0;;hop++) {
                    received=false;redirected=hop!=0;
                    if((uri.Scheme!="http"&&uri.Scheme!="https")||uri.UserInfo.Length!=0){FailData(job,"security|Unsupported HTTP redirect or URL credentials");return;}
                    MovieResources.Movie cached;
                    if(job.CachedMovies!=null&&job.CachedMovies.TryGetValue(uri.AbsoluteUri,out cached)) {
                        lock(sync)if(!closed&&!job.Cancelled){job.Bytes=cached.Bytes;job.ResponseUrl=uri.AbsoluteUri;job.Loaded=job.Total=cached.Bytes.Length;job.Ready=true;job.Cached=true;}
                        return;
                    }
                    HttpWebRequest request=(HttpWebRequest)WebRequest.Create(uri);
                    request.Method=method;request.AllowAutoRedirect=false;request.Timeout=15000;request.ReadWriteTimeout=15000;
                    request.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
                    request.ServicePoint.ConnectionLimit=8;
                    request.ServicePoint.Expect100Continue=false;
                    if(forwardHeaders)foreach(KeyValuePair<string,string> header in job.Headers) {
                        if(string.Equals(header.Key,"Content-Type",StringComparison.OrdinalIgnoreCase))continue;
                        if(string.Equals(header.Key,"Accept",StringComparison.OrdinalIgnoreCase))request.Accept=string.IsNullOrEmpty(request.Accept)?header.Value:request.Accept+", "+header.Value;
                        else request.Headers.Add(header.Key,header.Value);
                    }
                    if(method=="POST"){request.ContentLength=upload==null?0:upload.Length;request.AllowWriteStreamBuffering=false;if(contentType.Length!=0)request.ContentType=contentType;}
                    lock(sync){if(closed||job.Cancelled)return;job.Request=request;}
                    if(method=="POST")using(Stream output=request.GetRequestStream()) {
                        for(int offset=0;upload!=null&&offset<upload.Length;) {
                            lock(sync)if(closed||job.Cancelled)return;
                            int length=Math.Min(65536,upload.Length-offset);output.Write(upload,offset,length);offset+=length;
                        }
                    }
                    HttpWebResponse response;
                    try {response=(HttpWebResponse)request.GetResponse();}
                    catch(WebException error){response=error.Response as HttpWebResponse;if(response==null)throw;}
                    using(response) {
                        int code=(int)response.StatusCode;
                        lock(sync){if(closed||job.Cancelled)return;job.ResponseCode=code;job.ResponseUrl=uri.AbsoluteUri;job.Statuses.Enqueue(code+"|"+(hop==0?"0":"1"));}
                        received=true;
                        if(code==301||code==302||code==303||code==307||code==308) {
                            if(hop>=8)throw new IOException("URLLoader exceeded eight HTTP redirects");
                            string location=response.Headers["Location"];
                            if(string.IsNullOrEmpty(location))throw new IOException("HTTP redirect has no Location header");
                            Uri next=new Uri(uri,location);if(!SameDataOrigin(uri,next))forwardHeaders=false;
                            if(code==303||((code==301||code==302)&&method=="POST")){method="GET";upload=null;lock(sync)job.Upload=null;}
                            uri=next;continue;
                        }
                        if(code<200||code>=300)throw new IOException("HTTP "+code+" "+response.StatusDescription);
                        using(Stream input=response.GetResponseStream())ReadDataStream(job,input,response.ContentLength);
                        return;
                    }
                }
            } catch(Exception ex) {
                WebException web=ex as WebException;
                lock(sync)if(!closed&&!job.Cancelled&&!received)job.Statuses.Enqueue(redirected?"0|1":"0|0");
                FailData(job,(web!=null&&(web.Status==WebExceptionStatus.TrustFailure||web.Status==WebExceptionStatus.SecureChannelFailure)?"security|":"error|")+ex.Message.Replace('|',' '));
            } finally {lock(sync){job.Request=null;job.Upload=null;}}
        }
        void FailData(DataJob job,string error) {lock(sync)if(!closed&&!job.Cancelled){job.Bytes=null;job.Pcm=null;job.Upload=null;job.Reserved=job.DecodedReservation=0;job.Error=error;}}
        void CancelData(DataJob job) {
            job.Cancelled=true;job.Bytes=null;job.Pcm=null;job.Upload=null;job.Reserved=job.DecodedReservation=0;job.Statuses.Clear();dataJobs.Remove(job.Token);
            for(int remaining=dataWaiting.Count;remaining>0;remaining--){DataJob waiting=dataWaiting.Dequeue();if(!waiting.Cancelled)dataWaiting.Enqueue(waiting);}
            HttpWebRequest request=job.Request;
            // Abort outside the VM's lock and thread, including requests waiting
            // for headers or a stalled response body.
            if(request!=null)ThreadPool.QueueUserWorkItem(delegate {request.Abort();});
        }
    }
}
