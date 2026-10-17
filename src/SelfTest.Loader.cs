using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace IggyViewer {
    internal static partial class SelfTest {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate uint InvokeRoot(IntPtr player,IntPtr result,IntPtr receiver,IntPtr fastName,int count,IntPtr arguments);
        static void LoaderCall(Native native,string method) {
            IntPtr name=Marshal.StringToHGlobalAnsi(method);
            try {
                IntPtr fast=native.Bind<FastName>("IggyPlayerCreateFastNameUTF8")(native.Player,name,method.Length);
                Check(native.Bind<InvokeRoot>("IggyPlayerCallMethodRS")(native.Player,IntPtr.Zero,native.Bind<Native.PtrP>("IggyPlayerRootPath")(native.Player),fast,0,IntPtr.Zero)==0,"Loader fixture method "+method);
            } finally {Marshal.FreeHGlobal(name);}
        }
        public static void RunLoader(string dll,string movie,string output,Log log,string renderer) {
            Directory.CreateDirectory(output);string hash=Hash(File.ReadAllBytes(movie));
            using(Native native=new Native(dll,log,renderer:renderer)) {
                for(int cycle=0;cycle<3;cycle++) {
                    native.Open(movie,550,400);
                    Check(ReadValue(native,"completed")==0,"Loader.load returns before COMPLETE");
                    for(int step=0;step<200&&(ReadValue(native,"completed")<3||ReadValue(native,"initCancelled")<1||ReadValue(native,"errors")<3);step++){Thread.Sleep(10);Check(native.Frame(true),"Loader decode/render");}
                    Check(ReadValue(native,"completed")==3,"PNG, JPEG and escaped Unicode path complete");
                    Check(ReadValue(native,"failures")==0,"Loaded content, metadata and event checks");
                    Check(ReadValue(native,"errors")==3,"Missing, corrupt and unsupported resources report asynchronous IOErrorEvent");
                    Check(ReadValue(native,"cancelledEvents")==0,"close suppresses completion and errors");
                    Check(ReadValue(native,"initCancelled")==1,"unload inside INIT suppresses COMPLETE");
                    Check(ReadString(native,"order")=="open,progress,init,complete,","OPEN/PROGRESS/INIT/COMPLETE ordering");
                    using(Bitmap image=native.Renderer.Snapshot()) {
                        Check(image.GetPixel(13,20).ToArgb()==Color.Red.ToArgb(),"PNG red channel");
                        Check(image.GetPixel(11,11).ToArgb()==Color.White.ToArgb(),"Transparent PNG pixels");
                        Check(image.GetPixel(21,20).ToArgb()==Color.Lime.ToArgb(),"PNG green channel");
                        Check(image.GetPixel(29,20).ToArgb()==Color.Blue.ToArgb(),"PNG blue channel");
                        Color alpha=image.GetPixel(37,20);Check(alpha.R==255&&Math.Abs(alpha.G-127)<=1&&Math.Abs(alpha.B-127)<=1,"PNG alpha/premultiplication");
                        Color jpeg=image.GetPixel(70,20);Check(jpeg.R<3&&jpeg.G>250&&jpeg.B>250,"JPEG native bitmap");
                        Check(image.GetPixel(113,20).ToArgb()==Color.Red.ToArgb(),"Escaped Unicode image");
                        image.Save(Path.Combine(output,"loaded-"+cycle+".png"),ImageFormat.Png);
                        native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);GC.Collect();GC.WaitForPendingFinalizers();native.Frame(false);
                        using(Bitmap repeated=native.Renderer.Snapshot())Check(Different(image,repeated)==0,"Loaded bitmap survives VM and managed GC");
                    }
                    LoaderCall(native,"cloneImage");LoaderCall(native,"unloadImages");
                    for(int i=0;i<6;i++){native.TickOnce();native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);native.Frame(false);}
                    using(Bitmap empty=native.Renderer.Snapshot())Check(empty.GetPixel(13,20).ToArgb()==Color.White.ToArgb(),"unload removes displayed content");
                    using(Bitmap clone=native.Renderer.Snapshot())Check(clone.GetPixel(163,20).ToArgb()==Color.Red.ToArgb(),"BitmapData clone survives original content unload and GC");
                    Check(native.LoadedImageBytes==32*24*4,"Only retained clone pixels remain after unload and GC (bytes="+native.LoadedImageBytes+")");
                    Check(ReadValue(native,"unloads")==1,"One UNLOAD notification");
                    LoaderCall(native,"releaseClone");for(int i=0;i<6;i++){native.TickOnce();native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);native.Frame(false);}
                    Check(native.LoadedImageBytes==0,"Pixel storage is reclaimed after last clone dies");
                    LoaderCall(native,"reuse");
                    for(int step=0;step<200&&ReadValue(native,"completed")<4;step++){Thread.Sleep(10);Check(native.Frame(true),"Loader reuse");}
                    Check(ReadValue(native,"completed")==4&&ReadValue(native,"failures")==0,"Loader can load again after unload");
                    native.Close();
                }
                // Shut down immediately while worker jobs are still pending.
                for(int i=0;i<5;i++){native.Open(movie,550,400);native.Close();}
            }
            foreach(string message in log.Messages)Check(message.IndexOf("Uncaught exception",StringComparison.Ordinal)<0,"No Loader AS3 exceptions");
            Check(Hash(File.ReadAllBytes(movie))==hash,"Loader fixture unchanged");
            log.Write("PASS: asynchronous PNG/JPEG/Unicode loads, native colors/alpha, events/metadata/content, close/unload, GC, reopen and pending-worker shutdown.");
        }
    }
}
