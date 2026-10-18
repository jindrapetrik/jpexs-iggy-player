using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace IggyViewer {
    internal static partial class SelfTest {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int SetDataUrl(IntPtr path,IntPtr fast,IntPtr name,IntPtr text,int length);
        public static void RunDataLoader(string dll,string movie,string output,Log log,string renderer) {
            Directory.CreateDirectory(output);string hash=Hash(File.ReadAllBytes(movie)),large=Path.Combine(Path.GetFullPath(output),"oversize.bin");
            using(FileStream stream=new FileStream(large,FileMode.Create,FileAccess.Write))stream.SetLength(32L*1024*1024+1);
            using(Native native=new Native(dll,log,renderer:renderer)) {
                for(int cycle=0;cycle<2;cycle++) {
                    native.Open(movie,550,400);Check(ReadValue(native,"completed")==0,"URLLoader is asynchronous");
                    IntPtr name=Marshal.StringToHGlobalAnsi("limitUrl"),text=Marshal.StringToHGlobalUni(large);
                    try {Check(native.Bind<SetDataUrl>("IggyValueSetStringUTF16RS")(native.Bind<Native.PtrP>("IggyPlayerRootPath")(native.Player),IntPtr.Zero,name,text,large.Length)!=0,"Set size-limit test URL");}
                    finally {Marshal.FreeHGlobal(name);Marshal.FreeHGlobal(text);}
                    Call(native,"","startLimit");Call(native,"","loadChild");
                    native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);
                    for(int i=0;i<400&&(ReadValue(native,"completed")<9||ReadValue(native,"errors")<2||!KeyFlag(native,"childDone")||ReadValue(native,"replaced")<1||ReadValue(native,"reloaded")<1||ReadValue(native,"changed")<1);i++){Thread.Sleep(10);native.Frame(true);}
                    log.Write("URLLoader checks="+ReadValue(native,"checks")+", completed="+ReadValue(native,"completed")+", errors="+ReadValue(native,"errors")+", failures="+ReadValue(native,"failures")+", details="+ReadString(native,"details"));
                    log.Write("URLLoader done="+ReadString(native,"doneLabels")+", pending="+native.PendingDataLoads+", cancelled="+ReadValue(native,"cancelledEvents")+", replaced="+ReadValue(native,"replaced")+", reload="+ReadValue(native,"reloaded"));
                    Check(ReadValue(native,"completed")==9&&ReadValue(native,"errors")==2&&ReadValue(native,"security")==1,"Text/binary loads and asynchronous I/O/security failures");
                    Check(ReadValue(native,"failures")==0&&ReadValue(native,"checks")>=30,"URLLoader payloads, BOMs, events, counters and validations");
                    log.Write("URLLoader replacement="+ReadValue(native,"replaced")+", reload="+ReadValue(native,"reloaded")+", changed="+ReadValue(native,"changed")+", cancelled="+ReadValue(native,"cancelledEvents"));
                    Check(ReadValue(native,"replaced")==1&&ReadValue(native,"reloaded")==1&&ReadValue(native,"changed")==1&&ReadValue(native,"cancelledEvents")==0,"Replacement, cancellation and reentrant progress listeners");
                    Check(KeyFlag(native,"childPassed"),"Delayed URLLoader in a loaded movie uses its own relative directory");
                    Call(native,"","stopChild");
                    for(int i=0;i<3;i++){Thread.Sleep(10);native.Frame(true);}
                    Check(native.PendingDataLoads==0,"unloadAndStop cancels pending data jobs");
                    native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);native.Frame(false);
                    Call(native,"","release");
                    for(int i=0;i<4;i++){native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);native.Frame(true);}
                    Check(native.ByteArrayBufferCount==0&&native.PendingDataLoads==0,"Released URLLoader buffers and requests are collected");
                    native.Close();Check(native.PendingDataLoads==0&&native.ByteArrayBufferCount==0,"URLLoader shutdown releases host storage");
                }
                for(int i=0;i<4;i++){native.Open(movie,550,400);native.Close();}
                native.Open(Path.Combine(Path.GetDirectoryName(movie),"url-data","only.iggy"),550,400);
                for(int i=0;i<200&&!KeyFlag(native,"passed");i++){Thread.Sleep(10);native.Frame(true);}
                Check(KeyFlag(native,"passed"),"URLLoader activates without display Loader or ByteArray references");native.Close();
            }
            foreach(string message in log.Messages)Check(message.IndexOf("Uncaught exception",StringComparison.Ordinal)<0,"No uncaught URLLoader errors");
            Check(Hash(File.ReadAllBytes(movie))==hash,"URLLoader fixture remains unchanged");
            log.Write("PASS: asynchronous URLLoader text/binary/BOM/Unicode/large/empty/raw data, counters and events, cancellation/replacement, local path ownership, limits/security, stopped child jobs, GC and shutdown.");
        }
    }
}
