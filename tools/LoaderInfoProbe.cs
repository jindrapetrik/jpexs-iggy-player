using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace IggyViewer {
    // Standalone diagnostic executable: built with the player sources using
    // /main:IggyViewer.LoaderInfoProbe; the shipped player is not modified.
    internal static class LoaderInfoProbe {
        [DllImport("kernel32",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
        [DllImport("kernel32")] static extern IntPtr GetProcAddress(IntPtr module,string name);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr RootPath(IntPtr player);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate long NamePath(IntPtr output,IntPtr parent,IntPtr name);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate uint ValueType(IntPtr path,IntPtr fastName,IntPtr name,out int type);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate uint GetNumber(IntPtr path,IntPtr fastName,IntPtr name,out uint value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate uint SetNumber(IntPtr path,IntPtr fastName,IntPtr name,uint value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr ValueRef(IntPtr path,int weak,IntPtr reserved,int reserved2);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate uint SetRef(IntPtr path,IntPtr fastName,IntPtr name,IntPtr valueRef);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void FreeRef(IntPtr player,IntPtr valueRef);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr Builtin(IntPtr context);
        static T Bind<T>(IntPtr module,string name) where T:class {
            IntPtr address=GetProcAddress(module,name);if(address==IntPtr.Zero)throw new InvalidOperationException(name);
            return (T)(object)Marshal.GetDelegateForFunctionPointer(address,typeof(T));
        }
        static IntPtr PathTo(IntPtr root,string name,NamePath make,List<IntPtr> allocations) {
            IntPtr parent=root;
            foreach(string part in name.Split('.')) {
                IntPtr path=Marshal.AllocHGlobal(40),text=Marshal.StringToHGlobalAnsi(part);allocations.Add(path);
                try {if(make(path,parent,text)==0)throw new InvalidOperationException("Cannot make path: "+name);}
                finally{Marshal.FreeHGlobal(text);}
                parent=path;
            }
            return parent;
        }
        static int Main(string[] args) {
            if(args.Length!=3){Console.Error.WriteLine("Usage: LoaderInfoProbe DLL IGGY LOG");return 2;}
            string hash;
            using(SHA256 sha=SHA256.Create())using(FileStream file=File.OpenRead(args[0]))hash=BitConverter.ToString(sha.ComputeHash(file)).Replace("-","");
            if(hash!="239B132048C3364F52FA1C8B68F0033307E34BBDD02F7D1CE4E15E67CFD3DC07"&&hash!="DB1764BFC0714FAD7DA0DE5B31EEBA20FB52CB336B613284FB23C2E87BF9E408") {
                Console.Error.WriteLine("Diagnostic offsets are valid only for the two analyzed DLL binaries.");return 2;
            }
            Native.SetErrorMode(3);
            Log log=new Log(args[2]);
            using(Native native=new Native(args[0],log,true,false)) {
                native.Open(args[1],550,400);
                IntPtr module=GetModuleHandle(Path.GetFileName(args[0]));
                RootPath rootPath=Bind<RootPath>(module,"IggyPlayerRootPath");
                NamePath make=Bind<NamePath>(module,"IggyValuePathMakeNameRef");
                ValueType getType=Bind<ValueType>(module,"IggyValueGetTypeRS");
                GetNumber getNumber=Bind<GetNumber>(module,"IggyValueGetU32RS");
                SetNumber setNumber=Bind<SetNumber>(module,"IggyValueSetU32RS");
                ValueRef valueRef=Bind<ValueRef>(module,"IggyValueRefFromPath");
                SetRef setRef=Bind<SetRef>(module,"IggyValueSetValueRefRS");
                FreeRef freeRef=Bind<FreeRef>(module,"IggyValueRefFree");
                IntPtr root=rootPath(native.Player);
                log.Write("Source length="+new FileInfo(args[1]).Length+", player+112="+Marshal.ReadInt64(native.Player,112));
                foreach(string name in new string[]{"loaderInfo","loaderInfo.bytesTotal","loaderInfo.bytesLoaded","stage","stage.loaderInfo","stage.loaderInfo.bytesTotal","stage.loaderInfo.bytesLoaded","root.loaderInfo","root.loaderInfo.bytesTotal"}) {
                    List<IntPtr> allocations=new List<IntPtr>();
                    try {
                        IntPtr path=PathTo(root,name,make,allocations);int type;uint status=getType(path,IntPtr.Zero,IntPtr.Zero,out type);
                        log.Write(name+": typeStatus="+status+", type="+type);
                        if(name.EndsWith("bytesTotal")||name.EndsWith("bytesLoaded")) {
                            uint value;status=getNumber(path,IntPtr.Zero,IntPtr.Zero,out value);
                            log.Write(name+": numberStatus="+status+", value="+value);
                            if(name=="loaderInfo.bytesTotal") {
                                log.Write("Attempt write 12345: success="+setNumber(path,IntPtr.Zero,IntPtr.Zero,12345));
                                status=getNumber(path,IntPtr.Zero,IntPtr.Zero,out value);log.Write("After write: status="+status+", value="+value);
                            }
                        }
                    } finally {foreach(IntPtr allocation in allocations)Marshal.FreeHGlobal(allocation);}
                }
                List<IntPtr> paths=new List<IntPtr>();
                try {
                    IntPtr loader=PathTo(root,"loaderInfo",make,paths),stage=PathTo(root,"stage.loaderInfo",make,paths);
                    IntPtr reference=valueRef(loader,0,IntPtr.Zero,0);
                    try {
                        log.Write("Assign root.loaderInfo to stage.loaderInfo via public API: success="+setRef(stage,IntPtr.Zero,IntPtr.Zero,reference));
                        int type;uint status=getType(stage,IntPtr.Zero,IntPtr.Zero,out type);log.Write("stage.loaderInfo after assignment: status="+status+", type="+type);
                    } finally{freeRef(native.Player,reference);}
                } finally{foreach(IntPtr path in paths)Marshal.FreeHGlobal(path);}
                // Isolated feasibility probe. No hook is installed and no code,
                // movie bytes or production player executable are changed.
                bool modern=native.Version.StartsWith("Iggy version 1.2.56\n");
                IntPtr vm=Marshal.ReadIntPtr(native.Player,modern?10560:10480);
                Builtin getter=(Builtin)Marshal.GetDelegateForFunctionPointer(IntPtr.Add(module,modern?0x82fa0:0x86280),typeof(Builtin));
                paths=new List<IntPtr>();
                IntPtr context=Marshal.AllocHGlobal(80),stageReference=IntPtr.Zero,loaderReference=IntPtr.Zero;
                long savedLength=Marshal.ReadInt64(native.Player,112);
                try {
                    stageReference=valueRef(PathTo(root,"stage",make,paths),0,IntPtr.Zero,0);
                    loaderReference=valueRef(PathTo(root,"loaderInfo",make,paths),0,IntPtr.Zero,0);
                    if(stageReference==IntPtr.Zero||loaderReference==IntPtr.Zero)throw new InvalidOperationException("Missing diagnostic references");
                    for(int i=0;i<80;i+=8)Marshal.WriteInt64(context,i,0);
                    IntPtr output=IntPtr.Add(context,64);
                    Marshal.WriteIntPtr(context,0,vm);Marshal.WriteIntPtr(context,8,output);
                    Marshal.WriteIntPtr(context,24,stageReference);
                    getter(context);long stageValue=Marshal.ReadInt64(output);
                    log.Write("Native getter with Stage receiver: equals root LoaderInfo="+(stageValue==Marshal.ReadInt64(loaderReference)));
                    // Model a wrapper that forwards only Stage.loaderInfo to
                    // the original getter with the movie-root receiver slot.
                    Marshal.WriteIntPtr(context,24,IntPtr.Add(vm,64));
                    getter(context);
                    bool forwarded=Marshal.ReadInt64(output)==Marshal.ReadInt64(loaderReference);
                    log.Write("Native getter forwarded with root receiver: equals root LoaderInfo="+forwarded);
                    if(!forwarded||stageValue==Marshal.ReadInt64(loaderReference))throw new InvalidOperationException("Unexpected loaderInfo implementation");
                    long sourceLength=new FileInfo(args[1]).Length;
                    Marshal.WriteInt64(native.Player,112,sourceLength);
                    foreach(string name in new string[]{"loaderInfo.bytesTotal","loaderInfo.bytesLoaded"}) {
                        uint value;uint status=getNumber(PathTo(root,name,make,paths),IntPtr.Zero,IntPtr.Zero,out value);
                        log.Write("Temporary native source length: "+name+" status="+status+", value="+value);
                        if(status!=0||value!=sourceLength)throw new InvalidOperationException("Counter proof failed");
                    }
                } finally {
                    Marshal.WriteInt64(native.Player,112,savedLength);
                    if(stageReference!=IntPtr.Zero)freeRef(native.Player,stageReference);
                    if(loaderReference!=IntPtr.Zero)freeRef(native.Player,loaderReference);
                    Marshal.FreeHGlobal(context);foreach(IntPtr path in paths)Marshal.FreeHGlobal(path);
                }
                log.Write("Native source length restored="+Marshal.ReadInt64(native.Player,112));
                GC.KeepAlive(rootPath);GC.KeepAlive(make);GC.KeepAlive(getType);GC.KeepAlive(getNumber);GC.KeepAlive(setNumber);
            }
            return 0;
        }
    }
}
