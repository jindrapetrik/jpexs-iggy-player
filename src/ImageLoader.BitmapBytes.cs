using System;
using System.IO;
using System.Runtime.InteropServices;

namespace IggyViewer {
    internal unsafe sealed partial class ImageLoader {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int BitmapBytesArguments(IntPtr context,IntPtr format,IntPtr rectangleType,ref IntPtr rectangle,IntPtr bytesType,ref IntPtr bytes);
        static int BitmapEdge(double value,int maximum) {return (int)Math.Max(0,Math.Min(maximum,Math.Round(value,MidpointRounding.ToEven)));}
        IntPtr BitmapBytes(IntPtr context,bool read) {
            try {
                IntPtr receiver=Receiver(context);if(Disposed(receiver))return PixelError(context,"ArgumentError","Invalid BitmapData");
                IntPtr rectangle=IntPtr.Zero,bytes=IntPtr.Zero,format=Marshal.StringToHGlobalAnsi(read?"!O":"!O!O");int status;
                try {status=native.BitmapFunction<BitmapBytesArguments>(0xb2d60,0xb8d40)(context,format,rectangleMetadata,ref rectangle,native.BitmapByteArrayType,ref bytes);}finally {Marshal.FreeHGlobal(format);}
                if(status!=0||rectangle==IntPtr.Zero||rectangle==nullValue||(!read&&(bytes==IntPtr.Zero||bytes==nullValue)))return PixelError(context,"TypeError","Expected a Rectangle"+(read?"":" and a ByteArray"));
                receiver=Receiver(context);if(Disposed(receiver))return PixelError(context,"ArgumentError","Invalid BitmapData");
                double x=*(float*)IntPtr.Add(rectangle,39),y=*(float*)IntPtr.Add(rectangle,43),w=*(float*)IntPtr.Add(rectangle,47),h=*(float*)IntPtr.Add(rectangle,51);
                int stride=Marshal.ReadInt32(receiver,47),height=Marshal.ReadInt32(receiver,51),left=0,top=0,right=0,bottom=0;
                // Flash rounds the two endpoints separately, including ties
                // to even, then consumes only pixels inside the bitmap.
                if(!double.IsNaN(x+y+w+h)&&w>0&&h>0) {left=BitmapEdge(x,stride);top=BitmapEdge(y,height);right=BitmapEdge(x+w,stride);bottom=BitmapEdge(y+h,height);}
                int width=Math.Max(0,right-left),rows=Math.Max(0,bottom-top),count=width*rows;
                if(read) {
                    byte[] output=new byte[count*4];
                    if(count!=0) {
                        Pixels pixels=PixelStorage(receiver);int at=0;
                        for(int row=top;row<bottom;row++)for(int col=left;col<right;col++) {
                            uint argb=Unmultiply(Rgba(((uint*)pixels.Raw)[row*stride+col]));
                            output[at++]=(byte)(argb>>24);output[at++]=(byte)(argb>>16);output[at++]=(byte)(argb>>8);output[at++]=(byte)argb;
                        }
                    }
                    native.ReturnBitmapBytes(context,output);return IntPtr.Zero;
                }
                if(count==0)return IntPtr.Zero;
                Pixels target=PixelStorage(receiver);
                // Creating private pixels can run the moving collector.
                // Read the input atom again from its rooted argument slot.
                bytes=Marshal.ReadIntPtr(Marshal.ReadIntPtr(context,24),16);
                int available;uint position;bool little;IntPtr input=native.BitmapByteInput(bytes,out available,out position,out little);
                int copied=Math.Min(count,available/4);
                if(copied!=0) {
                    bool opaque=Marshal.ReadByte(Receiver(context),63)!=0;byte* source=(byte*)input;
                    for(int i=0;i<copied;i++) {
                        int at=i*4;uint color=little?(uint)(source[at]|source[at+1]<<8|source[at+2]<<16|source[at+3]<<24):(uint)(source[at]<<24|source[at+1]<<16|source[at+2]<<8|source[at+3]);
                        ((uint*)target.Raw)[(top+i/width)*stride+left+i%width]=Rgba(Premultiply(color,opaque));
                    }
                    native.BitmapBytePosition(bytes,position+(uint)(copied*4));UpdatePixels(target,left,top,width,rows);
                }
                // Preserve completed pixels and leave incomplete trailing
                // bytes unread, just as Flash Player does before Error #2030.
                return copied<count?PixelError(context,"Error","Error #2030: End of file was encountered."):IntPtr.Zero;
            } catch(InvalidDataException error){return PixelError(context,"ArgumentError",error.Message);}
        }
    }
}
