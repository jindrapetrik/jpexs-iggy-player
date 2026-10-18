using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace IggyViewer {
    internal static partial class SelfTest {
        static void DisplayResources(HttpTestServer server,string directory) {
            server.Resources.Add("/remote/pic.png",File.ReadAllBytes(Path.Combine(directory,"loader","colors.png")));
            server.Resources.Add("/remote/post.png",server.Resources["/remote/pic.png"]);
            server.Resources.Add("/remote/photo.jpg",File.ReadAllBytes(Path.Combine(directory,"loader","photo.jpg")));
            server.Resources.Add("/remote/child.iggy",File.ReadAllBytes(Path.Combine(directory,"url-data","network-child.iggy")));
            server.Resources.Add("/remote/data.txt",Encoding.UTF8.GetBytes("remote-relative"));
            server.Resources.Add("/remote/raw.swf",File.ReadAllBytes(Path.Combine(directory,"as3.swf")));
            server.Resources.Add("/remote/bad.iggy",new byte[]{1,2,3});
            server.Resources.Add("/remote/imports.iggy",File.ReadAllBytes(Path.Combine(directory,"bytes-loader","child.iggy")));
        }
        public static void RunNetworkDisplay(string dll,string movie,string output,Log log,string renderer) {
            Directory.CreateDirectory(output);string hash=Hash(File.ReadAllBytes(movie)),directory=Path.GetDirectoryName(movie);
            using(HttpTestServer server=new HttpTestServer())using(Native native=new Native(dll,log,renderer:renderer)) {
                DisplayResources(server,directory);native.Open(movie,550,400);HttpBase(native,server.Url);Call(native,"","start");
                native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);
                HttpWait(native,delegate{return ReadValue(native,"completed")>=4&&ReadValue(native,"errors")>=5&&KeyFlag(native,"childDone");},"HTTP images/movie, failures and nested relative requests finish");
                log.Write("Network Loader completed="+ReadValue(native,"completed")+", errors="+ReadValue(native,"errors")+", failures="+ReadValue(native,"failures")+", details="+ReadString(native,"details"));
                Check(ReadValue(native,"completed")==4&&ReadValue(native,"errors")==5&&ReadValue(native,"failures")==0&&KeyFlag(native,"childPassed"),"Network Loader content, metadata/events, POST, redirect URL and delayed relative Loader/URLLoader");
                using(Bitmap image=native.Renderer.Snapshot()){Check(image.GetPixel(13,20).ToArgb()==Color.Red.ToArgb(),"Downloaded PNG is rendered");Check(image.GetPixel(163,20).ToArgb()==Color.Red.ToArgb(),"Remote movie's relative PNG is rendered");image.Save(Path.Combine(output,"network-display.png"));}
                int hanging=server.Hanging,aborted=server.Aborted;
                Call(native,"","pending");HttpWait(native,delegate{return server.Hanging>hanging;},"Display request reached stalled server");
                Call(native,"","closePending");HttpWait(native,delegate{return ReadValue(native,"cancelled")==1&&server.Aborted>aborted;},"Loader.close aborts network reads");
                Call(native,"","cancelStatus");HttpWait(native,delegate{return ReadValue(native,"cancelled")==2;},"HTTP status callback may close display loading");
                Call(native,"","cancelInit");HttpWait(native,delegate{return ReadValue(native,"cancelled")==3;},"INIT callback may unload downloaded content");
                Check(ReadValue(native,"cancelledEvents")==0&&native.PendingDataLoads==0,"No false COMPLETE/errors or retained transport after cancellation");
                Call(native,"","unload");for(int i=0;i<5;i++){native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);native.Frame(true);}
                Check(native.LoadedImageBytes==0,"Network bitmap storage is collected");
                hanging=server.Hanging;aborted=server.Aborted;Call(native,"","pending");HttpWait(native,delegate{return server.Hanging>hanging;},"Pending display job started before shutdown");native.Close();
                for(int i=0;i<300&&server.Aborted<=aborted;i++)Thread.Sleep(10);
                Check(server.Aborted>aborted&&native.PendingDataLoads==0&&native.LoadedImageBytes==0,"Movie shutdown aborts pending display transport and releases storage");
            }
            using(X509Certificate2 certificate=new X509Certificate2(Path.Combine(directory,"url-data","localhost.pfx"),"changeit"))using(HttpTestServer server=new HttpTestServer(certificate))using(Native native=new Native(dll,log,renderer:renderer)) {
                DisplayResources(server,directory);native.Open(movie,550,400);HttpBase(native,server.Url);Call(native,"","tls");
                HttpWait(native,delegate{return ReadValue(native,"security")==1;},"Display Loader rejects untrusted TLS certificates");
                RemoteCertificateValidationCallback previous=ServicePointManager.ServerCertificateValidationCallback;
                ServicePointManager.ServerCertificateValidationCallback=delegate(object sender,X509Certificate cert,X509Chain chain,SslPolicyErrors errors){HttpWebRequest request=sender as HttpWebRequest;return request!=null&&request.RequestUri.AbsoluteUri.StartsWith(server.Url+"/",StringComparison.Ordinal)&&cert!=null&&cert.GetCertHashString()==certificate.GetCertHashString();};
                try{Call(native,"","tls");HttpWait(native,delegate{return ReadValue(native,"completed")==1;},"HTTPS image loads with test-only fixture trust");Check(ReadValue(native,"failures")==0,"HTTPS display content and status events");}
                finally{ServicePointManager.ServerCertificateValidationCallback=previous;}
                native.Close();
            }
            foreach(string message in log.Messages)Check(message.IndexOf("Uncaught exception",StringComparison.Ordinal)<0,"No uncaught network display errors");
            Check(Hash(File.ReadAllBytes(movie))==hash,"Network display source remains unchanged");
            log.Write("PASS: HTTP/HTTPS display Loader PNG/JPEG/POST, standalone IGGY with delayed relative image/data loads, final redirect URLs, metadata/events/GC, errors and context restrictions, cancellation/unload/shutdown and certificate checks.");
        }
    }
}
