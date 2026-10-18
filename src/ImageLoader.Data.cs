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
            internal string Token,Url,BaseUrl,Error;
            internal MovieActivity.Scope Owner;
            internal int Loaded,Total,Reserved;
            internal bool Ready,Cancelled;
            internal byte[] Bytes;
            internal HttpWebRequest Request;
            internal readonly Queue<string> Statuses=new Queue<string>();
        }
        readonly Dictionary<string,DataJob> dataJobs=new Dictionary<string,DataJob>();
        readonly Queue<DataJob> dataWaiting=new Queue<DataJob>();
        int dataReaders;
        internal int PendingDataLoads {get{lock(sync)return dataJobs.Count;}}
        string DataBaseUrl() {
            if(constructing!=null)return new Uri(constructing.Movie.FileName).AbsoluteUri;
            if(native.Activity.Current!=null)foreach(MovieRoot root in roots) {
                MovieActivity.Scope scope;
                if(!native.Activity.TryOwner(Marshal.ReadIntPtr(root.Root),out scope)||scope==null||!MovieActivity.Within(native.Activity.Current,scope))continue;
                foreach(MovieTicket ticket in tickets)if(Marshal.ReadIntPtr(ticket.Info)==Marshal.ReadIntPtr(root.Info))return new Uri(ticket.Movie.FileName).AbsoluteUri;
            }
            return new Uri(native.FileName).AbsoluteUri;
        }
        IntPtr DataContent(IntPtr context,AmfRequest request) {
            string operation=request.Operation,answer;
            if(operation=="data-base")return native.ReturnMovieString(context,DataBaseUrl());
            string value=operation=="data-bytes"?request.Extra:native.AmfString(Marshal.ReadIntPtr(request.Value),65536);
            if(operation=="data-start") {
                lock(sync) {
                    if(closed||native.Activity.Current!=null&&native.Activity.Current.Stopped)answer="error|The containing movie was closed";
                    else if(dataJobs.Count>=32)answer="error|Too many pending URLLoader requests";
                    else {
                        DataJob job=new DataJob {Token=Guid.NewGuid().ToString("N"),Url=value,BaseUrl=request.Extra,Owner=native.Activity.Current};dataJobs.Add(job.Token,job);answer=job.Token;
                        dataWaiting.Enqueue(job);StartDataReaders();
                    }
                }
                return native.ReturnMovieString(context,answer);
            }
            byte[] bytes=null;
            lock(sync) {
                DataJob job;
                if(operation=="data-cancel") {if(dataJobs.TryGetValue(value,out job))CancelData(job);answer="ok";}
                else if(!dataJobs.TryGetValue(value,out job))answer="error|URLLoader request was cancelled";
                else if(operation=="data-status")answer=job.Statuses.Count==0?"":job.Statuses.Dequeue();
                else if(operation=="data-poll")answer=job.Error??((job.Ready?"ready":"pending")+"|"+job.Loaded+"|"+job.Total);
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
                    try{ReadData(job);}finally{lock(sync){dataReaders--;StartDataReaders();}}
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
                if(length>DataLimit)throw new IOException("URLLoader data exceeds 32 MiB");
                long reserved=length;foreach(DataJob other in dataJobs.Values)if(other!=job)reserved+=other.Reserved;
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
            bool received=false,redirected=false;
            try {
                for(int hop=0;;hop++) {
                    received=false;redirected=hop!=0;
                    if((uri.Scheme!="http"&&uri.Scheme!="https")||uri.UserInfo.Length!=0){FailData(job,"security|Unsupported HTTP redirect or URL credentials");return;}
                    HttpWebRequest request=(HttpWebRequest)WebRequest.Create(uri);
                    request.Method="GET";request.AllowAutoRedirect=false;request.Timeout=15000;request.ReadWriteTimeout=15000;
                    request.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
                    request.ServicePoint.ConnectionLimit=8;
                    lock(sync){if(closed||job.Cancelled)return;job.Request=request;}
                    HttpWebResponse response;
                    try {response=(HttpWebResponse)request.GetResponse();}
                    catch(WebException error){response=error.Response as HttpWebResponse;if(response==null)throw;}
                    using(response) {
                        int code=(int)response.StatusCode;
                        lock(sync){if(closed||job.Cancelled)return;job.Statuses.Enqueue(code+"|"+(hop==0?"0":"1"));}
                        received=true;
                        if(code==301||code==302||code==303||code==307||code==308) {
                            if(hop>=8)throw new IOException("URLLoader exceeded eight HTTP redirects");
                            string location=response.Headers["Location"];
                            if(string.IsNullOrEmpty(location))throw new IOException("HTTP redirect has no Location header");
                            uri=new Uri(uri,location);continue;
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
            } finally {lock(sync)job.Request=null;}
        }
        void FailData(DataJob job,string error) {lock(sync)if(!closed&&!job.Cancelled){job.Bytes=null;job.Reserved=0;job.Error=error;}}
        void CancelData(DataJob job) {
            job.Cancelled=true;job.Bytes=null;job.Reserved=0;job.Statuses.Clear();dataJobs.Remove(job.Token);
            for(int remaining=dataWaiting.Count;remaining>0;remaining--){DataJob waiting=dataWaiting.Dequeue();if(!waiting.Cancelled)dataWaiting.Enqueue(waiting);}
            HttpWebRequest request=job.Request;
            // Abort outside the VM's lock and thread, including requests waiting
            // for headers or a stalled response body.
            if(request!=null)ThreadPool.QueueUserWorkItem(delegate {request.Abort();});
        }
    }
}
