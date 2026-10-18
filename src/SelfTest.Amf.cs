using System;
using System.IO;
using System.Threading;

namespace IggyViewer {
    internal static partial class SelfTest {
        public static void RunAmf(string dll,string movie,string output,Log log,string renderer) {
            Directory.CreateDirectory(output);string hash=Hash(File.ReadAllBytes(movie));
            using(Native native=new Native(dll,log,renderer:renderer)) {
                for(int cycle=0;cycle<2;cycle++) {
                    native.Open(movie,550,400);
                    for(int i=0;i<300&&!KeyFlag(native,"complete");i++){Thread.Sleep(10);native.Frame(true);}
                    log.Write("AMF checks="+ReadValue(native,"checks")+", failures="+ReadValue(native,"failures")+", details="+ReadString(native,"failureList"));
                    Check(KeyFlag(native,"complete")&&ReadValue(native,"loaded")==2&&ReadValue(native,"checks")>=145&&ReadValue(native,"failures")==0,"AMF serialization and metadata");
                    for(int i=0;i<2;i++){native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);native.Frame(false);}
                    Call(native,"","afterGC");Check(ReadValue(native,"failures")==0,"Deserialized graphs survive moving collection");
                    native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);native.Frame(false);
                    Check(native.ByteArrayBufferCount==0,"AMF temporary buffers are collected");native.Close();
                }
                native.Open(Path.Combine(Path.GetDirectoryName(movie),"amf","only.iggy"),550,400);native.Frame(true);
                Check(KeyFlag(native,"passed"),"XML and Vector activation without ByteArray or Loader references");native.Close();
            }
            Check(hash==Hash(File.ReadAllBytes(movie)),"AMF source remains unchanged");
            foreach(string message in log.Messages)Check(message.IndexOf("Uncaught exception",StringComparison.Ordinal)<0,"No uncaught AMF errors");
            log.Write("PASS: AMF serialization and reopen.");
        }
    }
}
