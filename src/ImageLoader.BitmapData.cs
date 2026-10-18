using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace IggyViewer {
    internal unsafe sealed partial class ImageLoader {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int PixelArguments(IntPtr context,IntPtr format,ref int x,ref int y,ref uint color);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int RectangleArguments(IntPtr context,IntPtr format,IntPtr metadata,ref IntPtr rectangle,ref uint color);
        readonly List<Builtin> pixelCallbacks=new List<Builtin>();
        PixelArguments pixelArguments;
        RectangleArguments rectangleArguments;
        IntPtr rectangleMetadata,bitmapMetadata,pointMetadata;
        void InstallPixelMethods(IntPtr metadata,IntPtr rectangle) {
            rectangleMetadata=rectangle;
            bitmapMetadata=metadata;
            pixelArguments=native.BitmapFunction<PixelArguments>(0xb2d60,0xb8d40);
            rectangleArguments=native.BitmapFunction<RectangleArguments>(0xb2d60,0xb8d40);
            IntPtr traits=Marshal.ReadIntPtr(metadata,72),table=Marshal.ReadIntPtr(traits,64);int count=(ushort)Marshal.ReadInt16(traits,22),installed=0;
            foreach(string operation in new string[]{"getPixel","getPixel32","setPixel","setPixel32","getPixels","setPixels","copyPixels","fillRect","dispose"}) {
                bool found=false;
                for(int i=0;i<count;i++) {
                    IntPtr function=IntPtr.Subtract(Marshal.ReadIntPtr(table,8*i),1);if(function.ToInt64()<65536||Marshal.ReadByte(function)!=16)continue;
                    IntPtr name=Marshal.ReadIntPtr(function,8);if(Marshal.ReadInt32(name,48)!=operation.Length||Marshal.PtrToStringUni(IntPtr.Add(name,52),operation.Length)!=operation)continue;
                    string method=operation;Builtin callback=context=>method=="copyPixels"?CopyBitmapPixels(context):method=="getPixels"||method=="setPixels"?BitmapBytes(context,method=="getPixels"):PixelOperation(context,method);pixelCallbacks.Add(callback);Marshal.WriteIntPtr(function,16,Marshal.GetFunctionPointerForDelegate(callback));found=true;installed++;break;
                }
                if(!found)throw new InvalidDataException("Missing BitmapData method: "+operation);
            }
            Builtin rect=context=> {
                IntPtr receiver=Receiver(context);if(Disposed(receiver))return PixelError(context,"ArgumentError","Invalid BitmapData");
                return native.BitmapRectangle(context,rectangleMetadata,Marshal.ReadInt32(receiver,47),Marshal.ReadInt32(receiver,51));
            };
            InstallGetter(metadata,"rect",rect);pixelCallbacks.Add(rect);
            log.Write("BitmapData: pixel/ByteArray access, copyPixels, fillRect, independent clones and mutable textures enabled ("+installed+" methods).");
        }
        static IntPtr Receiver(IntPtr context) {return Marshal.ReadIntPtr(Marshal.ReadIntPtr(context,24));}
        static uint Rgba(uint argb) {return (argb&0xff00ff00)|((argb&255)<<16)|((argb>>16)&255);}
        static uint Premultiply(uint color,bool opaque=false) {
            uint a=opaque?255:color>>24;return (a<<24)|((((color>>16)&255)*a+127)/255<<16)|((((color>>8)&255)*a+127)/255<<8)|(((color&255)*a+127)/255);
        }
        static uint Unmultiply(uint pixel) {
            uint a=pixel>>24;if(a==0)return 0;
            uint factor=BitmapUnmultiplyFactors[a];
            return (a<<24)|(Math.Min(255,(((pixel>>16)&255)*factor+32768)>>16)<<16)|(Math.Min(255,(((pixel>>8)&255)*factor+32768)>>16)<<8)|Math.Min(255,((pixel&255)*factor+32768)>>16);
        }
        bool Disposed(IntPtr value) {
            Pixels pixels;return Marshal.ReadInt32(value,47)<=0||Marshal.ReadInt32(value,51)<=0||(bitmaps.TryGetValue(Marshal.ReadIntPtr(value,39),out pixels)&&pixels.Raw==IntPtr.Zero);
        }
        Pixels OwnPixels(IntPtr receiver,uint[] rgba) {
            lock(sync) {
                int width=Marshal.ReadInt32(receiver,47),height=Marshal.ReadInt32(receiver,51),size=checked(width*height*4);
                if(width<1||height<1||width>3840||height>3840||rgba.Length!=width*height)throw new InvalidDataException("Invalid BitmapData dimensions");
                if(allocated+size>128L*1024*1024)throw new InvalidDataException("BitmapData storage exceeds 128 MiB");
                Pixels pixels=new Pixels {Definition=Marshal.AllocHGlobal(88),Size=size};
                try {
                    pixels.Raw=Marshal.AllocHGlobal(size);for(int i=0;i<88;i++)Marshal.WriteByte(pixels.Definition,i,0);
                    for(int i=0;i<rgba.Length;i++)((uint*)pixels.Raw)[i]=rgba[i];
                    Marshal.WriteByte(pixels.Definition,0,4);Marshal.WriteByte(pixels.Definition,1,128);
                    Marshal.WriteInt16(pixels.Definition,32,(short)width);Marshal.WriteInt16(pixels.Definition,34,(short)height);
                    Marshal.WriteByte(pixels.Definition,36,Marshal.ReadByte(receiver,63)==0?(byte)4:(byte)3);Marshal.WriteByte(pixels.Definition,39,2);
                    *(float*)IntPtr.Add(pixels.Definition,48)=1;*(float*)IntPtr.Add(pixels.Definition,52)=1;
                    Marshal.WriteIntPtr(pixels.Definition,64,pixels.Raw);Marshal.WriteInt32(pixels.Definition,80,size);
                    // The weak reference is established before publishing the new
                    // definition; it is also the receiver's moving-GC lookup.
                    IntPtr owner=reference(native.Player,receiver,1);pixels.Owners.Add(owner);Marshal.WriteIntPtr(Marshal.ReadIntPtr(owner),39,pixels.Definition);
                    bitmaps.Add(pixels.Definition,pixels);allocated+=size;return pixels;
                } catch {foreach(IntPtr owner in pixels.Owners)freeReference(native.Player,owner);if(pixels.Raw!=IntPtr.Zero)Marshal.FreeHGlobal(pixels.Raw);Marshal.FreeHGlobal(pixels.Definition);throw;}
            }
        }
        Pixels PixelStorage(IntPtr receiver) {
            Pixels pixels;IntPtr definition=Marshal.ReadIntPtr(receiver,39);
            if(bitmaps.TryGetValue(definition,out pixels))return pixels;
            int width=Marshal.ReadInt32(receiver,47),height=Marshal.ReadInt32(receiver,51);uint[] rgba=new uint[checked(width*height)];
            IntPtr buffer=Marshal.ReadIntPtr(receiver,55);
            if(definition!=IntPtr.Zero) {
                IntPtr texture=native.BitmapTexture(definition);
                if(!native.Renderer.ReadBitmapPixels(texture,definition,width,height,rgba))throw new InvalidDataException("BitmapData pixels are unavailable");
            } else if(buffer!=IntPtr.Zero) {
                bool opaque=Marshal.ReadByte(receiver,63)!=0;
                for(int i=0;i<rgba.Length;i++)rgba[i]=Rgba(Premultiply(((uint*)buffer)[i],opaque));
            } else throw new InvalidDataException("BitmapData pixels are unavailable");
            return OwnPixels(receiver,rgba);
        }
        IntPtr PixelError(IntPtr context,string kind,string message) {return native.RaiseByteArrayError(Marshal.ReadIntPtr(context),kind,message);}
        void UpdatePixels(Pixels pixels,int x,int y,int width,int height) {native.Renderer.UpdateBitmapPixels(Marshal.ReadIntPtr(pixels.Definition,40),pixels.Raw,Marshal.ReadInt16(pixels.Definition,32),x,y,width,height);}
        IntPtr PixelOperation(IntPtr context,string operation) {
            try {
                IntPtr receiver=Receiver(context);if(Disposed(receiver))return PixelError(context,"ArgumentError","Invalid BitmapData");
                if(operation=="dispose") {
                    lock(sync) {
                        Pixels disposed=PixelStorage(receiver);for(int i=0;i<disposed.Size/4;i++)((uint*)disposed.Raw)[i]=0;
                        Marshal.WriteByte(disposed.Definition,36,4);UpdatePixels(disposed,0,0,Marshal.ReadInt32(receiver,47),Marshal.ReadInt32(receiver,51));
                        Marshal.FreeHGlobal(disposed.Raw);disposed.Raw=IntPtr.Zero;Marshal.WriteIntPtr(disposed.Definition,64,IntPtr.Zero);allocated-=disposed.Size;
                        Marshal.WriteInt32(receiver,47,0);Marshal.WriteInt32(receiver,51,0);return IntPtr.Zero;
                    }
                }
                int x=0,y=0;uint color=0;bool read=operation=="getPixel"||operation=="getPixel32";
                int left=0,top=0,right=0,bottom=0;
                if(operation=="fillRect") {
                    IntPtr rectangle=IntPtr.Zero,format=Marshal.StringToHGlobalAnsi("!Ou");int status;
                    try{status=rectangleArguments(context,format,rectangleMetadata,ref rectangle,ref color);}finally{Marshal.FreeHGlobal(format);}
                    if(status!=0||rectangle==IntPtr.Zero||rectangle==nullValue)return PixelError(context,"TypeError","Expected a Rectangle and a color");
                    receiver=Receiver(context);if(Disposed(receiver))return PixelError(context,"ArgumentError","Invalid BitmapData");
                    double rx=*(float*)IntPtr.Add(rectangle,39),ry=*(float*)IntPtr.Add(rectangle,43),rw=*(float*)IntPtr.Add(rectangle,47),rh=*(float*)IntPtr.Add(rectangle,51);
                    if(double.IsNaN(rx+ry+rw+rh)||rw<=0||rh<=0)return IntPtr.Zero;
                    receiver=Receiver(context);
                    left=(int)Math.Max(0,Math.Min(Marshal.ReadInt32(receiver,47),Math.Floor(rx)));top=(int)Math.Max(0,Math.Min(Marshal.ReadInt32(receiver,51),Math.Floor(ry)));
                    right=(int)Math.Max(0,Math.Min(Marshal.ReadInt32(receiver,47),Math.Ceiling(rx+rw)));bottom=(int)Math.Max(0,Math.Min(Marshal.ReadInt32(receiver,51),Math.Ceiling(ry+rh)));
                    if(right<=left||bottom<=top)return IntPtr.Zero;
                } else {
                    IntPtr format=Marshal.StringToHGlobalAnsi(read?"dd":"ddu");int status;
                    try{status=pixelArguments(context,format,ref x,ref y,ref color);}finally{Marshal.FreeHGlobal(format);}
                    if(status!=0)return PixelError(context,"ArgumentError","Invalid pixel arguments");
                    receiver=Receiver(context);
                    if(Disposed(receiver))return PixelError(context,"ArgumentError","Invalid BitmapData");
                    if(x<0||y<0||x>=Marshal.ReadInt32(receiver,47)||y>=Marshal.ReadInt32(receiver,51)){if(read)native.BitmapNumber(context,0);return IntPtr.Zero;}
                    left=x;top=y;right=x+1;bottom=y+1;
                }
                // Argument conversions may run user code, dispose the receiver
                // or move it during GC. Inspect it only after all conversions.
                receiver=Receiver(context);if(Disposed(receiver))return PixelError(context,"ArgumentError","Invalid BitmapData");
                Pixels pixels=PixelStorage(receiver);int stride=Marshal.ReadInt32(Receiver(context),47);bool opaque=Marshal.ReadByte(Receiver(context),63)!=0;
                if(read) {uint argb=Unmultiply(Rgba(((uint*)pixels.Raw)[y*stride+x]));native.BitmapNumber(context,operation=="getPixel"?argb&0xffffff:argb);return IntPtr.Zero;}
                for(int row=top;row<bottom;row++)for(int col=left;col<right;col++) {
                    int at=row*stride+col;uint next=color;
                    if(operation=="setPixel")next=(color&0xffffff)|(((uint*)pixels.Raw)[at]&0xff000000);
                    ((uint*)pixels.Raw)[at]=Rgba(Premultiply(next,opaque));
                }
                UpdatePixels(pixels,left,top,right-left,bottom-top);return IntPtr.Zero;
            } catch(InvalidDataException error){return PixelError(context,"ArgumentError",error.Message);}
        }
    }
}
