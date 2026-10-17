using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace IggyViewer {
    internal static partial class SelfTest {
        static void CheckGamePlayButton(Bitmap image) {
            int gold=0,ink=0;
            for(int y=380;y<465;y++)for(int x=140;x<395;x++) {
                Color color=image.GetPixel(x*image.Width/550,y*image.Height/550);
                if(color.R>220&&color.G>140&&color.B<100)gold++;
                if(color.R>40&&color.R<120&&color.G<60&&color.B>30&&color.B<130)ink++;
            }
            Check(gold>8000&&ink>600,"Preloader draws the gold Play button and its dark lettering (gold="+gold+", ink="+ink+")");
        }
        public static void RunGamePlayback(string dll,string movie,string output,Log log) {
            Directory.CreateDirectory(output);string hash=Hash(File.ReadAllBytes(movie));
            using(Native native=new Native(dll,log)) {
                native.StorageDirectory=Path.Combine(Path.GetFullPath(output),"storage-"+Guid.NewGuid().ToString("N"));
                native.Open(movie,550,550);
                // PlayerForm.OpenFile resumes the host clock after Native.Open.
                // This used to turn the movie's stop() into a forced play().
                native.SetPaused(false);
                Action tick=native.TickOnce;
                for(int step=0;step<240&&ReadValue(native,"sec_count")<=3;step++) {
                    Thread.Sleep(25);tick();
                    Check(native.CurrentFrame==1,"Opening/resuming must preserve Game fixture's AS3 stop()");
                    if(step==20){native.SetPaused(true);Thread.Sleep(60);native.SetPaused(false);}
                }
                Check(ReadValue(native,"sec_count")>3,"Game fixture preloader completes with GUI resume sequence");
                Check(ReadValue(native,"new_time.running","IggyValueGetBooleanRS")==0,"Preloader timer stops");
                Check(ReadValue(native,"play_btn.visible","IggyValueGetBooleanRS")==1,"Game fixture Play button visible");
                Check(!native.HideSystemCursor,"Game fixture preloader retains system cursor");
                Check(native.Frame(false),"Preloader render");
                using(Bitmap image=native.Renderer.Snapshot()) {
                    image.Save(Path.Combine(output,"preloader.png"),ImageFormat.Png);
                    if(native.Version.StartsWith("Iggy version 1.2.56"))CheckGamePlayButton(image);
                }
                native.Resize(1100,825);Check(native.Frame(false),"Preloader render after nonuniform resize");
                using(Bitmap image=native.Renderer.Snapshot()) {
                    image.Save(Path.Combine(output,"preloader-resized.png"),ImageFormat.Png);
                    if(native.Version.StartsWith("Iggy version 1.2.56"))CheckGamePlayButton(image);
                }
                IntPtr symbols=Marshal.ReadIntPtr(native.Player,552);
                int sounds=0;
                for(int i=0;i<Marshal.ReadInt32(native.Player,560);i++) {
                    IntPtr entry=IntPtr.Add(symbols,24*i),definition=Marshal.ReadIntPtr(entry);
                    if(definition==IntPtr.Zero||Marshal.ReadByte(definition)!=21)continue;
                    IntPtr cls=Marshal.ReadIntPtr(definition,24);
                    Check(cls!=IntPtr.Zero&&Marshal.ReadIntPtr(cls,32)==definition,"Embedded sound class bound to its resource");sounds++;
                }
                Check(sounds>=3,"Game fixture embedded sound class coverage");
                Dictionary<IntPtr,string> bounds=new Dictionary<IntPtr,string>();
                IntPtr dictionary=Marshal.ReadIntPtr(native.Player,152);
                for(int i=0;i<Marshal.ReadInt32(native.Player,160);i++) {
                    IntPtr definition=Marshal.ReadIntPtr(dictionary,8*i);
                    if(definition==IntPtr.Zero||Marshal.ReadByte(definition)!=2)continue;
                    byte[] data=new byte[16];Marshal.Copy(IntPtr.Add(definition,96),data,0,16);bounds[definition]=Hash(data);
                }
                Check(ReadValue(native,"fade_out.wipe.currentFrame")==30,"Preloader waits for Play click");
                native.MouseMove(540,630);native.Frame(false);
                Check(native.HandCursor,"Play cursor before stationary tick");
                tick();Check(native.HandCursor,"Play cursor after stationary tick");
                using(Bitmap image=native.Renderer.Snapshot())image.Save(Path.Combine(output,"play-hover.png"),ImageFormat.Png);
                native.MouseButton(0,true);native.Frame(false);native.MouseButton(0,false);
                foreach(KeyValuePair<IntPtr,string> pair in bounds) {
                    byte[] data=new byte[16];Marshal.Copy(IntPtr.Add(pair.Key,96),data,0,16);Check(Hash(data)==pair.Value,"Input restores shared morph definition bounds");
                }
                for(int step=0;step<12;step++){Thread.Sleep(25);tick();}
                foreach(KeyValuePair<IntPtr,string> pair in bounds) {
                    byte[] data=new byte[16];Marshal.Copy(IntPtr.Add(pair.Key,96),data,0,16);Check(Hash(data)==pair.Value,"Ticks restore shared morph definition bounds");
                }
                Check(ReadValue(native,"fade_out.wipe.currentFrame")>30,"Actual mouse click starts preloader transition");
                native.Resize(550,550);
                for(int step=0;step<1600&&native.CurrentFrame!=3;step++){Thread.Sleep(25);tick();}
                Check(native.CurrentFrame==3,"Game fixture reaches menu through its own AS3 transitions");
                Check(native.HideSystemCursor,"Game fixture frame 3 Mouse.hide suppresses the system cursor");
                Check(ReadValue(native,"trackStarted","IggyValueGetBooleanRS")==1,"startBG constructor and play finish");
                Check(ReadValue(native,"cursor","IggyValueGetTypeRS")!=2,"Menu cursor constructed");
                native.SetPaused(true);Thread.Sleep(60);native.SetPaused(false);
                native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);GC.Collect();GC.WaitForPendingFinalizers();
                for(int step=0;step<240;step++){Thread.Sleep(25);tick();Check(native.CurrentFrame==3,"Resuming menu preserves its stop()");}
                Check(ReadValue(native,"fade_out.wipe.currentFrame")==30,"Menu transition finishes at its scripted stop");
                Check(native.Frame(false),"Game fixture menu render after GC");
                using(Bitmap image=native.Renderer.Snapshot())image.Save(Path.Combine(output,"menu.png"),ImageFormat.Png);
            }
            foreach(string message in log.Messages)Check(message.IndexOf("Uncaught exception",StringComparison.Ordinal)<0,"No AS3 exceptions from open through Game fixture menu");
            Check(Hash(File.ReadAllBytes(movie))==hash,"Game fixture source unchanged");
            log.Write("PASS: GUI resume/pause preserves AS3 stop, preloader timer stops, visible gold Play button and lettering in 1.2.56, actual mouse click, embedded sound class bindings, native AS3 transitions 1 -> 2 -> 3, startBG, menu update, GC and unchanged source. Full gameplay/audio output is not asserted; 1.2.30 lacks Transform.matrix3D.");
        }
    }
}
