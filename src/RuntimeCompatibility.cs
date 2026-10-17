using System;
using System.IO;
using System.Runtime.InteropServices;

namespace IggyViewer {
    internal unsafe sealed class RuntimeCompatibility {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr Builtin(IntPtr context);
        readonly Builtin emptyChannel;
        readonly LegacyRootProjection legacyProjection;
        readonly ZeroScaleCompatibility zeroScale;
        internal RuntimeCompatibility(IntPtr module,bool modern,IntPtr player,Log log,bool correctZeroScale=true) {
            IntPtr vm=Marshal.ReadIntPtr(player,modern?10560:10480);
            if(vm==IntPtr.Zero)return;
            emptyChannel=CreateEmptyChannel;
            int count=Marshal.ReadInt32(vm,modern?9648:9624);
            if(count<=0||count>250)throw new InvalidDataException("Unexpected Iggy built-in registry.");
            IntPtr textEvent=IntPtr.Zero,errorEvent=IntPtr.Zero,channel=IntPtr.Zero;
            for(int i=0;i<count;i++) {
                IntPtr entry=IntPtr.Add(vm,(modern?3648:3624)+24*i);
                string name=Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(entry));
                if(name=="DisplayObject"&&correctZeroScale)zeroScale=new ZeroScaleCompatibility(module,modern,Marshal.ReadIntPtr(entry,16),log);
                if(name=="TextEvent")textEvent=Marshal.ReadIntPtr(Marshal.ReadIntPtr(entry,16),56);
                if(name=="IOErrorEvent")errorEvent=Marshal.ReadIntPtr(entry,16);
                if(name!="SoundChannel")continue;
                IntPtr metadata=Marshal.ReadIntPtr(entry,16);
                IntPtr expected=IntPtr.Add(module,modern?0x4b730:0xa6190);
                if(Marshal.ReadIntPtr(metadata,56)!=expected)throw new InvalidDataException("Unexpected SoundChannel constructor.");
                channel=metadata;
            }
            if(channel==IntPtr.Zero||textEvent==IntPtr.Zero||errorEvent==IntPtr.Zero)throw new InvalidDataException("Cannot locate runtime compatibility classes.");
            log.Write("SoundChannel compatibility: explicitly constructed channels start stopped.");
            // IOErrorEvent has the same four fields/arguments as TextEvent in
            // these versions; only its constructor was replaced by a stub.
            Marshal.WriteIntPtr(errorEvent,56,textEvent);
            Marshal.WriteIntPtr(channel,56,Marshal.GetFunctionPointerForDelegate(emptyChannel));
            if(!modern)legacyProjection=new LegacyRootProjection(module,vm,log);
        }
        IntPtr CreateEmptyChannel(IntPtr context) {
            IntPtr receiver=Marshal.ReadIntPtr(Marshal.ReadIntPtr(context,24));
            // Native Sound.play creates a channel with a real voice index.
            // A directly constructed empty channel must not refer to voice 0.
            Marshal.WriteInt32(receiver,55,-1);
            Marshal.WriteInt32(receiver,59,0);
            return IntPtr.Zero;
        }
    }
}
