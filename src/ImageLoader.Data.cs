using System;
using System.Collections.Generic;
using System.IO;
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
        }
        readonly Dictionary<string,DataJob> dataJobs=new Dictionary<string,DataJob>();
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
                        ThreadPool.QueueUserWorkItem(delegate {decoders.Wait();try {ReadData(job);}finally {decoders.Release();}});
                    }
                }
                return native.ReturnMovieString(context,answer);
            }
            byte[] bytes=null;
            lock(sync) {
                DataJob job;
                if(operation=="data-cancel") {if(dataJobs.TryGetValue(value,out job))CancelData(job);answer="ok";}
                else if(!dataJobs.TryGetValue(value,out job))answer="error|URLLoader request was cancelled";
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
        void ReadData(DataJob job) {
            try {
                int separator=job.Url.IndexOfAny(new char[]{'?','#'});string location=separator<0?job.Url:job.Url.Substring(0,separator);Uri uri;
                if(!Uri.TryCreate(location,UriKind.Absolute,out uri))uri=new Uri(new Uri(job.BaseUrl),location);
                if(!uri.IsFile||uri.IsUnc){lock(sync)if(!closed&&!job.Cancelled)job.Error="security|Only local files are supported by URLLoader";return;}
                using(FileStream input=new FileStream(uri.LocalPath,FileMode.Open,FileAccess.Read,FileShare.Read)) {
                    if(input.Length>DataLimit)throw new IOException("URLLoader file exceeds 32 MiB");
                    int length=(int)input.Length;
                    lock(sync) {
                        if(closed||job.Cancelled)return;
                        long reserved=0;foreach(DataJob other in dataJobs.Values)reserved+=other.Reserved;
                        if(reserved+length>128L*1024*1024)throw new IOException("Pending URLLoader data exceeds 128 MiB");
                        job.Total=length;job.Reserved=length;
                    }
                    byte[] bytes=new byte[length];int offset=0;
                    while(offset<length) {
                        lock(sync)if(closed||job.Cancelled)return;
                        int read=input.Read(bytes,offset,Math.Min(65536,length-offset));if(read==0)throw new EndOfStreamException("Unexpected end of URLLoader file");offset+=read;
                        lock(sync)job.Loaded=offset;
                    }
                    lock(sync)if(!closed&&!job.Cancelled){job.Bytes=bytes;job.Ready=true;}
                }
            } catch(Exception ex) {lock(sync)if(!closed&&!job.Cancelled){job.Reserved=0;job.Error="error|"+ex.Message.Replace('|',' ');}}
        }
        void CancelData(DataJob job) {job.Cancelled=true;job.Bytes=null;job.Reserved=0;dataJobs.Remove(job.Token);}
    }
}
