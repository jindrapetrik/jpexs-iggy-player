using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace IggyViewer {
    internal static partial class SelfTest {
        // The test server binds only an ephemeral loopback port. Its private key
        // is a public fixture, never a credential for the player or any service.
        sealed class HttpTestServer : IDisposable {
            readonly TcpListener listener=new TcpListener(IPAddress.Loopback,0);
            readonly List<TcpClient> clients=new List<TcpClient>();
            readonly Thread accept;
            readonly X509Certificate2 certificate;
            bool stopped;
            internal int Hanging,Aborted,Requests;
            internal string CrossOrigin;
            internal readonly ManualResetEvent UploadDrain=new ManualResetEvent(false);
            internal readonly Dictionary<string,byte[]> Resources=new Dictionary<string,byte[]>(StringComparer.Ordinal);
            internal readonly Dictionary<string,string> Redirects=new Dictionary<string,string>(StringComparer.Ordinal);
            internal readonly HashSet<string> MissingPaths=new HashSet<string>(StringComparer.Ordinal),SlowPaths=new HashSet<string>(StringComparer.Ordinal);
            readonly List<string> requestedPaths=new List<string>();
            internal int Hits(string path) {lock(requestedPaths){int count=0;foreach(string requested in requestedPaths)if(requested==path)count++;return count;}}
            internal readonly string Url;
            internal HttpTestServer(X509Certificate2 certificate=null) {
                this.certificate=certificate;listener.Start();Url=(certificate==null?"http":"https")+"://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port;
                accept=new Thread(Listen);accept.IsBackground=true;accept.Start();
            }
            void Listen() {
                try {for(;;){TcpClient client=listener.AcceptTcpClient();lock(clients){if(stopped){client.Close();return;}clients.Add(client);}Thread worker=new Thread(delegate(){Serve(client);});worker.IsBackground=true;worker.Start();}}
                catch(SocketException) {} catch(ObjectDisposedException) {}
            }
            static void Write(Stream stream,string text) {byte[] bytes=Encoding.ASCII.GetBytes(text);stream.Write(bytes,0,bytes.Length);stream.Flush();}
            static void Respond(Stream stream,int code,string headers,byte[] bytes) {
                Write(stream,"HTTP/1.1 "+code+" Test\r\nConnection: close\r\nContent-Length: "+bytes.Length+"\r\n"+headers+"\r\n");stream.Write(bytes,0,bytes.Length);stream.Flush();
            }
            void Serve(TcpClient client) {
                try {
                    client.ReceiveTimeout=10000;client.SendTimeout=10000;
                    using(client)using(Stream stream=certificate==null?(Stream)client.GetStream():Secure(client)) {
                        StringBuilder request=new StringBuilder();
                        while(request.Length<65536) {int code=stream.ReadByte();if(code<0)return;request.Append((char)code);if(request.ToString().EndsWith("\r\n\r\n",StringComparison.Ordinal))break;}
                        string[] lines=request.ToString().Split(new string[]{"\r\n"},StringSplitOptions.None);
                        string[] first=lines[0].Split(' ');if(first.Length<2||(first[0]!="GET"&&first[0]!="POST"))return;
                        Dictionary<string,string> headers=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                        for(int i=1;i<lines.Length;i++){int colon=lines[i].IndexOf(':');if(colon<0)continue;string key=lines[i].Substring(0,colon),val=lines[i].Substring(colon+1).Trim();headers[key]=headers.ContainsKey(key)?headers[key]+","+val:val;}
                        Interlocked.Increment(ref Requests);string path=first[1];
                        int bodyLength=headers.ContainsKey("Content-Length")?int.Parse(headers["Content-Length"]):0;
                        if(bodyLength<0||bodyLength>33554432)return;
                        if(path=="/slowupload") {
                            Interlocked.Increment(ref Hanging);UploadDrain.WaitOne(10000);byte[] buffer=new byte[65536];int received=0;
                            try{while(received<bodyLength){int count=stream.Read(buffer,0,Math.Min(buffer.Length,bodyLength-received));if(count==0)break;received+=count;}}catch(IOException){}
                            if(received<bodyLength)Interlocked.Increment(ref Aborted);return;
                        }
                        byte[] body=new byte[bodyLength];int readBody=0;
                        while(readBody<bodyLength){int read=stream.Read(body,readBody,Math.Min(65536,bodyLength-readBody));if(read==0)return;readBody+=read;}
                        string resourcePath=path.Split('?')[0];byte[] resource;
                        lock(requestedPaths)requestedPaths.Add(resourcePath);
                        string redirected;
                        if(Redirects.TryGetValue(resourcePath,out redirected)){Respond(stream,302,"Location: "+redirected+"\r\n",new byte[0]);return;}
                        if(MissingPaths.Contains(resourcePath)){Respond(stream,404,"",new byte[0]);return;}
                        if(SlowPaths.Contains(resourcePath)) {
                            Interlocked.Increment(ref Hanging);
                            try{if(stream.ReadByte()<0)Interlocked.Increment(ref Aborted);}catch(IOException){Interlocked.Increment(ref Aborted);}return;
                        }
                        if(Resources.TryGetValue(resourcePath,out resource)){
                            if(resourcePath=="/remote/post.png"&&(first[0]!="POST"||Encoding.UTF8.GetString(body)!="image=1"||!headers.ContainsKey("X-Test")||headers["X-Test"]!="display")){Respond(stream,400,"",new byte[0]);return;}
                            Respond(stream,200,"",resource);return;
                        }
                        if(path=="/display-redirect"){Respond(stream,302,"Location: /remote/child.iggy\r\n",new byte[0]);return;}
                        if(path.StartsWith("/post-redirect/",StringComparison.Ordinal)) {
                            int redirectCode=int.Parse(path.Substring("/post-redirect/".Length));Respond(stream,redirectCode,"Location: /inspect\r\n",new byte[0]);return;
                        }
                        if(path=="/cross-origin") {Respond(stream,307,"Location: "+CrossOrigin+"/inspect\r\n",new byte[0]);return;}
                        if(path=="/echo") {Respond(stream,200,"",body);return;}
                        if(path=="/inspect"||path.StartsWith("/inspect-query?",StringComparison.Ordinal)) {
                            string type=headers.ContainsKey("Content-Type")?headers["Content-Type"]:"",custom=headers.ContainsKey("X-Test")?headers["X-Test"].Replace(", ",","):"",acceptHeader=headers.ContainsKey("Accept")?headers["Accept"]:"";
                            Respond(stream,200,"",Encoding.UTF8.GetBytes(first[0]+"\n"+path+"\n"+type+"\n"+custom+"\n"+acceptHeader+"\n"+Encoding.UTF8.GetString(body)));return;
                        }
                        byte[] text=Encoding.UTF8.GetBytes("ž🙂\0HTTP");
                        if(path=="/slowheaders"||path=="/slowbody") {
                            if(path=="/slowbody"){Write(stream,"HTTP/1.1 200 Test\r\nConnection: close\r\nContent-Length: 2097152\r\n\r\n");byte[] part=new byte[65536];stream.Write(part,0,part.Length);stream.Flush();}
                            Interlocked.Increment(ref Hanging);
                            try {if(stream.ReadByte()<0)Interlocked.Increment(ref Aborted);}catch(IOException){Interlocked.Increment(ref Aborted);}return;
                        }
                        if(path=="/redirect"||path=="/loop"||path=="/unsafe") {Respond(stream,302,"Location: "+(path=="/redirect"?"/text":path=="/loop"?"/loop":"file:///does-not-exist")+"\r\n",new byte[0]);return;}
                        if(path=="/missing"||path=="/failed") {Respond(stream,path=="/missing"?404:500,"",text);return;}
                        if(path=="/oversize"){Write(stream,"HTTP/1.1 200 Test\r\nConnection: close\r\nContent-Length: 33554433\r\n\r\n");return;}
                        if(path=="/truncated"){Write(stream,"HTTP/1.1 200 Test\r\nConnection: close\r\nContent-Length: 65536\r\n\r\nshort");return;}
                        if(path=="/empty"){Respond(stream,204,"",new byte[0]);return;}
                        if(path=="/variables"){Respond(stream,200,"",Encoding.UTF8.GetBytes("name=one&name=two&word=%C5%BE%F0%9F%99%82"));return;}
                        if(path=="/binary"){byte[] bytes=new byte[256];for(int i=0;i<256;i++)bytes[i]=(byte)i;Respond(stream,200,"",bytes);return;}
                        if(path=="/gzip") {using(MemoryStream bytes=new MemoryStream()){using(GZipStream zip=new GZipStream(bytes,CompressionMode.Compress,true))zip.Write(text,0,text.Length);Respond(stream,200,"Content-Encoding: gzip\r\n",bytes.ToArray());}return;}
                        if(path=="/gzip-limit") {using(MemoryStream bytes=new MemoryStream()){using(GZipStream zip=new GZipStream(bytes,CompressionMode.Compress,true)){byte[] part=new byte[65536];for(int i=0;i<513;i++)zip.Write(part,0,part.Length);}Respond(stream,200,"Content-Encoding: gzip\r\n",bytes.ToArray());}return;}
                        if(path=="/chunked") {
                            Write(stream,"HTTP/1.1 200 Test\r\nConnection: close\r\nTransfer-Encoding: chunked\r\n\r\n");
                            // Split inside a UTF-8 sequence to exercise transport buffering.
                            for(int i=0;i<text.Length;i++){Write(stream,"1\r\n");stream.Write(text,i,1);Write(stream,"\r\n");Thread.Sleep(30);}Write(stream,"0\r\n\r\n");return;
                        }
                        if(path=="/text"||path=="/query?word=%C5%BE%F0%9F%99%82&plus=%2B"){Respond(stream,200,"",text);return;}
                        Respond(stream,400,"",new byte[0]);
                    }
                } catch(IOException) {} catch(AuthenticationException) {} catch(ObjectDisposedException) {} catch(SocketException) {}
                finally {lock(clients)clients.Remove(client);}
            }
            Stream Secure(TcpClient client) {
                SslStream stream=new SslStream(client.GetStream(),false);
                try{stream.AuthenticateAsServer(certificate,false,(SslProtocols)3072,false);return stream;}catch{stream.Dispose();throw;}
            }
            public void Dispose() {UploadDrain.Set();lock(clients){stopped=true;listener.Stop();foreach(TcpClient client in clients)client.Close();}accept.Join(1000);}
        }
        static void HttpBase(Native native,string url) {
            IntPtr name=Marshal.StringToHGlobalAnsi("baseUrl"),text=Marshal.StringToHGlobalUni(url);
            try{Check(native.Bind<SetDataUrl>("IggyValueSetStringUTF16RS")(native.Bind<Native.PtrP>("IggyPlayerRootPath")(native.Player),IntPtr.Zero,name,text,url.Length)!=0,"Set HTTP fixture URL");}
            finally{Marshal.FreeHGlobal(name);Marshal.FreeHGlobal(text);}
        }
        static void HttpWait(Native native,Func<bool> done,string label) {
            for(int i=0;i<700&&!done();i++){Thread.Sleep(10);native.Frame(true);}
            Check(done(),label+" (complete="+ReadValue(native,"completed")+", sent="+ReadValue(native,"sent")+", errors="+ReadValue(native,"errors")+", security="+ReadValue(native,"security")+", failures="+ReadValue(native,"failures")+", details="+ReadString(native,"details")+")");
        }
        public static void RunHttpLoader(string dll,string movie,string output,Log log,string renderer) {
            Directory.CreateDirectory(output);string hash=Hash(File.ReadAllBytes(movie));
            using(HttpTestServer server=new HttpTestServer())using(HttpTestServer other=new HttpTestServer())using(Native native=new Native(dll,log,renderer:renderer)) {
                server.CrossOrigin=other.Url;
                native.Open(movie,550,400);HttpBase(native,server.Url);Call(native,"","start");
                log.Write("HTTP fixture URL="+server.Url+", AS3 base="+ReadString(native,"baseUrl"));
                native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);
                HttpWait(native,delegate{return ReadValue(native,"completed")>=8&&ReadValue(native,"errors")>=6&&ReadValue(native,"security")>=1;},"HTTP text/binary/variables, redirects and error responses finish");
                log.Write("HTTP completed="+ReadValue(native,"completed")+", errors="+ReadValue(native,"errors")+", security="+ReadValue(native,"security")+", failures="+ReadValue(native,"failures")+", details="+ReadString(native,"details"));
                Check(ReadValue(native,"completed")==8&&ReadValue(native,"errors")==6&&ReadValue(native,"security")==1&&ReadValue(native,"failures")==0,"HTTP payloads, status/clone/event ordering, queries, gzip/chunks, decoded size limits and redirect errors");
                Call(native,"","requests");native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);
                HttpWait(native,delegate{return ReadValue(native,"sent")>=15;},"GET query data, POST bodies and custom headers finish");
                Check(ReadValue(native,"sent")==15&&ReadValue(native,"failures")==0,"Request snapshots, UTF-8/forms/binary, headers and POST redirect semantics: "+ReadString(native,"details"));
                int uploadHanging=server.Hanging,uploadAborted=server.Aborted;
                Call(native,"","pendingUpload");HttpWait(native,delegate{return server.Hanging>uploadHanging;},"POST reached stalled upload endpoint");
                Call(native,"","closeUpload");server.UploadDrain.Set();
                HttpWait(native,delegate{return native.PendingDataLoads==0&&server.Aborted>uploadAborted;},"Cancelling POST aborts the socket during body upload");
                Call(native,"","cancelProgress");HttpWait(native,delegate{return ReadValue(native,"cancelled")==1&&server.Aborted>=1;},"Progress cancellation aborts stalled network reads");
                Call(native,"","cancelStatus");HttpWait(native,delegate{return ReadValue(native,"cancelled")==2;},"Status listeners may cancel a redirect");
                Call(native,"","replaceStatus");HttpWait(native,delegate{return ReadValue(native,"replaced")==1;},"Status listeners may replace a load");
                int hanging=server.Hanging,aborted=server.Aborted;
                Call(native,"","pending");HttpWait(native,delegate{return server.Hanging>hanging;},"Server received pending headers request");
                Call(native,"","closePending");HttpWait(native,delegate{return ReadValue(native,"cancelled")==3&&server.Aborted>aborted;},"close aborts requests awaiting headers");
                hanging=server.Hanging;aborted=server.Aborted;
                Call(native,"","pending");HttpWait(native,delegate{return server.Hanging>hanging;},"Pending replacement reached server");
                Call(native,"","replacePending");HttpWait(native,delegate{return ReadValue(native,"replaced")==2&&server.Aborted>aborted;},"Replacement aborts old HTTP connection");
                Check(native.PendingDataLoads==0&&ReadValue(native,"cancelledEvents")==0&&ReadValue(native,"failures")==0,"Cancelled jobs have no later callbacks or retained host requests");
                hanging=server.Hanging;aborted=server.Aborted;
                Call(native,"","childNetwork");HttpWait(native,delegate{return server.Hanging>hanging;},"Loaded child starts its network request");
                Call(native,"","stopChild");HttpWait(native,delegate{return native.PendingDataLoads==0&&server.Aborted>aborted;},"unloadAndStop aborts child-owned HTTP requests");
                Call(native,"","release");for(int i=0;i<4;i++){native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);native.Frame(true);}
                Check(native.ByteArrayBufferCount==0,"HTTP ByteArrays are collected");
                hanging=server.Hanging;aborted=server.Aborted;
                Call(native,"","pending");HttpWait(native,delegate{return server.Hanging>hanging;},"Shutdown request reached server");native.Close();
                for(int i=0;i<300&&server.Aborted<=aborted;i++)Thread.Sleep(10);
                Check(native.PendingDataLoads==0&&server.Aborted>aborted,"Movie shutdown aborts pending network I/O");
                TcpListener unused=new TcpListener(IPAddress.Loopback,0);unused.Start();int port=((IPEndPoint)unused.LocalEndpoint).Port;unused.Stop();
                native.Open(movie,550,400);HttpBase(native,"http://127.0.0.1:"+port);Call(native,"","refused");
                HttpWait(native,delegate{return ReadValue(native,"errors")==1;},"Connection refusal reports an asynchronous error");
                Check(ReadValue(native,"failures")==0&&native.PendingDataLoads==0,"Connection failure reports status zero and releases the request");native.Close();
                native.Open(Path.Combine(Path.GetDirectoryName(movie),"url-data","request-only.iggy"),550,400);
                Check(KeyFlag(native,"passed"),"URLRequestMethod and URLRequestHeader activate without URLLoader references");native.Close();
            }
            string certificatePath=Path.Combine(Path.GetDirectoryName(movie),"url-data","localhost.pfx");
            using(X509Certificate2 certificate=new X509Certificate2(certificatePath,"changeit"))using(HttpTestServer server=new HttpTestServer(certificate))using(Native native=new Native(dll,log,renderer:renderer)) {
                native.Open(movie,550,400);HttpBase(native,server.Url);Call(native,"","rejectTls");
                HttpWait(native,delegate{return ReadValue(native,"security")==1;},"Untrusted HTTPS certificate is rejected");
                Check(ReadValue(native,"cancelledEvents")==0&&ReadValue(native,"failures")==0,"TLS failure reports status zero followed by securityError");
                RemoteCertificateValidationCallback previous=ServicePointManager.ServerCertificateValidationCallback;
                // Test-only trust, scoped to this loopback fixture and restored
                // immediately. The production loader never installs a callback.
                ServicePointManager.ServerCertificateValidationCallback=delegate(object sender,X509Certificate cert,X509Chain chain,SslPolicyErrors errors){HttpWebRequest request=sender as HttpWebRequest;return request!=null&&request.RequestUri.AbsoluteUri.StartsWith(server.Url+"/",StringComparison.Ordinal)&&cert!=null&&cert.GetCertHashString()==certificate.GetCertHashString();};
                try {Call(native,"","tls");HttpWait(native,delegate{return ReadValue(native,"completed")==1;},"TLS 1.2 downloads HTTP data");Call(native,"","tlsPost");HttpWait(native,delegate{return ReadValue(native,"sent")==1;},"POST sends UTF-8 data over TLS 1.2");Check(ReadValue(native,"failures")==0,"HTTPS payload and status events");}
                finally {ServicePointManager.ServerCertificateValidationCallback=previous;}
                native.Close();
            }
            foreach(string message in log.Messages)Check(message.IndexOf("Uncaught exception",StringComparison.Ordinal)<0,"No uncaught HTTP loader exceptions");
            Check(Hash(File.ReadAllBytes(movie))==hash,"Network fixture remains unchanged");
            log.Write("PASS: HTTP/HTTPS GET/POST, query/form/text/binary snapshots, headers/validation, 301/302/303/307/308 replay and origin changes, upload cancellation, statuses/reflection, gzip/chunks, limits/errors, child unload, socket abort, GC/shutdown and TLS certificate validation.");
        }
    }
}
