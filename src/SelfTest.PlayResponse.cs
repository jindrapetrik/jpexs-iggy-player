using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
namespace IggyViewer {
    internal static partial class SelfTest {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr TestValueRef(IntPtr path,int weak,IntPtr reserved,int flags);
        static bool ClipPlaying(Native native,string name) {
            bool playing=false;AtPath(native,name,delegate(IntPtr path) {
                IntPtr reference=native.Bind<TestValueRef>("IggyValueRefFromPath")(path,0,IntPtr.Zero,0);
                try {IntPtr instance=Marshal.ReadIntPtr(Marshal.ReadIntPtr(reference),39);playing=(Marshal.ReadByte(instance,185)&1)!=0;}
                finally {native.Bind<Native.VoidPP>("IggyValueRefFree")(native.Player,reference);}
            });return playing;
        }
        public static void RunPlayResponse(string dll,string movie,string output,Log log) {
            Directory.CreateDirectory(output);string hash=Hash(File.ReadAllBytes(movie));
            using(Native native=new Native(dll,log)) {
                native.StorageDirectory=Path.Combine(Path.GetFullPath(output),"storage-"+Guid.NewGuid().ToString("N"));
                native.Open(movie,2050,1124);
                for(int i=0;i<240&&ReadValue(native,"sec_count")<=3;i++){Thread.Sleep(25);native.TickOnce();}
                Check(ReadValue(native,"sec_count")>3,"Preloader ready");
                native.SetPaused(true);Check(native.Frame(false),"Large-window preloader render");
                using(Bitmap image=native.Renderer.Snapshot())image.Save(Path.Combine(output,"before-click.png"),ImageFormat.Png);
                Check(!ClipPlaying(native,"fade_out.wipe"),"Curtain waits for click");
                native.MouseMove(270*2050/550,420*1124/550);
                Check(native.HandCursor,"Game fixture Play button displays a hand cursor");
                native.Frame(false);Check(native.HandCursor,"Game fixture Play hand persists after drawing without mouse movement");
                native.SetPaused(false);
                for(int i=0;i<30;i++) {
                    native.TickOnce();Check(native.HandCursor,"Game fixture Play hand persists over stationary simulation tick "+i);
                }
                Stopwatch watch=Stopwatch.StartNew();
                native.MouseButton(0,true);native.MouseButton(0,false);
                Check(ClipPlaying(native,"fade_out.wipe"),"First click starts the original AS3 transition synchronously");
                log.Write("First click accepted in "+watch.ElapsedMilliseconds+" ms.");
                // Omit intermediate draws and all further input to simulate
                // time spent inside the software renderer in a large window.
                Thread.Sleep(1200);
                Check(native.Frame(true),"Catch up after a slow frame");
                Check(native.CurrentFrame==2,"A single click reaches the next screen without repeated input");
                Check(native.LastAdvancedFrames>20,"Animation consumes elapsed movie time, not one tick per draw");
                log.Write("Next screen after "+watch.ElapsedMilliseconds+" ms; simulation ticks="+native.LastAdvancedFrames);
                using(Bitmap image=native.Renderer.Snapshot())image.Save(Path.Combine(output,"after-click.png"),ImageFormat.Png);
                native.SetPaused(true);int frame=native.CurrentFrame;uint wipe=ReadValue(native,"fade_out.wipe.currentFrame");
                Thread.Sleep(150);native.MouseMove(20,20);native.Frame(false);
                Check(native.CurrentFrame==frame&&ReadValue(native,"fade_out.wipe.currentFrame")==wipe&&native.LastAdvancedFrames==0,"Paused input and drawing do not tick");
            }
            foreach(string message in log.Messages)Check(!message.Contains("Uncaught exception"),"No AS3 exceptions during first-click transition");
            Check(Hash(File.ReadAllBytes(movie))==hash,"Game fixture source unchanged");
            log.Write("PASS: stationary Game fixture Play hand over draw and 30 ticks, first click at 2050x1124, no repeated clicks, next screen after a slow frame, paused input, unchanged source.");
        }
        public static void RunClock(string dll,string movie,string output,Log log) {
            Directory.CreateDirectory(output);
            using(Native native=new Native(dll,log)) {
                native.Open(movie,640,480);native.Frame(true);int start=native.CurrentFrame;
                Thread.Sleep(250);native.Frame(true);
                Check(native.LastAdvancedFrames>=5&&native.CurrentFrame>=start+5,"Slow rendering catches up several movie frames");
                native.SetPaused(true);start=native.CurrentFrame;Thread.Sleep(300);native.Frame(false);
                Check(native.CurrentFrame==start&&native.LastAdvancedFrames==0,"Paused clock does not advance");
                native.SetPaused(false);native.Frame(true);
                Check(native.LastAdvancedFrames<=2,"Resume does not replay time spent paused");
                native.Close();native.Open(movie,640,480);native.Frame(true);
                Check(native.CurrentFrame<=2,"Reopen resets playback clock and backlog");
            }
            log.Write("PASS: elapsed-time catch-up, pause/resume without paused-time backlog, reopen clock reset.");
        }
    }
}
