using System;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;
using System.Globalization;

namespace IggyViewer {
    internal unsafe sealed partial class Native {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int AmfInvoke(IntPtr vm,IntPtr result,IntPtr function,IntPtr args,int count,int parameter);
        int byteArrayDefaultEncoding=3;
        ByteArrayCall byteArrayConstructor;
        ByteArrayCall amfAnyVectorConstructor;
        IntPtr byteArrayAmfInterfaces,byteArrayOriginalInterfaces;
        byte byteArrayOriginalInterfaceCount;
        void InstallAmfInterfaces(IntPtr vm) {
            IntPtr input=MovieLookup(vm,"flash.utils::IDataInput"),output=MovieLookup(vm,"flash.utils::IDataOutput");
            if(input==IntPtr.Zero||output==IntPtr.Zero)return;
            byteArrayOriginalInterfaces=Marshal.ReadIntPtr(byteArrayMetadata,88);byteArrayOriginalInterfaceCount=Marshal.ReadByte(byteArrayMetadata,30);
            if(byteArrayOriginalInterfaceCount>253)throw new InvalidDataException("Too many ByteArray interfaces.");
            byteArrayAmfInterfaces=Marshal.AllocHGlobal(8*(byteArrayOriginalInterfaceCount+2));
            for(int i=0;i<byteArrayOriginalInterfaceCount;i++)Marshal.WriteIntPtr(byteArrayAmfInterfaces,8*i,Marshal.ReadIntPtr(byteArrayOriginalInterfaces,8*i));
            Marshal.WriteIntPtr(byteArrayAmfInterfaces,8*byteArrayOriginalInterfaceCount,input);Marshal.WriteIntPtr(byteArrayAmfInterfaces,8*(byteArrayOriginalInterfaceCount+1),output);
            Marshal.WriteIntPtr(byteArrayMetadata,88,byteArrayAmfInterfaces);Marshal.WriteByte(byteArrayMetadata,30,(byte)(byteArrayOriginalInterfaceCount+2));
        }
        void DetachAmfInterfaces() {if(byteArrayAmfInterfaces!=IntPtr.Zero){Marshal.WriteIntPtr(byteArrayMetadata,88,byteArrayOriginalInterfaces);Marshal.WriteByte(byteArrayMetadata,30,byteArrayOriginalInterfaceCount);}}
        internal IntPtr AmfClassOption(IntPtr metadata,string name) {
            IntPtr vm=Marshal.ReadIntPtr(Player,modernDll?10560:10480),key=MovieName(vm,"::"+name,true);
            return MovieFunction<MovieProperty>(0xaf4d0,0xb6b90)(vm,Marshal.ReadIntPtr(metadata,48),key);
        }
        internal IntPtr AmfBoolean(bool value) {return Marshal.ReadIntPtr(module,(modernDll?0x110a90:0x101d00)+(value?8:0));}
        internal string AmfString(IntPtr value,int maximum=16*1024*1024) {
            long atom=value.ToInt64();if((atom&1)==0)return (atom>>1).ToString(CultureInfo.InvariantCulture);
            if(value==IntPtr.Add(module,modernDll?0x103979:0xf3959)||value==IntPtr.Add(module,modernDll?0x103981:0xf3961))return "";
            if(value==AmfBoolean(true))return "true";
            if(value==AmfBoolean(false))return "false";
            if(atom>65536&&(atom&3)==1) {
                int kind=Marshal.ReadByte(IntPtr.Subtract(value,1));
                if(kind==7)return (*(double*)IntPtr.Add(value,7)).ToString("R",CultureInfo.InvariantCulture);
                if(kind==8) {IntPtr text=Marshal.ReadIntPtr(value,7);int length=Marshal.ReadInt32(text,7);if(length<0||length>maximum)throw new InvalidDataException("Text exceeds the size limit.");return Marshal.PtrToStringUni(IntPtr.Add(text,11),length);}
            }
            throw new InvalidDataException("Expected XML text.");
        }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr AmfVectorClassFactory(IntPtr vm,IntPtr type);
        internal void InstallAmfVector(IntPtr vector) {
            // The template already provides untyped array storage and methods,
            // but its constructor is a stub. Typed vectors keep using applytype.
            IntPtr setters=Marshal.ReadIntPtr(Marshal.ReadIntPtr(vector,72),80),function=Marshal.ReadIntPtr(setters,24);
            ByteArrayCall length=(ByteArrayCall)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(function,16),typeof(ByteArrayCall));
            amfAnyVectorConstructor=context=> {
                IntPtr result=IntPtr.Zero;
                if(Marshal.ReadInt32(context,16)>0)result=length(context);
                if(Marshal.ReadInt32(context,16)>1&&Activity.True(ByteArrayParameter(context,2))) {
                    IntPtr receiver=ByteArrayReceiver(context);Marshal.WriteByte(receiver,1,(byte)(Marshal.ReadByte(receiver,1)|2));
                }
                return result;
            };
            Marshal.WriteByte(vector,27,32);Marshal.WriteIntPtr(vector,56,Marshal.GetFunctionPointerForDelegate(amfAnyVectorConstructor));
        }
        internal IntPtr AmfVectorType(IntPtr value) {
            if(value.ToInt64()<65536||(value.ToInt64()&3)!=1)return IntPtr.Zero;
            int kind=Marshal.ReadByte(IntPtr.Subtract(value,1));if(kind!=32&&kind!=38)return IntPtr.Zero;
            IntPtr metadata=Marshal.ReadIntPtr(Marshal.ReadIntPtr(value,7),32),type=Marshal.ReadIntPtr(metadata,136);
            IntPtr vm=Marshal.ReadIntPtr(Player,modernDll?10560:10480);
            IntPtr template=Marshal.ReadIntPtr(vm,modernDll?10464:10440);
            if(kind==38)return metadata!=template&&Marshal.ReadIntPtr(metadata,64)==template?Marshal.ReadIntPtr(type,48):IntPtr.Zero;
            if(metadata!=template&&(Marshal.ReadByte(value,1)&1)==0)return IntPtr.Zero;
            return Marshal.ReadIntPtr(metadata==template?template:type,48);
        }
        internal IntPtr AmfVectorClass(IntPtr type) {
            if(type.ToInt64()<65536||(type.ToInt64()&3)!=1||Marshal.ReadByte(IntPtr.Subtract(type,1))!=38)throw new InvalidOperationException("Expected an AMF vector element class.");
            IntPtr vm=Marshal.ReadIntPtr(Player,modernDll?10560:10480),metadata=Marshal.ReadIntPtr(Marshal.ReadIntPtr(type,7),32),specialized=Marshal.ReadIntPtr(metadata,128);
            if(specialized==IntPtr.Zero)specialized=MovieFunction<AmfVectorClassFactory>(0x3aa00,0x3ef80)(vm,metadata);
            KeepMovieClass(specialized);return Marshal.ReadIntPtr(specialized,48);
        }
        internal string ReadAmfText(IntPtr value,uint length) {return ReadByteArrayText(value,length,Encoding.UTF8,false);}
        internal void SetSharedObjectBytes(IntPtr value,byte[] bytes) {
            if(!IsByteArray(value)||bytes.Length>16*1024*1024)throw new InvalidDataException("SharedObject AMF data exceeds 16 MiB.");
            ResizeByteArray(value,bytes.Length);if(bytes.Length!=0)Marshal.Copy(bytes,0,ByteArrayData(value),bytes.Length);Marshal.WriteInt32(value,59,0);
        }
        void InstallByteArrayConstructor() {
            ByteArrayCall original=(ByteArrayCall)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(byteArrayMetadata,56),typeof(ByteArrayCall));
            byteArrayDefaultEncoding=3;
            byteArrayConstructor=context=> {
                IntPtr result=original(context);
                if(byteArrayDefaultEncoding!=3)ByteArraySettings(ByteArrayReceiver(context),true).Encoding=byteArrayDefaultEncoding;
                return result;
            };
            Marshal.WriteIntPtr(byteArrayMetadata,56,Marshal.GetFunctionPointerForDelegate(byteArrayConstructor));
        }
        IntPtr InvokeAmf(IntPtr context,bool write) {
            IntPtr vm=Marshal.ReadIntPtr(context);
            int stackOffset=modernDll?27448:27416,count=write?2:1;
            IntPtr stack=Marshal.ReadIntPtr(vm,stackOffset);
            Marshal.WriteIntPtr(stack,IntPtr.Zero);Marshal.WriteIntPtr(stack,8,ByteArrayReceiver(context));
            if(write)Marshal.WriteIntPtr(stack,16,ByteArrayParameter(context,1));
            Marshal.WriteIntPtr(vm,stackOffset,IntPtr.Add(stack,8*(count+1)));
            try {
                // Resolve through the global script so its initializer establishes
                // lexical scope. Merely reading the Class metadata skips that step.
                // Receiver and arguments remain rooted on the VM's moving GC stack.
                IntPtr codecName=MovieName(vm,"com.jpexs.iggy.runtime::HostAmf",true);
                Marshal.WriteIntPtr(stack,MovieFunction<MovieGlobal>(0x79990,0x7ff90)(vm,codecName));
                Marshal.WriteIntPtr(stack,MovieFunction<MovieProperty>(0xaf4d0,0xb6b90)(vm,Marshal.ReadIntPtr(stack),codecName));
                IntPtr name=MovieName(vm,write?"::write":"::read",true);
                IntPtr function=MovieFunction<MovieProperty>(0xaf4d0,0xb6b90)(vm,Marshal.ReadIntPtr(stack),name);
                MovieFunction<AmfInvoke>(0x794c0,0x7fbd0)(vm,Marshal.ReadIntPtr(context,8),function,stack,count,-1);
                return Marshal.ReadIntPtr(vm,(modernDll?10528:10504))==IntPtr.Zero?IntPtr.Zero:Marshal.ReadIntPtr(vm,modernDll?10552:10528);
            } finally {Marshal.WriteIntPtr(vm,stackOffset,stack);}
        }
        IntPtr ByteArrayAmfProperty(IntPtr context,string operation) {
            IntPtr value=ByteArrayReceiver(context);int encoding;
            switch(operation) {
                case "readObject":return InvokeAmf(context,false);
                case "writeObject":return InvokeAmf(context,true);
                case "objectEncoding":ByteArrayState state=ByteArraySettings(value);encoding=state==null?3:state.Encoding;break;
                case "static_defaultObjectEncoding":encoding=byteArrayDefaultEncoding;break;
                case "set_objectEncoding":case "set_static_defaultObjectEncoding":
                    uint next=ByteArrayArgument(context,1);if(next!=0&&next!=3)throw new ByteArrayFailure("ArgumentError","objectEncoding must be AMF0 (0) or AMF3 (3).");
                    if(operation=="set_objectEncoding")ByteArraySettings(ByteArrayReceiver(context),true).Encoding=(int)next;else byteArrayDefaultEncoding=(int)next;
                    return IntPtr.Zero;
                default:throw new InvalidOperationException("Unknown AMF property.");
            }
            Marshal.WriteIntPtr(Marshal.ReadIntPtr(context,8),new IntPtr(2*encoding));return IntPtr.Zero;
        }
    }
}
