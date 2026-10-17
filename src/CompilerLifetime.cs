using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace IggyViewer {
    // The native linker leaves compiler metadata pointing into an arena that
    // its one-shot loader frees. Retain that arena for subsequent local loads.
    // This hook is installed only after the complete DLL hash is verified.
    internal sealed class CompilerLifetime : IDisposable {
        [DllImport("kernel32",SetLastError=true)] static extern bool VirtualProtect(IntPtr address,UIntPtr size,uint protection,out uint previous);
        [DllImport("kernel32")] static extern bool FlushInstructionCache(IntPtr process,IntPtr address,UIntPtr size);
        [DllImport("kernel32")] static extern IntPtr GetCurrentProcess();
        readonly IntPtr address;
        readonly byte[] original=new byte[12];
        readonly Native.VoidP callback,release;
        readonly List<IntPtr> retained=new List<IntPtr>();
        readonly int arenaOffset;
        bool disposed;
        internal CompilerLifetime(IntPtr module,bool modern) {
            arenaOffset=modern?27016:26992;
            address=IntPtr.Add(module,modern?0x3a690:0x3b5d0);
            release=(Native.VoidP)Marshal.GetDelegateForFunctionPointer(IntPtr.Add(module,modern?0x5a650:0x5e1c0),typeof(Native.VoidP));
            callback=Retain;Marshal.Copy(address,original,0,original.Length);
            if(original[0]==0x48&&original[1]==0xb8&&original[10]==0xff&&original[11]==0xe0)
                throw new InvalidOperationException("The native movie linker is already in use by another player instance.");
            byte[] jump=new byte[12];jump[0]=0x48;jump[1]=0xb8;
            Array.Copy(BitConverter.GetBytes(Marshal.GetFunctionPointerForDelegate(callback).ToInt64()),0,jump,2,8);jump[10]=0xff;jump[11]=0xe0;
            Write(jump);
        }
        void Write(byte[] bytes) {
            uint protection;
            if(!VirtualProtect(address,(UIntPtr)bytes.Length,0x40,out protection))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            try {Marshal.Copy(bytes,0,address,bytes.Length);FlushInstructionCache(GetCurrentProcess(),address,(UIntPtr)bytes.Length);}
            finally {uint ignored;VirtualProtect(address,(UIntPtr)bytes.Length,protection,out ignored);}
        }
        internal void Retain(IntPtr vm) {
            if(vm==IntPtr.Zero)return;
            IntPtr arena=Marshal.ReadIntPtr(vm,arenaOffset);
            if(arena!=IntPtr.Zero){retained.Add(arena);Marshal.WriteIntPtr(vm,arenaOffset,IntPtr.Zero);}
        }
        public void Dispose() {
            if(disposed)return;disposed=true;
            Write(original);foreach(IntPtr arena in retained)release(arena);retained.Clear();GC.KeepAlive(callback);
        }
    }
}
