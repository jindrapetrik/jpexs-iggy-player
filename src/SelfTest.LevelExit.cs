using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;

namespace IggyViewer {
    internal static partial class SelfTest {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate uint InvokeMethod(IntPtr player,IntPtr result,IntPtr receiver,IntPtr method,int count,IntPtr args);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr FastName(IntPtr player,IntPtr text,int length);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate uint SetDouble(IntPtr path,IntPtr fastName,IntPtr name,double value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate uint GetDouble(IntPtr path,IntPtr fastName,IntPtr name,out double value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void ArrayPath(IntPtr output,IntPtr parent,int index);
        static void AtPath(Native native,string name,Action<IntPtr> action) {
            IntPtr path=native.Bind<Native.PtrP>("IggyPlayerRootPath")(native.Player);
            List<IntPtr> allocations=new List<IntPtr>();
            try {
                if(name!="")foreach(string part in name.Split('.')) {
                    IntPtr next=Marshal.AllocHGlobal(40),text=Marshal.StringToHGlobalAnsi(part);allocations.Add(next);
                    try {int index;if(int.TryParse(part,out index))native.Bind<ArrayPath>("IggyValuePathMakeArrayRef")(next,path,index);else Check(native.Bind<MakeValuePath>("IggyValuePathMakeNameRef")(next,path,text)!=0,"Path "+name);}finally {Marshal.FreeHGlobal(text);}
                    path=next;
                }
                action(path);
            }finally {foreach(IntPtr p in allocations)Marshal.FreeHGlobal(p);}
        }
        static void Call(Native native,string path,string method,params double[] numbers) {
            AtPath(native,path,delegate(IntPtr receiver) {
                IntPtr text=Marshal.StringToHGlobalAnsi(method),args=Marshal.AllocHGlobal(Math.Max(1,numbers.Length*32));
                try {
                    for(int i=0;i<numbers.Length;i++){Marshal.WriteInt32(args,32*i,4);Marshal.Copy(new double[]{numbers[i]},0,IntPtr.Add(args,32*i+16),1);}
                    IntPtr name=native.Bind<FastName>("IggyPlayerCreateFastNameUTF8")(native.Player,text,method.Length);
                    uint status=native.Bind<InvokeMethod>("IggyPlayerCallMethodRS")(native.Player,IntPtr.Zero,receiver,name,numbers.Length,args);
                    Check(status==0,"Call "+path+"."+method+" status="+status);
                }finally {Marshal.FreeHGlobal(text);Marshal.FreeHGlobal(args);}
            });
        }
        static void SetNumber(Native native,string name,double value) {AtPath(native,name,p=>Check(native.Bind<SetDouble>("IggyValueSetF64RS")(p,IntPtr.Zero,IntPtr.Zero,value)!=0,"Set "+name));}
        static double Number(Native native,string name) {double value=0;AtPath(native,name,delegate(IntPtr p){Check(native.Bind<GetDouble>("IggyValueGetF64RS")(p,IntPtr.Zero,IntPtr.Zero,out value)==0,"Get "+name);});return value;}
        public static void RunZeroScale(string dll,string movie,string output,Log log) {
            Directory.CreateDirectory(output);string hash=Hash(File.ReadAllBytes(movie));
            using(Native baseline=new Native(dll,log)) {
                baseline.CorrectZeroScale=false;baseline.Open(movie,550,400);
                Check(!KeyFlag(baseline,"zero"),"Unmodified DLL reproduces scaleY=0; scaleY=0 becoming 1");
            }
            int start=log.Messages.Count;
            using(Native native=new Native(dll,log)) {
                for(int cycle=0;cycle<3;cycle++) {
                    native.Open(movie,550,400);
                    foreach(int collection in new int[]{0,100,100}) {
                        native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,collection);GC.Collect();GC.WaitForPendingFinalizers();
                        Call(native,"","run");
                        foreach(string field in new string[]{"zero","positive","negative","normal","finite","converted","changed"})Check(KeyFlag(native,field),"scaleY semantics: "+field+" normal scale="+Number(native,"normalScale")+" rotation="+Number(native,"normalRotation"));
                    }
                    native.Close();native.Close();
                }
            }
            for(int i=start;i<log.Messages.Count;i++)Check(!log.Messages[i].Contains("Uncaught exception"),"No zero-scale AS3 exceptions");
            Check(Hash(File.ReadAllBytes(movie))==hash,"Zero-scale source unchanged");
            log.Write("PASS: zero/positive/negative/rotated scaleY, translation and X scale, NaN/Infinity, AS3 conversion and side effects, native/managed GC, reload, negative control.");
        }
        public static void RunLevelExit(string dll,string movie,string output,Log log) {
            Directory.CreateDirectory(output);string hash=Hash(File.ReadAllBytes(movie));
            foreach(bool corrected in new bool[]{false,true})
            using(Native native=new Native(dll,log)) {
                native.CorrectZeroScale=corrected;
                native.StorageDirectory=Path.Combine(Path.GetFullPath(output),"storage-"+Guid.NewGuid().ToString("N"));
                native.Open(movie,550,550);Call(native,"new_time","stop");
                native.Seek(3);native.Seek(4);
                Call(native,"fade_out.wipe","gotoAndStop",30);
                Check(ReadValue(native,"stages.currentFrame")==1&&ReadValue(native,"others.length")==1,"Tutorial contains its real finish portal");
                int index=(int)ReadValue(native,"others.length")-1;
                double x=Number(native,"others."+index+".x"),y=Number(native,"others."+index+".y");
                SetNumber(native,"stages.current_stage.player.x",x);SetNumber(native,"stages.current_stage.player.y",y);
                SetNumber(native,"stages.current_stage.x",275-x);SetNumber(native,"stages.current_stage.y",225-y);
                SetNumber(native,"stages.current_stage.player.time_seconds",81);
                Action tick=native.TickOnce;
                for(int i=0;i<180;i++) {
                    tick();
                    if(i%20==0)log.Write("EXIT corrected="+corrected+" step="+i+" level="+ReadValue(native,"stages.currentFrame")+" wipe="+ReadValue(native,"fade_out.wipe.currentFrame")+" scale="+Number(native,"stages.current_stage.player.scaleY"));
                    if(ReadValue(native,"stages.currentFrame")!=1)break;
                }
                if(!corrected) {
                    Check(ReadValue(native,"stages.currentFrame")==1,"Unmodified DLL reproduces the looping tutorial exit");
                    continue;
                }
                Check(ReadValue(native,"stages.currentFrame")==3,"Tutorial exit reaches the hub");
                for(int i=0;i<240;i++)tick();
                log.Write("HUB wipe="+ReadValue(native,"fade_out.wipe.currentFrame")+" act0_time="+ReadValue(native,"act0_time"));
                Check(ReadValue(native,"fade_out.wipe.currentFrame")==30,"Hub curtain stops");
                Check(ReadValue(native,"act0_time")==81,"Tutorial completion is recorded");
                native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);GC.Collect();GC.WaitForPendingFinalizers();
                Check(native.Frame(false),"Draw the completed tutorial hub");
                using(Bitmap image=native.Renderer.Snapshot())image.Save(Path.Combine(output,"hub.png"),ImageFormat.Png);
                x=Number(native,"stages.current_stage.act1.x")+Number(native,"stages.current_stage.act1.width")/2;
                y=Number(native,"stages.current_stage.act1.y")-50;
                SetNumber(native,"stages.current_stage.player.x",x);SetNumber(native,"stages.current_stage.player.y",y);
                SetNumber(native,"stages.current_stage.x",275-x);SetNumber(native,"stages.current_stage.y",325-y);
                for(int i=0;i<300&&ReadValue(native,"stages.currentFrame")==3;i++)tick();
                log.Write("NEXT LEVEL="+ReadValue(native,"stages.currentFrame"));
                Check(ReadValue(native,"stages.currentFrame")==4,"Act 1 starts through the hub button's original AS3");
                for(int i=0;i<120;i++)tick();
                Check(ReadValue(native,"stages.currentFrame")==4&&ReadValue(native,"fade_out.wipe.currentFrame")==30,"Act 1 remains stable after the transition");
                double before=Number(native,"stages.current_stage.player.x");
                native.Key(37,true);Check(KeyFlag(native,"stages.current_stage.player.aDown"),"Act 1 receives Left key down");
                for(int i=0;i<8;i++)tick();
                native.Key(37,false);
                Check(!KeyFlag(native,"stages.current_stage.player.aDown")&&Number(native,"stages.current_stage.player.x")<before-1,"Act 1 player responds to movement and key release");
                Check(native.Frame(false),"Draw Act 1");
                using(Bitmap image=native.Renderer.Snapshot())image.Save(Path.Combine(output,"act1.png"),ImageFormat.Png);
            }
            foreach(string message in log.Messages)if(message.Contains("Uncaught exception"))
                // The original SWF indexes act-2 after the tutorial's goto(3).
                // It occurs after entering the hub and does not prevent Act 1.
                Check(message.Contains("TypeError getting field $x in method moveFrame"),"No new gameplay exceptions: "+message);
            Check(Hash(File.ReadAllBytes(movie))==hash,"Original Game fixture source unchanged");
            log.Write("PASS: negative control loops, actual tutorial portal -> stable hub, completion time, GC -> actual hub activation -> stable Act 1; original source unchanged. Original moveFrame act-2 lookup remains a non-blocking warning.");
        }
    }
}
