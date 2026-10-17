using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;

namespace IggyViewer {
    internal static partial class SelfTest {
        public static void RunMovieLoader(string dll,string movie,string output,Log log,string renderer) {
            Directory.CreateDirectory(output);string hash=Hash(File.ReadAllBytes(movie));
            using(Native native=new Native(dll,log,renderer:renderer)) {
                for(int cycle=0;cycle<3;cycle++) {
                    native.Open(movie,550,400);Check(ReadValue(native,"ready")==0,"Movie loading is asynchronous");
                    for(int step=0;step<300;step++) {
                        Thread.Sleep(10);Check(native.Frame(true),"Advance loaded movies");
                        if(ReadValue(native,"ready")==2&&KeyFlag(native,"plainReady")&&ReadValue(native,"errors")==3&&KeyFlag(native,"first.content.nestedReady")&&KeyFlag(native,"second.content.nestedReady"))break;
                    }
                    Check(ReadValue(native,"ready")==2,"Two IGGY movies complete, including .swf URL fallback");
                    Check(ReadValue(native,"failures")==0,"Loaded movie constructor, identity, metadata and parent/child Main separation");
                    Check(ReadValue(native,"errors")==3&&ReadValue(native,"cancelled")==0,"Missing/32-bit/corrupt movies fail and cancelled movie stays silent");
                    Check(KeyFlag(native,"plainReady"),"Timeline-only IGGY uses an ordinary MovieClip");
                    Check(ReadString(native,"order")=="open,init,complete,","Movie event ordering");
                    Check(ReadValue(native,"first.content.totalFrames")==3,"Loaded root preserves timeline frame count");
                    Check(KeyFlag(native,"first.content.nestedReady")&&KeyFlag(native,"second.content.nestedReady"),"Loaded script resolves nested PNG relative to child file");
                    Check(ReadValue(native,"first.content.ticks")>0,"Loaded AS3 ENTER_FRAME runs in parent VM");
                    using(Bitmap image=native.Renderer.Snapshot()) {
                        Check(image.GetPixel(20,60).ToArgb()==Color.Blue.ToArgb(),"First child timeline shape");
                        Check(image.GetPixel(270,60).ToArgb()==Color.Blue.ToArgb(),"Second child placement transform");
                        Check(image.GetPixel(20,230).ToArgb()==Color.Blue.ToArgb(),"Script-free child timeline shape");
                        Check(image.GetPixel(34,104).ToArgb()==Color.Red.ToArgb(),"First child nested bitmap");
                        Check(image.GetPixel(284,104).ToArgb()==Color.Red.ToArgb(),"Second child nested bitmap");
                        Check(native.Renderer.Triangles>200,"Loaded AS3 creates visible text");
                        image.Save(Path.Combine(output,"movies-"+cycle+".png"),ImageFormat.Png);
                        for(int i=0;i<3;i++){native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);native.Frame(false);}
                        GC.Collect();GC.WaitForPendingFinalizers();native.Frame(false);
                        using(Bitmap repeated=native.Renderer.Snapshot())Check(Different(image,repeated)==0,"Loaded definitions and nested images survive native/managed GC");
                    }
                    Call(native,"first.content","gotoAndStop",3);Check(ReadValue(native,"first.content.currentFrame")==3,"Parent can call child's MovieClip API");
                    uint ticks=ReadValue(native,"first.content.ticks");native.TickOnce();native.Frame(false);
                    Check(ReadValue(native,"first.content.ticks")>ticks&&ReadValue(native,"first.content.currentFrame")==3,"Stopped timeline retains normal ENTER_FRAME delivery");
                    Call(native,"","unloadMovies");native.Frame(false);
                    Check(ReadValue(native,"first.content","IggyValueGetTypeRS")==2,"unload clears Loader.content");
                    Check(KeyFlag(native,"saved.constructorInfo"),"Retained loaded root remains usable after unload");
                    Check(ReadValue(native,"saved.loaderInfo.bytesLoaded")>0,"Retained movie keeps LoaderInfo after unload");
                    using(Bitmap empty=native.Renderer.Snapshot())Check(empty.GetPixel(20,60).ToArgb()==Color.White.ToArgb()&&empty.GetPixel(270,60).ToArgb()==Color.White.ToArgb(),"unload removes both movies from rendering");
                    Call(native,"","releaseSaved");Call(native,"","reuse");
                    for(int step=0;step<300&&ReadValue(native,"ready")<3;step++){Thread.Sleep(10);Check(native.Frame(true),"Load again after unload");}
                    Check(ReadValue(native,"ready")==3&&ReadValue(native,"failures")==0,"Reused Loader instantiates movie again");
                    native.Close();
                }
                for(int i=0;i<5;i++){native.Open(movie,550,400);native.Close();}
            }
            foreach(string message in log.Messages) {
                Check(message.IndexOf("Uncaught exception",StringComparison.Ordinal)<0,"No loaded movie AS3 exceptions");
                Check(message.IndexOf("still has dependencies",StringComparison.Ordinal)<0,"Dynamic libraries close in dependency order");
            }
            Check(Hash(File.ReadAllBytes(movie))==hash,"Source movie is unchanged");
            log.Write("PASS: dynamic IGGY content and .swf fallback, colliding Main classes, AS3/text/timeline/transforms, nested images, LoaderInfo/root/parent, events, cancellation, GC, unload/reuse and pending-load shutdown.");
        }
    }
}
