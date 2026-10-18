using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace IggyViewer {
    internal static partial class SelfTest {
        static void SoundResources(HttpTestServer server,string directory) {
            server.Resources.Add("/tone.mp3",File.ReadAllBytes(Path.Combine(directory,"audio/tone-stereo.mp3")));
            server.Resources.Add("/post.mp3",server.Resources["/tone.mp3"]);
            server.Resources.Add("/vbr.mp3",File.ReadAllBytes(Path.Combine(directory,"audio/tone-vbr.mp3")));
            server.Resources.Add("/bad.mp3",new byte[]{1,2,3,4,5});server.Redirects.Add("/redirect.mp3","/tone.mp3");
            server.Resources.Add("/nested/child.iggy",File.ReadAllBytes(Path.Combine(directory,"audio/sound-child.iggy")));
            server.Resources.Add("/nested/tone.mp3",server.Resources["/tone.mp3"]);server.SlowPaths.Add("/nested/slowheaders");
        }
        public static void RunSoundLoader(string dll,string movie,string output,Log log) {
            Directory.CreateDirectory(output);string directory=Path.GetDirectoryName(movie),hash=Hash(File.ReadAllBytes(movie));
            using(HttpTestServer server=new HttpTestServer())using(Native native=new Native(dll,log,audio:true,renderer:"software")) {
                SoundResources(server,directory);native.Open(movie,550,400);HttpBase(native,server.Url);Call(native,"","start");
                native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);
                HttpWait(native,delegate{return ReadValue(native,"completed")==3;},"Local constructor MP3 and HTTP stereo/VBR loads complete");
                Check(ReadValue(native,"failures")==0,"Sound constructor/events/metadata: "+ReadString(native,"details"));
                Check(ReadValue(native,"local.bytesLoaded")==new FileInfo(Path.Combine(directory,"audio/tone-mono.mp3")).Length&&ReadValue(native,"primary.bytesTotal")==new FileInfo(Path.Combine(directory,"audio/tone-stereo.mp3")).Length,"Sound counters report encoded MP3 bytes, not decoded PCM size");
                Check(ReadString(native,"primaryUrl")==server.Url+"/tone.mp3","Sound.url exposes the final redirected URL");
                Call(native,"","play");Check(KeyFlag(native,"started"),"Downloaded Sound.play returns SoundChannel");
                AudioWait(native,150);Check(AudioRms(native.AudioSnapshot())>40,"Downloaded MP3 is mixed at low volume");
                AudioWait(native,1400);Check(ReadValue(native,"soundCompleted")==1&&native.TrackedSoundChannels==0,"Looped downloaded sound completes once");
                Call(native,"","constraints");HttpWait(native,delegate{return ReadValue(native,"completed")==4;},"POST sound and load constraints");
                Call(native,"","relative");HttpWait(native,delegate{return KeyFlag(native,"childDone");},"Sound created in a remote movie resolves its URL in a delayed callback");Check(KeyFlag(native,"childPassed"),"Remote sound metadata and relative URL");
                Call(native,"","bad");HttpWait(native,delegate{return ReadValue(native,"errors")==4;},"Missing/malformed/oversized/truncated MP3 report errors");
                Check(ReadValue(native,"failures")==0,"Sound load errors and arguments: "+ReadString(native,"details"));
                int hanging=server.Hanging,aborted=server.Aborted;Call(native,"","pending");HttpWait(native,delegate{return server.Hanging>hanging;},"Sound download reaches stalled server");
                Call(native,"","closePending");HttpWait(native,delegate{return server.Aborted>aborted&&native.PendingDataLoads==0;},"Sound.close aborts socket and releases buffers");
                Call(native,"","cancelOpen");HttpWait(native,delegate{return native.PendingDataLoads==0;},"OPEN listener may close Sound");Check(ReadValue(native,"cancelledEvents")==0,"No callbacks after sound cancellation");
                hanging=server.Hanging;aborted=server.Aborted;Call(native,"","childPending");HttpWait(native,delegate{return server.Hanging>hanging;},"Loaded child starts pending sound");Call(native,"","unloadChild");
                HttpWait(native,delegate{return server.Aborted>aborted&&native.PendingDataLoads==0;},"unloadAndStop cancels child sound loading");
                Call(native,"","orphan");native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);AudioWait(native,100);
                Check(native.LoadedSoundBytes>0&&AudioRms(native.AudioSnapshot())>40,"Active channels retain PCM after their Sound is collected");
                AudioWait(native,1800);
                Call(native,"","drop");for(int i=0;i<6;i++){native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);AudioWait(native,20);}
                Check(native.LoadedSoundBytes==0,"Downloaded PCM is collected after Sounds and channels die");
                hanging=server.Hanging;aborted=server.Aborted;Call(native,"","pending");HttpWait(native,delegate{return server.Hanging>hanging;},"Sound pending before shutdown");native.Close();
                for(int i=0;i<300&&server.Aborted<=aborted;i++)Thread.Sleep(10);
                Check(server.Aborted>aborted&&native.PendingDataLoads==0&&native.LoadedSoundBytes==0,"Shutdown cancels sound downloads and retires PCM");
            }
            using(X509Certificate2 certificate=new X509Certificate2(Path.Combine(directory,"url-data/localhost.pfx"),"changeit"))using(HttpTestServer server=new HttpTestServer(certificate))using(Native native=new Native(dll,log,audio:true)) {
                SoundResources(server,directory);native.Open(movie,550,400);HttpBase(native,server.Url);Call(native,"","tls");HttpWait(native,delegate{return ReadValue(native,"security")==1;},"Sound rejects untrusted TLS");
                RemoteCertificateValidationCallback previous=ServicePointManager.ServerCertificateValidationCallback;
                ServicePointManager.ServerCertificateValidationCallback=delegate(object sender,X509Certificate cert,X509Chain chain,SslPolicyErrors errors){HttpWebRequest request=sender as HttpWebRequest;return request!=null&&request.RequestUri.AbsoluteUri.StartsWith(server.Url+"/",StringComparison.Ordinal)&&cert!=null&&cert.GetCertHashString()==certificate.GetCertHashString();};
                try{Call(native,"","start");HttpWait(native,delegate{return ReadValue(native,"completed")==3;},"HTTPS MP3 decoding with scoped fixture trust");Check(ReadValue(native,"failures")==0,"HTTPS sound events");}
                finally{ServicePointManager.ServerCertificateValidationCallback=previous;}
                native.Close();
            }
            foreach(string message in log.Messages)Check(message.IndexOf("Uncaught exception",StringComparison.Ordinal)<0,"No uncaught Sound.load errors");
            Check(Hash(File.ReadAllBytes(movie))==hash,"Sound source unchanged");
            log.Write("PASS: local/HTTP/HTTPS Sound.load and constructor, mono/stereo/VBR, events/counters/GC, POST/redirect/errors, native playback/loops/completion, close and shutdown.");
        }
    }
}
