using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace IggyViewer {
    internal static partial class SelfTest {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate uint GetUnicodeValue(IntPtr path,IntPtr fastName,IntPtr name,int capacity,IntPtr buffer,out int length);
        static string ReadString(Native native,string name) {
            IntPtr path=native.Bind<Native.PtrP>("IggyPlayerRootPath")(native.Player);
            GetUnicodeValue get=native.Bind<GetUnicodeValue>("IggyValueGetStringUTF16RS");
            IntPtr text=Marshal.StringToHGlobalAnsi(name),buffer=Marshal.AllocHGlobal(4096);
            try {int length;Check(get(path,IntPtr.Zero,text,2048,buffer,out length)==0&&length>=0&&length<2048,"Read string "+name);return Marshal.PtrToStringUni(buffer,length);}
            finally {Marshal.FreeHGlobal(text);Marshal.FreeHGlobal(buffer);}
        }
        public static void RunRuntime(string dll,string movie,string output,Log log) {
            Directory.CreateDirectory(output);byte[] original=File.ReadAllBytes(movie);string hash=Hash(original);
            RuntimeScriptPlan plan=new RuntimeScriptPlan(new byte[][]{original});byte[] transformed=plan.Apply(original,true,log);
            Check(Hash(original)==hash,"Runtime preparation preserves its source");
            Check(Hash(Container.Prepare(transformed,log))==Hash(Container.Prepare(Container.Prepare(transformed,log),log)),"Runtime fixups prepare idempotently");
            string storage=Path.Combine(Path.GetFullPath(output),"storage-"+Guid.NewGuid().ToString("N"));
            for(int session=0;session<2;session++)using(Native native=new Native(dll,log)) {
                native.StorageDirectory=storage;
                for(int cycle=0;cycle<2;cycle++) {
                    native.Open(movie,550,400);
                    foreach(string name in new string[]{"channelReady","persisted","cleared","sameStore","loadReturned","infoType"})
                        Check(ReadValue(native,name,"IggyValueGetBooleanRS")==1,"Runtime constructor: "+name);
                    Check(ReadValue(native,"storageFailed","IggyValueGetBooleanRS")==0,"Successful SharedObject persistence");
                    Check(ReadValue(native,"before")==session*2+cycle,"SharedObject persists across VM reopen and native shutdown");
                    Check(ReadString(native,"literal")=="Loader","QName redirect must preserve string literals");
                    Check(ReadValue(native,"errorsAtReturn")==0,"External loading errors must be asynchronous");
                    native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);GC.Collect();GC.WaitForPendingFinalizers();
                    for(int step=0;step<5;step++){Thread.Sleep(40);Check(native.Frame(true),"Runtime fixture timers");}
                    Check(ReadValue(native,"ioErrors")==1,"Replacement load cancels earlier pending request");
                    foreach(string name in new string[]{"eventTypeOk","eventTextOk","eventTargetOk","eventCountsOk","eventUrlOk","correctEvent"})
                        Check(ReadValue(native,name,"IggyValueGetBooleanRS")==1,"IOErrorEvent metadata: "+name);
                    Check(ReadValue(native,"completes")==0,"No false COMPLETE on unavailable resource");
                    Check(ReadValue(native,"cancelErrors")==0,"Closed Loader must not dispatch an error");
                    CheckLoaderCounters(native,original.LongLength);native.Close();
                }
                string copy=Path.Combine(output,"scope-copy.iggy");File.WriteAllBytes(copy,original);
                // Separate movie identities must not share local saved data.
                if(session==0) {
                    native.Open(copy,550,400);Check(ReadValue(native,"before")==0,"SharedObject isolation between files");native.Close();
                }
                native.Dispose();native.Dispose();
            }
            foreach(string message in log.Messages) {
                Check(message.IndexOf("Uncaught exception",StringComparison.Ordinal)<0,"No runtime AS3 exceptions");
                Check(message.IndexOf("host operation failed",StringComparison.Ordinal)<0,"No hidden persistence failures");
            }
            // An unwritable storage path must produce a catchable failure,
            // never a successful flush or damage to an existing file.
            string blocked=Path.Combine(output,"storage-blocked-"+Guid.NewGuid().ToString("N"));File.WriteAllText(blocked,"preserve");
            using(Native native=new Native(dll,log)) {
                native.StorageDirectory=blocked;native.Open(movie,550,400);
                Check(ReadValue(native,"storageFailed","IggyValueGetBooleanRS")==1,"Failed disk write is catchable in AS3");
                Check(ReadValue(native,"loadReturned","IggyValueGetBooleanRS")==0,"Failed flush does not pretend completion");
            }
            Check(File.ReadAllText(blocked)=="preserve","Storage error preserves blocking file");
            Check(Hash(File.ReadAllBytes(movie))==hash,"Runtime fixture unchanged on disk");
            log.Write("PASS: stopped SoundChannel, persistent SharedObject/clear/Unicode/nested arrays across reopen and shutdown, isolated movies, catchable storage failures, asynchronous Loader IOErrorEvent, cancelled requests, native/.NET GC, untouched literals and original file.");
        }
    }
}
