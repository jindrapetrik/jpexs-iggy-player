using System;
using System.IO;
using System.Runtime.InteropServices;

namespace IggyViewer {
    // 1.2.30 already has native PerspectiveProjection accessors and the root's
    // projection fields, but never registers Transform.perspectiveProjection.
    internal unsafe sealed class LegacyRootProjection {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr Getter(IntPtr context);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr Factory(IntPtr vm,IntPtr metadata);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr MakeName(IntPtr vm,IntPtr ns,IntPtr text);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void AddProperty(IntPtr vm,IntPtr obj,IntPtr name,IntPtr value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void Barrier(IntPtr vm,IntPtr from,IntPtr to);
        readonly IntPtr vm,projection,name;
        readonly Getter original,replacement;
        readonly Factory create;
        readonly AddProperty add;
        readonly MakeName find;
        readonly Barrier barrier;
        static T Bind<T>(IntPtr module,int offset) where T:class {return (T)(object)Marshal.GetDelegateForFunctionPointer(IntPtr.Add(module,offset),typeof(T));}
        internal LegacyRootProjection(IntPtr module,IntPtr vm,Log log) {
            this.vm=vm;original=Bind<Getter>(module,0x877e0);create=Bind<Factory>(module,0x47890);
            add=Bind<AddProperty>(module,0xb5040);barrier=Bind<Barrier>(module,0x49ab0);replacement=Read;
            find=Bind<MakeName>(module,0xb5cf0);
            int count=Marshal.ReadInt32(vm,9624);IntPtr closure=IntPtr.Zero;
            for(int i=0;i<count;i++) {
                IntPtr entry=IntPtr.Add(vm,3624+24*i);string cls=Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(entry));
                if(cls=="PerspectiveProjection")projection=Marshal.ReadIntPtr(entry,16);
                if(cls!="DisplayObject")continue;
                IntPtr traits=Marshal.ReadIntPtr(Marshal.ReadIntPtr(entry,16),72),getters=Marshal.ReadIntPtr(traits,72);
                for(int j=0;j<(ushort)Marshal.ReadInt16(traits,26);j++) {
                    IntPtr function=Marshal.ReadIntPtr(getters,8*j);
                    if(function!=IntPtr.Zero&&Marshal.ReadByte(function)==16&&Marshal.ReadIntPtr(function,16)==IntPtr.Add(module,0x877e0))closure=function;
                }
            }
            if(projection==IntPtr.Zero||closure==IntPtr.Zero)throw new InvalidDataException("Missing legacy projection support.");
            IntPtr text=Marshal.StringToHGlobalAnsi("perspectiveProjection");
            try {name=Bind<MakeName>(module,0x225a0)(vm,Marshal.ReadIntPtr(vm,3480),text);}
            finally {Marshal.FreeHGlobal(text);}
            log.Write("Legacy root perspectiveProjection connected to existing native projection fields.");
            Marshal.WriteIntPtr(closure,16,Marshal.GetFunctionPointerForDelegate(replacement));
        }
        IntPtr Read(IntPtr context) {
            IntPtr result=original(context);IntPtr* call=(IntPtr*)context;
            if(call[0]!=vm||*(long*)call[3]!=Marshal.ReadInt64(vm,64))return result;
            IntPtr transform=Marshal.ReadIntPtr(call[1]);
            if(transform.ToInt64()<65536||Marshal.ReadByte(transform,-1)<0x20)return result;
            if(find(vm,IntPtr.Subtract(Marshal.ReadIntPtr(transform,31),1),name)!=IntPtr.Zero)return result;
            IntPtr value=create(vm,projection);
            // Root the new value on the native VM stack across dictionary growth.
            IntPtr stack=Marshal.ReadIntPtr(vm,27416);Marshal.WriteIntPtr(stack,value);Marshal.WriteIntPtr(vm,27416,IntPtr.Add(stack,8));
            try {
                Marshal.WriteInt32(value,39,1113325568); // 55 degrees, same as native constructor
                Marshal.WriteInt32(value,43,1132068864);Marshal.WriteInt32(value,47,1132068864);
                IntPtr owner=Marshal.ReadIntPtr(call[3]);
                if((Marshal.ReadByte(value,-1)&~Marshal.ReadByte(owner,-1))>=24)barrier(vm,IntPtr.Subtract(value,1),IntPtr.Subtract(owner,1));
                int slot=(ushort)Marshal.ReadInt16(Marshal.ReadIntPtr(Marshal.ReadIntPtr(value,7),88));
                Marshal.WriteIntPtr(value,slot-1,owner);
                // The low-level VM dictionary writer clones the instance's
                // prototype and performs the native GC write barriers itself.
                add(vm,Marshal.ReadIntPtr(call[1]),IntPtr.Add(name,3),Marshal.ReadIntPtr(stack));
            } finally {Marshal.WriteIntPtr(vm,27416,stack);}
            return result;
        }
    }
}
