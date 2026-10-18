using System;
using System.Runtime.InteropServices;

namespace IggyViewer {
    internal unsafe sealed partial class Native {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr GetBitmapTexture(IntPtr generic,IntPtr definition,IntPtr unused,out int alpha,out int width,out int height,float scaleX,float scaleY);
        internal T BitmapFunction<T>(int modern,int legacy) where T:class {return MovieFunction<T>(modern,legacy);}
        internal bool BitmapExceptionPending(IntPtr context) {return Marshal.ReadIntPtr(Marshal.ReadIntPtr(context),modernDll?10528:10504)!=IntPtr.Zero;}
        internal void BitmapNumber(IntPtr context,uint value) {Marshal.WriteIntPtr(Marshal.ReadIntPtr(context,8),ByteArrayNumericResult(Marshal.ReadIntPtr(context),value));}
        internal IntPtr BitmapRectangle(IntPtr context,IntPtr metadata,int width,int height) {
            IntPtr value=MovieFunction<ByteArrayNew>(0x44b40,0x47890)(Marshal.ReadIntPtr(context),metadata);
            for(int i=0;i<4;i++)*(float*)IntPtr.Add(value,39+4*i)=i==2?width:i==3?height:0;
            Marshal.WriteIntPtr(Marshal.ReadIntPtr(context,8),value);return IntPtr.Zero;
        }
        internal IntPtr BitmapTexture(IntPtr definition) {
            int alpha,width,height;return MovieFunction<GetBitmapTexture>(0x5aa30,0x5fbe0)(Player,definition,IntPtr.Zero,out alpha,out width,out height,1,1);
        }
    }
}
