using System;
using System.IO;
using System.Runtime.InteropServices;

namespace IggyViewer {
    internal unsafe sealed partial class Native {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr GetBitmapTexture(IntPtr generic,IntPtr definition,IntPtr unused,out int alpha,out int width,out int height,float scaleX,float scaleY);
        internal T BitmapFunction<T>(int modern,int legacy) where T:class {return MovieFunction<T>(modern,legacy);}
        internal bool BitmapExceptionPending(IntPtr context) {return Marshal.ReadIntPtr(Marshal.ReadIntPtr(context),modernDll?10528:10504)!=IntPtr.Zero;}
        internal void BitmapNumber(IntPtr context,uint value) {Marshal.WriteIntPtr(Marshal.ReadIntPtr(context,8),ByteArrayNumericResult(Marshal.ReadIntPtr(context),value));}
        internal IntPtr BitmapByteArrayType {get{return byteArrayMetadata;}}
        internal void ReturnBitmapBytes(IntPtr context,byte[] bytes) {
            IntPtr value=MovieFunction<ByteArrayNew>(0x44b40,0x47890)(Marshal.ReadIntPtr(context),byteArrayMetadata);
            // Root the new object before registering settings, which can GC.
            Marshal.WriteIntPtr(Marshal.ReadIntPtr(context,8),value);
            if(byteArrayDefaultEncoding!=3)ByteArraySettings(value,true).Encoding=byteArrayDefaultEncoding;
            value=Marshal.ReadIntPtr(Marshal.ReadIntPtr(context,8));WriteByteArrayData(value,bytes);
        }
        internal IntPtr BitmapByteInput(IntPtr value,out int available,out uint position,out bool little) {
            if(!IsByteArray(value))throw new InvalidDataException("Expected a ByteArray");
            int length=ByteArrayLength(value);position=(uint)Marshal.ReadInt32(value,59);available=position>(uint)length?0:length-(int)position;little=ByteArrayLittleEndian(value);
            return available==0?IntPtr.Zero:IntPtr.Add(ByteArrayData(value),(int)position);
        }
        internal void BitmapBytePosition(IntPtr value,uint position) {Marshal.WriteInt32(value,59,unchecked((int)position));}
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
