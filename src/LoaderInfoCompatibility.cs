using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace IggyViewer {
    // Per-player compatibility for the two supplied x64 binaries. Only VM data
    // is changed: the native closure remains owned and rooted by its class.
    internal unsafe sealed class LoaderInfoCompatibility {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr Getter(IntPtr context);
        readonly Getter original, replacement;
        readonly IntPtr vm;
        public int StageReads { get; private set; }
        internal Func<IntPtr,IntPtr> LoadedInfo;

        internal static void VerifyBinary(string path,bool modern) {
            string expected=modern?"239B132048C3364F52FA1C8B68F0033307E34BBDD02F7D1CE4E15E67CFD3DC07":"DB1764BFC0714FAD7DA0DE5B31EEBA20FB52CB336B613284FB23C2E87BF9E408";
            using(SHA256 sha=SHA256.Create())using(FileStream stream=File.OpenRead(path)) {
                string actual=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
                if(actual!=expected)throw new NotSupportedException("LoaderInfo compatibility requires the exact supplied Iggy 1.2.30 or 1.2.56 DLL binary.");
            }
        }

        internal LoaderInfoCompatibility(IntPtr module,bool modern,IntPtr player,long sourceLength,Log log) {
            // This is the original on-disk IGGY length, before the host expands
            // relocation/style data. Loading is synchronous, so loaded=total.
            Marshal.WriteInt64(player,112,sourceLength);
            vm=Marshal.ReadIntPtr(player,modern?10560:10480);
            if(vm==IntPtr.Zero)return; // AS3-free movies need no getter.
            if(Marshal.ReadIntPtr(vm)!=player)throw new InvalidDataException("Unexpected Iggy VM owner for LoaderInfo compatibility.");
            IntPtr callback=IntPtr.Add(module,modern?0x82fa0:0x86280);
            int classCount=Marshal.ReadInt32(vm,modern?9648:9624);
            if(classCount<=0||classCount>250)throw new InvalidDataException("Unexpected Iggy built-in class registry.");
            IntPtr closure=IntPtr.Zero;
            for(int i=0;i<classCount;i++) {
                IntPtr entry=IntPtr.Add(vm,(modern?3648:3624)+24*i);
                if(Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(entry))!="DisplayObject")continue;
                IntPtr metadata=Marshal.ReadIntPtr(entry,16);
                IntPtr traits=Marshal.ReadIntPtr(metadata,72);
                int count=(ushort)Marshal.ReadInt16(traits,26);
                if(count<=0||count>256)throw new InvalidDataException("Unexpected Iggy DisplayObject getter table.");
                IntPtr getters=Marshal.ReadIntPtr(traits,72);
                for(int j=0;j<count;j++) {
                    IntPtr function=Marshal.ReadIntPtr(getters,8*j);
                    if(function==IntPtr.Zero||Marshal.ReadByte(function)!=16)continue;
                    if(Marshal.ReadIntPtr(function,16)!=callback)continue;
                    if(closure!=IntPtr.Zero&&closure!=function)throw new InvalidDataException("Ambiguous Iggy loaderInfo getter.");
                    closure=function;
                }
                break;
            }
            if(closure==IntPtr.Zero)throw new InvalidDataException("Cannot locate Iggy loaderInfo getter before AS3 initialization.");
            original=(Getter)Marshal.GetDelegateForFunctionPointer(callback,typeof(Getter));
            replacement=Read;
            // Inherited getters share this function object. Native GC may move
            // it, so retain no closure address and do not write it on Close.
            Marshal.WriteIntPtr(closure,16,Marshal.GetFunctionPointerForDelegate(replacement));
            log.Write("LoaderInfo compatibility installed before AS3: Stage uses movie root; bytesLoaded=bytesTotal="+sourceLength);
        }

        IntPtr Read(IntPtr context) {
            IntPtr* call=(IntPtr*)context;
            if(LoadedInfo!=null) {
                IntPtr value=LoadedInfo(call[3]);
                if(value!=IntPtr.Zero){Marshal.WriteIntPtr(call[1],value);return IntPtr.Zero;}
            }
            if(call[0]!=vm||*(long*)call[3]!=Marshal.ReadInt64(vm,72))return original(context);
            // The original getter reads four context words. Use a local receiver
            // slot referencing the VM's GC-rooted movie; never alter VM.root.
            IntPtr* forwarded=stackalloc IntPtr[4];
            for(int i=0;i<4;i++)forwarded[i]=call[i];
            forwarded[3]=IntPtr.Add(vm,64);
            StageReads++;
            return original((IntPtr)forwarded);
        }

        // Native.Close keeps this object alive through PlayerDestroy. Destruction
        // releases the per-VM closure; the delegate can be collected afterwards.
    }
}
