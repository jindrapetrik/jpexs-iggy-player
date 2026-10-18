using System;
using System.Drawing;
using System.IO;
using System.Threading;
namespace IggyViewer {
    internal static partial class SelfTest {
        public static void RunBitmapData(string dll,string movie,string output,Log log,string renderer) {
            Directory.CreateDirectory(output);string hash=Hash(File.ReadAllBytes(movie));
            using(Native native=new Native(dll,log,renderer:renderer)) {
                native.Open(movie,550,400);
                for(int i=0;i<300&&!KeyFlag(native,"loaded");i++){Thread.Sleep(10);native.Frame(true);}
                Check(KeyFlag(native,"loaded")&&ReadValue(native,"failures")==0,"BitmapData initial pixel/clone/embedded/loaded/error checks: "+ReadString(native,"details"));
                using(Bitmap image=native.Renderer.Snapshot()) {
                    Color alpha=image.GetPixel(14,14);Check(alpha.R==255&&Math.Abs(alpha.G-127)<=1&&Math.Abs(alpha.B-127)<=1,"Premultiplied transparent red renders over white");
                    Check(image.GetPixel(94,14).ToArgb()==Color.Yellow.ToArgb(),"Independent clone initially yellow");
                    Check(image.GetPixel(180,20).ToArgb()==Color.Magenta.ToArgb(),"Opaque fill ignores alpha");
                    Check(image.GetPixel(240,20).ToArgb()==Color.Cyan.ToArgb()&&image.GetPixel(300,20).ToArgb()==Color.Red.ToArgb(),"Embedded source and clone differ");
                    Check(image.GetPixel(363,20).ToArgb()==Color.Cyan.ToArgb()&&image.GetPixel(413,20).ToArgb()==Color.Red.ToArgb(),"Loaded PNG has independent editable copy");
                    image.Save(Path.Combine(output,"pixels-initial.png"));
                }
                Call(native,"","change");native.Frame(false);
                using(Bitmap image=native.Renderer.Snapshot()) {
                    Check(image.GetPixel(14,14).ToArgb()==Color.Lime.ToArgb(),"Existing bitmap texture updates after fillRect");
                    Color alpha=image.GetPixel(94,14);Check(Math.Abs(alpha.R-127)<=1&&Math.Abs(alpha.G-127)<=1&&alpha.B==255,"Existing clone texture updates with alpha");
                    Check(image.GetPixel(180,20).ToArgb()==Color.Yellow.ToArgb()&&image.GetPixel(240,20).ToArgb()==Color.Blue.ToArgb(),"Opaque and embedded texture updates");
                    image.Save(Path.Combine(output,"pixels-updated.png"));
                }
                Call(native,"","convert");Call(native,"","disposeDisplayed");native.Frame(false);
                using(Bitmap image=native.Renderer.Snapshot())Check(image.GetPixel(94,14).ToArgb()==Color.White.ToArgb(),"Disposed displayed bitmap is transparent");
                Check(ReadValue(native,"failures")==0,"Pixel mutations/argument conversion/disposal: "+ReadString(native,"details"));
                native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);native.Frame(false);
                Call(native,"","drop");for(int i=0;i<6;i++){native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);native.Frame(true);}
                Check(native.LoadedImageBytes==0,"All bitmap pixel storage is collected");native.Close();
            }
            foreach(string message in log.Messages)Check(message.IndexOf("Uncaught exception",StringComparison.Ordinal)<0,"No uncaught BitmapData errors");
            Check(Hash(File.ReadAllBytes(movie))==hash,"BitmapData fixture unchanged");
            log.Write("PASS: BitmapData pixel access/alpha/clipping, independent clones, embedded and loaded images, mutable rendered textures, dispose, conversions and GC.");
        }
    }
}
