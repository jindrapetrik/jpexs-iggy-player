using System;
using System.Drawing;
using System.IO;
using System.Threading;

namespace IggyViewer {
    internal static partial class SelfTest {
        public static void RunLoaderBytes(string dll,string movie,string output,Log log,string renderer) {
            Directory.CreateDirectory(output);string hash=Hash(File.ReadAllBytes(movie));
            using(Native native=new Native(dll,log,renderer:renderer)) {
                for(int cycle=0;cycle<2;cycle++) {
                    native.Open(movie,550,400);
                    Check(ReadValue(native,"completed")==0,"loadBytes returns before asynchronous completion");
                    for(int step=0;step<300&&!KeyFlag(native,"complete");step++){Thread.Sleep(10);Check(native.Frame(true),"Advance loadBytes fixture");}
                    log.Write("loadBytes: complete="+KeyFlag(native,"complete")+", loaded="+ReadValue(native,"completed")+", errors="+ReadValue(native,"errors")+", failures="+ReadValue(native,"failures")+", checks="+ReadString(native,"failureList"));
                    Check(KeyFlag(native,"complete")&&ReadValue(native,"completed")==6&&ReadValue(native,"errors")==5,"Images and movies load; invalid, raw SWF, 32-bit and blocked code report errors");
                    Check(ReadValue(native,"failures")==0,"ByteArray copying, snapshot, positions, contexts, imports, constructors and metadata");
                    Check(ReadValue(native,"cancelledEvents")==0&&ReadValue(native,"initCancelled")==1,"close cancels and unload in INIT suppresses COMPLETE");
                    Check(ReadString(native,"order")=="open,progress,init,complete,","loadBytes event ordering");
                    native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);GC.Collect();GC.WaitForPendingFinalizers();native.Frame(false);
                    Check(native.ByteArrayBufferCount==0,"Native collection releases unreachable writable ByteArray storage");
                    using(Bitmap image=native.Renderer.Snapshot()) {
                        Check(image.GetPixel(13,20).ToArgb()==Color.Red.ToArgb(),"ByteArray PNG pixels survive caller mutation and GC");
                        Color jpeg=image.GetPixel(70,20);Check(jpeg.G>250&&jpeg.B>250&&jpeg.R<3,"ByteArray JPEG pixels");
                        Check(image.GetPixel(130,60).ToArgb()==Color.Blue.ToArgb(),"ByteArray movie resolves its imported timeline asset");
                        image.Save(Path.Combine(output,"bytes-"+cycle+".png"));
                    }
                    Call(native,"","stopMovies");for(int i=0;i<12;i++){Thread.Sleep(10);native.Frame(true);}
                    Call(native,"","checkStopped");Check(KeyFlag(native,"stopped"),"unloadAndStop retires ByteArray movie timers and frame listeners");
                    native.Close();
                    Check(native.ByteArrayBufferCount==0,"Movie shutdown releases all writable ByteArray storage");
                }
                for(int i=0;i<4;i++){native.Open(movie,550,400);native.Close();}
            }
            Check(Hash(File.ReadAllBytes(movie))==hash,"loadBytes source remains unchanged");
            foreach(string message in log.Messages)Check(message.IndexOf("Uncaught exception",StringComparison.Ordinal)<0,"No loadBytes AS3 exceptions");
            log.Write("PASS: loadBytes PNG/JPEG/IGGY snapshots, embedded and writable ByteArrays, imports/domains/metadata, event ordering, cancellation, unloadAndStop, GC and reopen.");
        }
    }
}
