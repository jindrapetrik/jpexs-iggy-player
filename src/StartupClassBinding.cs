using System;
using System.Runtime.InteropServices;

namespace IggyViewer {
    // Class-symbol binding can execute script initializers. Delay it until
    // the host callbacks are installed, without changing the movie's ABC.
    // Native verifies the complete DLL hash before this hook is used.
    internal sealed class StartupClassBinding : IDisposable {
        [DllImport("kernel32",SetLastError=true)] static extern bool VirtualProtect(IntPtr address,UIntPtr size,uint protection,out uint previous);
        [DllImport("kernel32")] static extern bool FlushInstructionCache(IntPtr process,IntPtr address,UIntPtr size);
        [DllImport("kernel32")] static extern IntPtr GetCurrentProcess();
        readonly IntPtr address;
        readonly byte original;
        bool disposed;
        internal StartupClassBinding(IntPtr module,bool modern) {
            address=IntPtr.Add(module,modern?0x42d0:0x1cd0);
            original=Marshal.ReadByte(address);
            if(original==0xc3)throw new InvalidOperationException("Native startup class binding is already deferred.");
            Write(0xc3);
        }
        void Write(byte value) {
            uint protection;
            if(!VirtualProtect(address,(UIntPtr)1,0x40,out protection))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            try {Marshal.WriteByte(address,value);FlushInstructionCache(GetCurrentProcess(),address,(UIntPtr)1);}
            finally {uint ignored;VirtualProtect(address,(UIntPtr)1,protection,out ignored);}
        }
        public void Dispose() {if(disposed)return;Write(original);disposed=true;}
    }
}
