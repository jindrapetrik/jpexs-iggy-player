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
        static void DependencyResources(HttpTestServer server,string directory) {
            string data=Path.Combine(directory,"network-dependencies");
            byte[] root=File.ReadAllBytes(Path.Combine(data,"root.iggy")),library=File.ReadAllBytes(Path.Combine(data,"library.iggy")),leaf=File.ReadAllBytes(Path.Combine(data,"leaf.iggy"));
            server.Redirects.Add("/entry.iggy","/bundle/root.iggy?asset=1#movie");
            foreach(string prefix in new string[]{"bundle","missing-import","missing-texture","bad-texture","slow-import","slow-texture","denied"}) {
                string path="/"+prefix;
                server.Resources.Add(path+"/root.iggy",root);
                server.Redirects.Add(path+"/sub%20folder/library.iggy","/libs/library.iggy");
                server.Redirects.Add(path+"/leaf.iggy","/leaves/leaf.iggy");
                server.MissingPaths.Add(path+"/root.raw.iggytex");
                server.Resources.Add(path+"/root.iggytex",File.ReadAllBytes(Path.Combine(data,"root.raw.iggytex")));
            }
            server.Resources.Add("/libs/library.iggy",library);server.Resources.Add("/leaves/leaf.iggy",leaf);
            server.Redirects.Add("/libs/nested/leaf.iggy","/leaves/leaf.iggy");
            server.Resources.Add("/libs/library.raw.iggytex",File.ReadAllBytes(Path.Combine(data,"library.raw.iggytex")));
            server.Resources.Add("/bundle/data.txt",Encoding.UTF8.GetBytes("bundle-relative"));
            server.Resources.Add("/bundle/bytes-parent.iggy",File.ReadAllBytes(Path.Combine(data,"bytes-parent.iggy")));
            server.Resources.Add("/bundle/bytes-root.iggy",File.ReadAllBytes(Path.Combine(data,"bytes-root.iggy")));
            server.Redirects.Remove("/missing-import/sub%20folder/library.iggy");server.MissingPaths.Add("/missing-import/sub%20folder/library.iggy");server.MissingPaths.Add("/missing-import/sub%20folder/library.swf");
            server.Resources.Remove("/missing-texture/root.iggytex");server.MissingPaths.Add("/missing-texture/root.iggytex");
            server.Resources["/bad-texture/root.iggytex"]=new byte[]{1,2,3};
            server.Redirects.Remove("/slow-import/sub%20folder/library.iggy");server.SlowPaths.Add("/slow-import/sub%20folder/library.iggy");
            server.Resources.Remove("/slow-texture/root.iggytex");server.SlowPaths.Add("/slow-texture/root.iggytex");
            server.Resources.Add("/cycle/root.iggy",root);server.Resources.Add("/cycle/sub%20folder/library.iggy",File.ReadAllBytes(Path.Combine(data,"cycle.iggy")));
            server.Redirects.Add("/root.iggy","/cycle/root.iggy");
            server.Resources.Add("/unsafe/root.iggy",File.ReadAllBytes(Path.Combine(data,"unsafe.iggy")));
        }
        static void DependencyPixels(Native native,string output) {
            using(Bitmap image=native.Renderer.Snapshot()) {
                Color imported=image.GetPixel(20,20),root=image.GetPixel(80,20);
                Check(imported.ToArgb()==Color.Cyan.ToArgb()&&root.ToArgb()==Color.Magenta.ToArgb(),"Downloaded imported/root textures render in the right positions: "+imported+", "+root);
                image.Save(Path.Combine(output,"network-dependencies.png"));
            }
        }
        public static void RunNetworkDependencies(string dll,string movie,string output,Log log,string renderer) {
            Directory.CreateDirectory(output);string hash=Hash(File.ReadAllBytes(movie)),directory=Path.GetDirectoryName(movie);
            using(HttpTestServer server=new HttpTestServer())using(Native native=new Native(dll,log,renderer:renderer)) {
                DependencyResources(server,directory);native.Open(movie,550,400);HttpBase(native,server.Url);Call(native,"","start");
                native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);
                HttpWait(native,delegate{return ReadValue(native,"completed")==1&&KeyFlag(native,"childDone");},"Remote imported classes, textures and delayed relative request finish");
                Check(KeyFlag(native,"childPassed")&&ReadValue(native,"failures")==0&&ReadValue(native,"statuses")==2,"Nested imported AS3, final root URL, shared leaf and main-only status events");
                Check(server.Hits("/libs/nested/leaf.iggy")==1&&server.Hits("/leaves/leaf.iggy")==1,"Shared dependency payload is fetched once after redirects");
                Check(server.Hits("/libs/library.raw.iggytex")==1&&server.Hits("/bundle/root.raw.iggytex")==1&&server.Hits("/bundle/root.iggytex")==1,"Texture URL uses each movie's final URL and raw/ordinary fallback");
                DependencyPixels(native,output);
                Call(native,"","bytes");HttpWait(native,delegate{return ReadValue(native,"completed")==2&&KeyFlag(native,"childDone");},"Remote loadBytes retains its parent's import base URL");
                Check(KeyFlag(native,"childPassed"),"loadBytes imports include textures from their own final URLs");
                Call(native,"","missing");HttpWait(native,delegate{return ReadValue(native,"errors")==1;},"Missing imported movie reports ioError");
                Call(native,"","textures");HttpWait(native,delegate{return ReadValue(native,"errors")==2;},"Missing companion texture reports ioError");
                Call(native,"","corrupt");HttpWait(native,delegate{return ReadValue(native,"errors")==3;},"Truncated companion texture reports ioError");
                Call(native,"","cycle");HttpWait(native,delegate{return ReadValue(native,"errors")==4;},"Redirected cyclic imports are rejected");
                Call(native,"","unsafe");HttpWait(native,delegate{return ReadValue(native,"security")==1;},"Network imports cannot access local files");
                Call(native,"","denied");HttpWait(native,delegate{return ReadValue(native,"errors")==5;},"allowCodeImport rejects movie before fetching dependencies");
                Check(server.Hits("/denied/sub%20folder/library.iggy")==0&&ReadValue(native,"completed")==2&&ReadValue(native,"failures")==0,"Failures never create content or bypass context restrictions");
                foreach(string method in new string[]{"pendingImport","pendingTexture"}) {
                    int hanging=server.Hanging,aborted=server.Aborted;Call(native,"",method);
                    HttpWait(native,delegate{return server.Hanging>hanging;},"Dependency reached stalled server: "+method);Call(native,"","closePending");
                    HttpWait(native,delegate{return server.Aborted>aborted&&native.PendingDataLoads==0;},"Loader.close aborts dependency: "+method);
                    Check(ReadValue(native,"completed")==2&&ReadValue(native,"errors")==5&&ReadValue(native,"security")==1,"No late callbacks after dependency cancellation");
                }
                Call(native,"","unload");
                int finalHanging=server.Hanging,finalAborted=server.Aborted;Call(native,"","pendingImport");HttpWait(native,delegate{return server.Hanging>finalHanging;},"Dependency started before shutdown");native.Close();
                for(int i=0;i<300&&server.Aborted<=finalAborted;i++)Thread.Sleep(10);
                Check(server.Aborted>finalAborted&&native.PendingDataLoads==0,"Shutdown aborts outstanding dependency sockets");
            }
            using(X509Certificate2 certificate=new X509Certificate2(Path.Combine(directory,"url-data","localhost.pfx"),"changeit"))using(HttpTestServer server=new HttpTestServer(certificate))using(Native native=new Native(dll,log,renderer:renderer)) {
                DependencyResources(server,directory);native.Open(movie,550,400);HttpBase(native,server.Url);Call(native,"","tls");
                HttpWait(native,delegate{return ReadValue(native,"security")==1;},"Untrusted HTTPS dependency movie is rejected");
                RemoteCertificateValidationCallback previous=ServicePointManager.ServerCertificateValidationCallback;
                ServicePointManager.ServerCertificateValidationCallback=delegate(object sender,X509Certificate cert,X509Chain chain,SslPolicyErrors errors){HttpWebRequest request=sender as HttpWebRequest;return request!=null&&request.RequestUri.AbsoluteUri.StartsWith(server.Url+"/",StringComparison.Ordinal)&&cert!=null&&cert.GetCertHashString()==certificate.GetCertHashString();};
                try{Call(native,"","start");HttpWait(native,delegate{return ReadValue(native,"completed")==1&&KeyFlag(native,"childDone");},"HTTPS graph and companion textures finish");Check(KeyFlag(native,"childPassed")&&ReadValue(native,"failures")==0,"HTTPS dependency graph");DependencyPixels(native,output);}
                finally{ServicePointManager.ServerCertificateValidationCallback=previous;}
                native.Close();
            }
            foreach(string message in log.Messages)Check(message.IndexOf("Uncaught exception",StringComparison.Ordinal)<0,"No uncaught dependency loader errors");
            Check(Hash(File.ReadAllBytes(movie))==hash,"Dependency source remains unchanged");
            log.Write("PASS: HTTP/HTTPS imported IGGY graphs and textures, final/relative URLs, shared dependencies, rendered pixels, missing/corrupt/cyclic/unsafe resources, context restrictions and dependency cancellation/shutdown.");
        }
    }
}
