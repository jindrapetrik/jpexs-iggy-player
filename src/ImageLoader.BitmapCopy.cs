using System;
using System.IO;
using System.Runtime.InteropServices;

namespace IggyViewer {
    internal unsafe sealed partial class ImageLoader {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int CopyArguments(IntPtr context,IntPtr format,IntPtr sourceType,ref IntPtr source,IntPtr rectType,ref IntPtr rect,IntPtr destType,ref IntPtr dest,IntPtr maskType,ref IntPtr mask,IntPtr alphaType,ref IntPtr alpha,ref int merge);
        static long CopyCoordinate(double value,bool round=false) {
            double integer=round?Math.Round(value,MidpointRounding.ToEven):Math.Truncate(value);
            return double.IsNaN(integer)||integer<int.MinValue||integer>int.MaxValue?int.MinValue:(long)integer;
        }
        static uint CopyBlend(uint source,uint target) {
            uint inv=256-(source>>24),result=0;
            for(int shift=0;shift<32;shift+=8)result|=Math.Min(255,((source>>shift)&255)+((((target>>shift)&255)*inv)>>8))<<shift;
            return result;
        }
        IntPtr CopyBitmapPixels(IntPtr context) {
            try {
                if(Disposed(Receiver(context)))return PixelError(context,"ArgumentError","Invalid BitmapData");
                IntPtr source=IntPtr.Zero,rect=IntPtr.Zero,dest=IntPtr.Zero,mask=IntPtr.Zero,alpha=IntPtr.Zero,format=Marshal.StringToHGlobalAnsi("!O!O!O?!O!Ob");int merge=0,status;
                try {status=native.BitmapFunction<CopyArguments>(0xb2d60,0xb8d40)(context,format,bitmapMetadata,ref source,rectangleMetadata,ref rect,pointMetadata,ref dest,bitmapMetadata,ref mask,pointMetadata,ref alpha,ref merge);}finally {Marshal.FreeHGlobal(format);}
                if(status!=0||source==IntPtr.Zero||source==nullValue||rect==IntPtr.Zero||rect==nullValue||dest==IntPtr.Zero||dest==nullValue)return PixelError(context,"TypeError","Expected source BitmapData, Rectangle and destination Point");
                bool hasMask=mask!=IntPtr.Zero&&mask!=nullValue;
                IntPtr receiver=Receiver(context);
                if(Disposed(receiver)||Disposed(source)||(hasMask&&Disposed(mask)))return PixelError(context,"ArgumentError","Invalid BitmapData");
                double rx=*(float*)IntPtr.Add(rect,39),ry=*(float*)IntPtr.Add(rect,43),rw=*(float*)IntPtr.Add(rect,47),rh=*(float*)IntPtr.Add(rect,51);
                if(double.IsNaN(rx+ry+rw+rh)||rw<=0||rh<=0)return IntPtr.Zero;
                long sx=CopyCoordinate(rx,true),sy=CopyCoordinate(ry,true),w=CopyCoordinate(rx+rw,true)-sx,h=CopyCoordinate(ry+rh,true)-sy;
                long dx=CopyCoordinate(*(float*)IntPtr.Add(dest,39)),dy=CopyCoordinate(*(float*)IntPtr.Add(dest,43)),ax=0,ay=0;
                if(alpha!=IntPtr.Zero&&alpha!=nullValue){ax=CopyCoordinate(*(float*)IntPtr.Add(alpha,39));ay=CopyCoordinate(*(float*)IntPtr.Add(alpha,43));}
                int targetWidth=Marshal.ReadInt32(receiver,47),targetHeight=Marshal.ReadInt32(receiver,51),sourceWidth=Marshal.ReadInt32(source,47),sourceHeight=Marshal.ReadInt32(source,51);
                bool opaque=Marshal.ReadByte(receiver,63)!=0;hasMask&=mask!=IntPtr.Zero&&mask!=nullValue&&Marshal.ReadByte(mask,63)==0;
                long left=Math.Max(0,Math.Max(-sx,-dx)),top=Math.Max(0,Math.Max(-sy,-dy));
                long right=Math.Min(w,Math.Min(sourceWidth-sx,targetWidth-dx)),bottom=Math.Min(h,Math.Min(sourceHeight-sy,targetHeight-dy));
                int maskWidth=hasMask?Marshal.ReadInt32(mask,47):0;
                if(hasMask){left=Math.Max(left,-ax);top=Math.Max(top,-ay);right=Math.Min(right,maskWidth-ax);bottom=Math.Min(bottom,Marshal.ReadInt32(mask,51)-ay);}
                if(right<=left||bottom<=top)return IntPtr.Zero;
                int width=(int)(right-left),height=(int)(bottom-top),sourceX=(int)(sx+left),sourceY=(int)(sy+top),destX=(int)(dx+left),destY=(int)(dy+top),alphaX=(int)(ax+left),alphaY=(int)(ay+top);
                Pixels targetPixels=PixelStorage(receiver);
                // Any promotion to private storage can move the VM's objects.
                // Re-read remaining inputs from their rooted argument slots.
                Pixels sourcePixels=PixelStorage(Marshal.ReadIntPtr(Marshal.ReadIntPtr(context,24),8));
                Pixels maskPixels=hasMask?PixelStorage(Marshal.ReadIntPtr(Marshal.ReadIntPtr(context,24),32)):null;
                uint[] snapshot=null;
                if(sourcePixels==targetPixels) {
                    snapshot=new uint[width*height];for(int row=0;row<height;row++)for(int col=0;col<width;col++)snapshot[row*width+col]=((uint*)sourcePixels.Raw)[(sourceY+row)*sourceWidth+sourceX+col];
                }
                for(int row=0;row<height;row++)for(int col=0;col<width;col++) {
                    uint color=snapshot==null?((uint*)sourcePixels.Raw)[(sourceY+row)*sourceWidth+sourceX+col]:snapshot[row*width+col];
                    if(hasMask) {
                        // Flash snapshots overlapping source pixels, but reads
                        // an aliased destination alpha mask as writes proceed.
                        uint a=((uint*)maskPixels.Raw)[(alphaY+row)*maskWidth+alphaX+col]>>24;
                        // Mask multiplication truncates at 8 bits. Full alpha
                        // is an identity, avoiding the 255/256 attenuation.
                        if(a!=255) {
                            uint masked=0;for(int shift=0;shift<32;shift+=8)masked|=((((color>>shift)&255)*a)>>8)<<shift;color=masked;
                        }
                    }
                    int at=(destY+row)*targetWidth+destX+col;
                    if(merge!=0||opaque)color=CopyBlend(color,((uint*)targetPixels.Raw)[at]);
                    ((uint*)targetPixels.Raw)[at]=opaque?color|0xff000000:color;
                }
                UpdatePixels(targetPixels,destX,destY,width,height);return IntPtr.Zero;
            } catch(InvalidDataException error){return PixelError(context,"ArgumentError",error.Message);}
        }
    }
}
