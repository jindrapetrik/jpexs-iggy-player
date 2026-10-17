using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;

namespace IggyViewer {
    internal static partial class SelfTest {
        public static void RunMovieUnload(string dll,string movie,string output,Log log,string renderer,bool audio=false) {
            Directory.CreateDirectory(output);string hash=Hash(File.ReadAllBytes(movie));
            using(Native native=new Native(dll,log,renderer:renderer,audio:audio)) {
                for(int cycle=0;cycle<2;cycle++) {
                    native.Open(movie,550,400);
                    for(int step=0;step<300;step++) {
                        Thread.Sleep(10);Check(native.Frame(true),"Advance movie unload fixture");
                        if(ReadValue(native,"ready")==2&&ReadValue(native,"first.content.lateTicks")>2&&ReadValue(native,"second.content.stageTicks")>2&&KeyFlag(native,"first.content.nestedReady")&&KeyFlag(native,"second.content.nestedReady"))break;
                    }
                    Check(ReadValue(native,"ready")==2&&ReadValue(native,"failures")==0,"Both loaded instances initialize normally");
                    Check(ReadValue(native,"first.content.timerTicks")>0&&ReadValue(native,"first.content.lateTicks")>0,"Constructor and later anonymous timers run");
                    Check(ReadValue(native,"first.content.intervalTicks")>0&&ReadValue(native,"first.content.anonymousTimerTicks")>0,"Interval and timer created by anonymous callback run");
                    Check(KeyFlag(native,"first.content.argumentsCorrect")&&ReadValue(native,"first.content.cancelledTimers")==0,"Timer helpers forward arguments and both clear functions cancel IDs");
                    Check(ReadValue(native,"first.content.stageTicks")>0&&ReadValue(native,"first.content.anonymousTicks")>0,"Bound and anonymous Stage listeners run");
                    Check(ReadValue(native,"first.content.timeline.totalFrames")==3,"Nested timeline fixture has multiple frames");
                    uint beforeImageUnload=ReadValue(native,"first.content.ticks");Call(native,"first.content","unloadNestedImage");
                    for(int step=0;step<5;step++){Thread.Sleep(10);Check(native.Frame(true),"Unload image without stopping its containing movie");}
                    Check(ReadValue(native,"first.content.nested.content","IggyValueGetTypeRS")==2&&ReadValue(native,"first.content.ticks")>beforeImageUnload&&KeyFlag(native,"first.content.clock.running"),"unloadAndStop on an image preserves containing movie activity");
                    if(audio) {
                        Call(native,"","startParentSound");Call(native,"first.content","startSounds");Call(native,"second.content","startSounds");
                        native.Frame(true);log.Write("Unload test voices before stop: "+native.AudioChannels);
                        Check(native.AudioChannels==5&&native.TrackedSoundChannels==3,"Parent and two children play AS3 and timeline sounds");
                    }
                    Call(native,"first.content","startPending");Call(native,"","stopFirst",cycle);Check(native.Activity.CollectRequested==(cycle!=0),"gc argument controls the deferred collection hint");native.Frame(false);
                    Check(ReadValue(native,"first.content","IggyValueGetTypeRS")==2,"unloadAndStop clears content");
                    Check(!KeyFlag(native,"saved.clock.running")&&!KeyFlag(native,"saved.late.running"),"Both owned timers are stopped");
                    if(audio)Check(native.AudioChannels==3&&native.TrackedSoundChannels==2,"Only first child's AS3 and timeline voices stop");
                    uint ticks=ReadValue(native,"saved.ticks"),timers=ReadValue(native,"saved.timerTicks"),late=ReadValue(native,"saved.lateTicks"),stage=ReadValue(native,"saved.stageTicks"),anonymous=ReadValue(native,"saved.anonymousTicks"),mouse=ReadValue(native,"saved.mouseTicks"),interval=ReadValue(native,"saved.intervalTicks"),anonymousTimer=ReadValue(native,"saved.anonymousTimerTicks");
                    uint frame=ReadValue(native,"saved.timeline.currentFrame"),detached=ReadValue(native,"saved.detached.currentFrame"),parent=ReadValue(native,"parentTicks"),parentTimer=ReadValue(native,"parentTimerTicks"),other=ReadValue(native,"second.content.ticks"),otherTimer=ReadValue(native,"second.content.timerTicks");
                    Call(native,"","mouseEvent");
                    for(int step=0;step<160;step++){Thread.Sleep(10);Check(native.Frame(true),"Advance parent after unloading one instance");}
                    log.Write("Unload counters: ticks "+ticks+" -> "+ReadValue(native,"saved.ticks")+", Stage "+stage+" -> "+ReadValue(native,"saved.stageTicks")+", anonymous "+anonymous+" -> "+ReadValue(native,"saved.anonymousTicks"));
                    Check(ReadValue(native,"saved.ticks")==ticks&&ReadValue(native,"saved.stageTicks")==stage&&ReadValue(native,"saved.anonymousTicks")==anonymous,"Unloaded frame callbacks stop");
                    Check(ReadValue(native,"saved.timerTicks")==timers&&ReadValue(native,"saved.lateTicks")==late,"Unloaded timers remain inactive");
                    Check(ReadValue(native,"saved.intervalTicks")==interval&&ReadValue(native,"saved.anonymousTimerTicks")==anonymousTimer&&ReadValue(native,"saved.timeoutTicks")==0,"Intervals, pending timeout and callback-created timer stop");
                    Check(ReadValue(native,"saved.pendingDone")==0,"Nested pending loads are cancelled without completion");
                    Check(ReadValue(native,"saved.mouseTicks")==mouse&&ReadValue(native,"second.content.mouseTicks")>0,"Only unloaded Stage mouse listener is removed");
                    Check(ReadValue(native,"saved.timeline.currentFrame")==frame&&ReadValue(native,"saved.detached.currentFrame")==detached,"Nested and detached timelines stop");
                    Check(ReadValue(native,"parentTicks")>parent&&ReadValue(native,"parentTimerTicks")>parentTimer,"Parent events and timer keep running");
                    Check(ReadValue(native,"second.content.ticks")>other&&ReadValue(native,"second.content.timerTicks")>otherTimer,"Other instance sharing classes keeps running");
                    if(audio)Check(native.AudioChannels==3&&ReadValue(native,"saved.soundComplete")==0,"Sibling and parent sounds survive and cancellation does not dispatch SOUND_COMPLETE");
                    using(Bitmap image=native.Renderer.Snapshot())image.Save(Path.Combine(output,"unloaded-"+cycle+".png"),ImageFormat.Png);
                    Call(native,"","releaseSaved");
                    Check(ReadValue(native,"saved","IggyValueGetTypeRS")==2,"Test releases its last saved root reference");
                    for(int gc=0;gc<5;gc++){native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);native.Frame(false);}
                    Check(native.LoadedImageBytes==32*24*4,"Unreferenced unloaded child pixels are reclaimed while sibling pixels remain (bytes="+native.LoadedImageBytes+")");
                    Call(native,"","reuse");
                    for(int step=0;step<300&&ReadValue(native,"ready")<3;step++){Thread.Sleep(10);Check(native.Frame(true),"Reuse stopped Loader");}
                    Check(ReadValue(native,"ready")==3&&ReadValue(native,"failures")==0,"New instance can run after unloadAndStop");
                    Call(native,"","armReentrantStop");
                    log.Write("Unload phase: reentrant stop cycle "+cycle);
                    for(int step=0;step<50&&ReadValue(native,"first.content","IggyValueGetTypeRS")!=2;step++){Thread.Sleep(10);Check(native.Frame(true),"Unload from within a Timer listener");}
                    Check(ReadValue(native,"first.content","IggyValueGetTypeRS")==2&&!KeyFlag(native,"saved.afterStopRunning")&&ReadValue(native,"saved.afterStopTicks")==0,"In-flight callback cannot restart activity after unloadAndStop");
                    Call(native,"","stopSecond");Call(native,"","releaseSaved");
                    log.Write("Unload phase: final GC cycle "+cycle);
                    Check(!KeyFlag(native,"hasCapture"),"Capture-phase Stage listeners are removed from both instances");
                    if(audio)Check(native.AudioChannels==1&&native.TrackedSoundChannels==1,"Parent audio survives unloading both children");
                    for(int step=0;step<5;step++){native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);native.Frame(false);}
                    native.Close();
                    log.Write("Unload phase: closed cycle "+cycle);
                }
                native.Open(Path.Combine(Path.GetDirectoryName(movie),"movie-loader","timer-functions.iggy"),550,400);
                for(int step=0;step<40;step++){Thread.Sleep(10);Check(native.Frame(true),"Standalone timer functions without a Loader reference");}
                Check(ReadValue(native,"ticks")==3&&ReadValue(native,"done")==1&&ReadValue(native,"cancelled")==0&&KeyFlag(native,"argsOk"),"Timer helpers are injected without Loader and preserve arguments, timeout-once and cancellation");
                native.Close();
            }
            foreach(string message in log.Messages)Check(message.IndexOf("Uncaught exception",StringComparison.Ordinal)<0,"No AS3 exception during unloadAndStop");
            Check(Hash(File.ReadAllBytes(movie))==hash,"Unload fixture source is unchanged");
            log.Write("PASS: unloadAndStop, constructor/later timers, intervals/timeouts, bound/anonymous frame and Stage listeners, nested/detached timelines, independent parent/sibling activity, GC hints, pixel reclamation, reload and close; audio="+audio+".");
        }
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
                    Check(ReadValue(native,"failures")==0,"Loaded movie constructor, identity, metadata and explicitly isolated Main classes");
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
            log.Write("PASS: dynamic IGGY content and .swf fallback, isolated Main classes, AS3/text/timeline/transforms, nested images, LoaderInfo/root/parent, events, cancellation, GC, unload/reuse and pending-load shutdown.");
        }
    }
}
