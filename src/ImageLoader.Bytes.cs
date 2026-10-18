using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace IggyViewer {
    internal unsafe sealed partial class ImageLoader {
        sealed class ByteRequest {internal IntPtr Request;internal string Token;}
        readonly List<ByteRequest> byteRequests=new List<ByteRequest>();
        void StartBytes(IntPtr info,IntPtr arguments) {
            string token;
            try {
                lock(sync) {
                    if(closed)throw new IOException("Movie closed");
                    if(jobs.Count>=32)throw new IOException("Too many pending image loads");
                    if(native.Activity.Current!=null&&native.Activity.Current.Stopped)throw new IOException("The containing movie was unloaded");
                    int domain;if(!int.TryParse(ReadText(Marshal.ReadIntPtr(arguments,32)),out domain)||!native.ValidMovieDomain(domain))throw new IOException("Invalid Loader application domain");
                    long pending=0;foreach(Job existing in jobs.Values)pending+=existing.InputBytes;
                    byte[] bytes=native.CopyByteArray(Marshal.ReadIntPtr(arguments,16),(int)Math.Max(0,256L*1024*1024-pending));
                    if(bytes.Length==0)throw new IOException("Loader bytes are empty");
                    string baseUrl=ReadText(Marshal.ReadIntPtr(arguments,24));Uri uri;
                    string baseFile=Uri.TryCreate(baseUrl,UriKind.Absolute,out uri)&&uri.IsFile?uri.LocalPath:native.FileName;
                    string digest;using(SHA256 sha=SHA256.Create())digest=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");
                    string file=Path.Combine(Path.GetDirectoryName(baseFile),"__iggy_memory_"+digest+".iggy");
                    Job job=new Job {Token=Guid.NewGuid().ToString("N"),Url=new Uri(file).AbsoluteUri,BaseFile=baseFile,Domain=domain,
                        AllowCode=ReadText(Marshal.ReadIntPtr(arguments,40))!="no",Owner=native.Activity.Current,SourceBytes=bytes,InputBytes=bytes.Length};
                    if(uri!=null&&(uri.Scheme=="http"||uri.Scheme=="https"))job.SourceUrl=new Uri(uri,"__iggy_memory_"+digest+".iggy").AbsoluteUri;
                    jobs.Add(job.Token,job);token=job.Token;
                    ThreadPool.QueueUserWorkItem(delegate {decoders.Wait();try {Decode(job);}finally {decoders.Release();}});
                }
            } catch(Exception ex) {token="error|"+ex.Message.Replace('|',' ');}
            byteRequests.Add(new ByteRequest {Request=reference(native.Player,info,1),Token=token});
        }
        void PruneByteRequests() {
            for(int i=byteRequests.Count-1;i>=0;i--)if(Marshal.ReadIntPtr(byteRequests[i].Request).ToInt64()==7) {
                freeReference(native.Player,byteRequests[i].Request);byteRequests.RemoveAt(i);
            }
        }
    }
}
