using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace IggyViewer {
    internal static partial class SelfTest {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate long MakeValuePath(IntPtr output,IntPtr parent,IntPtr name);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate uint GetValueNumber(IntPtr path,IntPtr fastName,IntPtr name,out uint value);

        static uint ReadValue(Native native,string name,string export="IggyValueGetU32RS") {
            IntPtr path=native.Bind<Native.PtrP>("IggyPlayerRootPath")(native.Player);
            MakeValuePath make=native.Bind<MakeValuePath>("IggyValuePathMakeNameRef");
            GetValueNumber get=native.Bind<GetValueNumber>(export);
            List<IntPtr> allocations=new List<IntPtr>();
            try {
                foreach(string part in name.Split('.')) {
                    IntPtr next=Marshal.AllocHGlobal(40),text=Marshal.StringToHGlobalAnsi(part);allocations.Add(next);
                    try {Check(make(next,path,text)!=0,"Create value path "+name);}
                    finally {Marshal.FreeHGlobal(text);}
                    path=next;
                }
                uint value;uint status=get(path,IntPtr.Zero,IntPtr.Zero,out value);
                Check(status==0,"Read "+name+" with "+export+", status="+status);return value;
            } finally {foreach(IntPtr allocation in allocations)Marshal.FreeHGlobal(allocation);}
        }

        static void CheckLoaderCounters(Native native,long length) {
            foreach(string name in new string[]{"loaderInfo.bytesLoaded","loaderInfo.bytesTotal","stage.loaderInfo.bytesLoaded","stage.loaderInfo.bytesTotal","root.loaderInfo.bytesTotal"})
                Check(ReadValue(native,name)==length,"Fully loaded original IGGY length at "+name);
        }

        public static void RunLoaderInfo(string dll,string fixture,string output,Log log) {
            Directory.CreateDirectory(output);long length=new FileInfo(fixture).Length;string hash=Hash(File.ReadAllBytes(fixture));
            // Negative control: the same constructor fails with the untouched
            // Stage getter, so this fixture really exercises AS3, not just API.
            using(Native baseline=new Native(dll,log,true,false)) {
                baseline.Open(fixture,550,400);
                Check(ReadValue(baseline,"stage.loaderInfo","IggyValueGetTypeRS")==2,"Unmodified Stage getter is null");
                Check(ReadValue(baseline,"initialTotal")==0,"Unmodified DLL cannot complete fixture constructor");
            }
            int firstMessage=log.Messages.Count;
            using(Native native=new Native(dll,log)) {
                Native.VoidPI collect=native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect");
                for(int cycle=0;cycle<3;cycle++) {
                    native.Open(fixture,550,400);
                    Check(native.LoaderInfoStageReads>=3,"Stage LoaderInfo must work in the AS3 constructor");
                    Check(ReadValue(native,"initialTotal")==length&&ReadValue(native,"initialLoaded")==length,"AS3 constructor receives completed size");
                    Check(ReadValue(native,"initialSame","IggyValueGetBooleanRS")==1,"AS3 Stage and root share LoaderInfo identity");
                    Check(ReadValue(native,"childLoaderInfoNull","IggyValueGetBooleanRS")==1,"Other DisplayObject receivers keep native behavior");
                    CheckLoaderCounters(native,length);
                    Check(native.Frame(false),"Draw LoaderInfo fixture");
                    using(Bitmap image=native.Renderer.Snapshot()) {
                        Check(native.Renderer.Triangles>100,"Constructor finishes and creates visible text");
                        image.Save(Path.Combine(output,"ready-"+cycle+".png"),ImageFormat.Png);
                    }
                    foreach(int kind in new int[]{0,100,100}) {
                        collect(native.Player,kind);GC.Collect();GC.WaitForPendingFinalizers();
                        CheckLoaderCounters(native,length);
                        uint reads=ReadValue(native,"reads");
                        for(int i=0;i<3;i++){Thread.Sleep(50);Check(native.Frame(true),"AS3 frame after GC");}
                        Check(ReadValue(native,"reads")>reads,"AS3 event callback survives native and managed GC");
                        Check(ReadValue(native,"tickTotal")==length,"AS3 Stage counter survives GC");
                    }
                    native.Seek(1);CheckLoaderCounters(native,length);native.Close();native.Close();
                    Check(native.LoaderInfoStageReads==0,"Closed VM releases compatibility state");
                }
            }
            for(int i=firstMessage;i<log.Messages.Count;i++)Check(log.Messages[i].IndexOf("Uncaught exception",StringComparison.Ordinal)<0,"No AS3 exceptions with LoaderInfo compatibility");
            Check(Hash(File.ReadAllBytes(fixture))==hash,"LoaderInfo fixture unchanged on disk");
            log.Write("PASS: Stage/root LoaderInfo identity, constructor and event reads, source byte counts, preserved child behavior, minor/major native GC, managed GC, seek, reopen and repeated Close.");
        }
    }
}
