using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace IggyViewer {
    internal unsafe sealed partial class Native {
        const int ByteArrayLimit=256*1024*1024;
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr ByteArrayCall(IntPtr context);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr ByteArrayNumber(IntPtr vm,double value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr ByteArrayNew(IntPtr vm,IntPtr metadata);
        readonly List<ByteArrayCall> byteArrayCallbacks=new List<ByteArrayCall>();
        readonly HashSet<IntPtr> byteArrayBuffers=new HashSet<IntPtr>();
        internal int ByteArrayBufferCount {get{return byteArrayBuffers.Count;}}
        IntPtr byteArrayMetadata; VoidPP byteArrayFinalizer;
        readonly Dictionary<string,IntPtr> byteArrayErrors=new Dictionary<string,IntPtr>();
        sealed class ByteArrayFailure : Exception {
            internal readonly string Kind;
            internal ByteArrayFailure(string kind,string message):base(message){Kind=kind;}
        }
        internal void InstallByteArrays(Dictionary<string,IntPtr> classes) {
            byteArrayMetadata=classes["ByteArray"];IntPtr vm=Marshal.ReadIntPtr(Player,modernDll?10560:10480);
            foreach(string name in new string[]{"Error","EOFError","RangeError","ArgumentError","TypeError"})byteArrayErrors[name]=classes[name];
            // Writable storage is owned by the host, not the DLL's arena.
            // VM finalizers receive the untagged object address. The DLL's
            // ByteArray finalizer incorrectly uses the tagged +47 offset.
            byteArrayFinalizer=(machine,raw)=> {IntPtr data=Marshal.ReadIntPtr(raw,48);if(data!=IntPtr.Zero){Marshal.WriteIntPtr(raw,48,IntPtr.Zero);FreeByteArrayBuffer(data);}};
            IntPtr originalFinalizer=Marshal.ReadIntPtr(byteArrayMetadata,16),replacement=Marshal.GetFunctionPointerForDelegate(byteArrayFinalizer);
            foreach(KeyValuePair<IntPtr,IntPtr> entry in MovieRegistry(vm))if(Marshal.ReadIntPtr(entry.Value,16)==originalFinalizer)Marshal.WriteIntPtr(entry.Value,16,replacement);
            Marshal.WriteIntPtr(byteArrayMetadata,16,replacement);
            InstallByteArrayConstructor();InstallByteArrayMethods(Marshal.ReadIntPtr(byteArrayMetadata,72),false);
            InstallByteArrayMethods(Marshal.ReadIntPtr(byteArrayMetadata,80),true);
        }
        void InstallByteArrayMethods(IntPtr traits,bool statics) {
            foreach(int table in new int[]{64,72,80}) {
                int count=(ushort)Marshal.ReadInt16(traits,table==64?22:table==72?26:24);IntPtr entries=Marshal.ReadIntPtr(traits,table);
                if(count>256)throw new InvalidDataException("Invalid native ByteArray method table.");
                for(int i=0;i<count;i++) {
                    IntPtr function=Marshal.ReadIntPtr(entries,8*i);if(table==64)function=IntPtr.Subtract(function,1);
                    if(function.ToInt64()<65536||Marshal.ReadByte(function)!=16)continue;
                    IntPtr text=Marshal.ReadIntPtr(function,8);string name=Marshal.PtrToStringUni(IntPtr.Add(text,52),Marshal.ReadInt32(text,48));
                    if(statics&&name!="defaultObjectEncoding")continue;
                    bool property=name=="length"||name=="position"||name=="bytesAvailable"||name=="endian"||name=="objectEncoding"||statics;
                    if(table==64&&!ByteArrayMethods.Contains(name)||table!=64&&!property)continue;
                    string operation=(table==80?"set_":"")+(statics?"static_":"")+name;
                    ByteArrayCall callback=context=>ByteArrayOperation(context,operation);byteArrayCallbacks.Add(callback);
                    Marshal.WriteIntPtr(function,16,Marshal.GetFunctionPointerForDelegate(callback));
                }
            }
        }
        void FreeByteArrayBuffer(IntPtr data) {if(byteArrayBuffers.Remove(data))Marshal.FreeHGlobal(data); }
        bool IsByteArray(IntPtr value) {
            if(value.ToInt64()<65536||(value.ToInt64()&3)!=1||Marshal.ReadByte(IntPtr.Subtract(value,1))<32)return false;
            IntPtr metadata=Marshal.ReadIntPtr(Marshal.ReadIntPtr(value,7),32);
            for(int depth=0;metadata!=IntPtr.Zero&&depth<256;depth++,metadata=Marshal.ReadIntPtr(metadata,64))if(metadata==byteArrayMetadata)return true;
            return false;
        }
        static int ByteArrayLength(IntPtr value) {
            IntPtr resource=Marshal.ReadIntPtr(value,39);int length=resource==IntPtr.Zero?Marshal.ReadInt32(value,55):Marshal.ReadInt32(resource,32);
            if(length<0||length>ByteArrayLimit)throw new InvalidDataException("ByteArray exceeds 256 MiB.");return (int)length;
        }
        static IntPtr ByteArrayData(IntPtr value) {IntPtr resource=Marshal.ReadIntPtr(value,39);return resource==IntPtr.Zero?Marshal.ReadIntPtr(value,47):Marshal.ReadIntPtr(resource,40);}
        internal byte[] CopyByteArray(IntPtr value,int maximum=ByteArrayLimit) {
            if(!IsByteArray(value))throw new InvalidDataException("Expected a native ByteArray.");
            int length=ByteArrayLength(value);if(length>maximum)throw new InvalidDataException("Pending Loader byte storage exceeds 256 MiB.");
            byte[] data=new byte[length];IntPtr source=ByteArrayData(value);
            if(data.Length!=0){if(source==IntPtr.Zero)throw new InvalidDataException("Missing ByteArray storage.");Marshal.Copy(source,data,0,data.Length);}return data;
        }
        static int ByteArrayCapacity(int length) {int capacity=256;while(capacity<length)capacity*=2;return capacity;}
        void ResizeByteArray(IntPtr value,int length) {
            int previous=ByteArrayLength(value);IntPtr old=Marshal.ReadIntPtr(value,47),data=ByteArrayData(value);
            if(Marshal.ReadIntPtr(value,39)!=IntPtr.Zero||old==IntPtr.Zero||length>ByteArrayCapacity(previous)) {
                int capacity=ByteArrayCapacity(length);IntPtr buffer=Marshal.AllocHGlobal(capacity);byteArrayBuffers.Add(buffer);
                if(buffer==IntPtr.Zero)throw new OutOfMemoryException();
                byte* target=(byte*)buffer;for(int i=0;i<capacity;i++)target[i]=i<Math.Min(previous,length)?((byte*)data)[i]:(byte)0;
                Marshal.WriteIntPtr(value,39,IntPtr.Zero);Marshal.WriteIntPtr(value,47,buffer);
                if(old!=IntPtr.Zero)FreeByteArrayBuffer(old);
            } else if(length!=previous) {
                byte* target=(byte*)old;for(int i=Math.Min(previous,length);i<Math.Max(previous,length);i++)target[i]=0;
            }
            Marshal.WriteInt32(value,55,length);
            if((uint)Marshal.ReadInt32(value,59)>(uint)length)Marshal.WriteInt32(value,59,length);
        }
        void ReleaseByteArrays() {
            foreach(IntPtr buffer in byteArrayBuffers)Marshal.FreeHGlobal(buffer);
            byteArrayBuffers.Clear();byteArrayCallbacks.Clear();byteArrayErrors.Clear();amfSchemas.Clear();byteArrayMetadata=IntPtr.Zero;byteArrayFinalizer=null;byteArrayConstructor=null;
        }
        uint ByteArrayArgument(IntPtr context,int index) {
            if(Marshal.ReadInt32(context,16)<index)return 0;
            double number=ByteArrayNumeric(context,index);if(double.IsNaN(number)||double.IsInfinity(number))return 0;
            number=Math.Truncate(number)%4294967296.0;if(number<0)number+=4294967296.0;return (uint)number;
        }
        IntPtr ByteArrayOperation(IntPtr context,string operation) {
            IntPtr vm=Marshal.ReadIntPtr(context),args=Marshal.ReadIntPtr(context,24),value=Marshal.ReadIntPtr(args);
            try {
                int required=operation=="readMultiByte"||operation=="writeMultiByte"?2:operation.StartsWith("write",StringComparison.Ordinal)||operation.StartsWith("set_",StringComparison.Ordinal)||operation=="readBytes"||operation=="readUTFBytes"?1:0;
                if(Marshal.ReadInt32(context,16)<required)throw new ByteArrayFailure("ArgumentError","Missing ByteArray argument.");
                uint argument=operation=="set_position"||operation=="set_length"||operation=="writeByte"?ByteArrayArgument(context,1):0;
                value=ByteArrayReceiver(context);
                bool statics=operation.IndexOf("static_",StringComparison.Ordinal)>=0;
                int length=statics?0:ByteArrayLength(value);uint position=statics?0:(uint)Marshal.ReadInt32(value,59);double result=0;
                switch(operation) {
                    case "length":result=length;break;
                    case "position":result=position;break;
                    case "bytesAvailable":result=position<(uint)length?length-position:0;break;
                    case "set_position":Marshal.WriteInt32(value,59,unchecked((int)argument));return IntPtr.Zero;
                    case "set_length":if(argument>ByteArrayLimit)throw new InvalidDataException();ResizeByteArray(value,(int)argument);return IntPtr.Zero;
                    case "readByte":case "readUnsignedByte":
                        if(position>=(uint)length)throw new EndOfStreamException();
                        byte next=Marshal.ReadByte(ByteArrayData(value),(int)position);result=operation=="readByte"?(int)(sbyte)next:next;
                        Marshal.WriteInt32(value,59,(int)position+1);break;
                    case "writeByte":
                        if(position>=ByteArrayLimit)throw new InvalidDataException();
                        if(position>=(uint)length)ResizeByteArray(value,(int)position+1);
                        else if(Marshal.ReadIntPtr(value,39)!=IntPtr.Zero)ResizeByteArray(value,length);
                        Marshal.WriteByte(ByteArrayData(value),(int)position,(byte)argument);Marshal.WriteInt32(value,59,(int)position+1);return IntPtr.Zero;
                    case "readBytes":case "writeBytes":
                        uint offset=ByteArrayArgument(context,2),count=ByteArrayArgument(context,3);bool read=operation=="readBytes";
                        value=ByteArrayReceiver(context);IntPtr other=ByteArrayParameter(context,1);position=(uint)Marshal.ReadInt32(value,59);
                        if(!IsByteArray(other))throw new ByteArrayFailure("TypeError","Expected a ByteArray.");
                        byte[] source=CopyByteArray(read?value:other);uint start=read?position:offset;
                        if(read&&start>(uint)source.Length&&count!=0)throw new EndOfStreamException();start=Math.Min(start,(uint)source.Length);
                        if(count==0)count=(uint)source.Length-start;
                        uint destination=read?offset:position;
                        if(count>(uint)source.Length-start){if(read)throw new EndOfStreamException();throw new InvalidDataException();}
                        if((ulong)destination+count>ByteArrayLimit)throw new InvalidDataException();
                        IntPtr target=read?other:value;uint saved=(uint)Marshal.ReadInt32(target,59);
                        if(read&&destination+count>(uint)ByteArrayLength(target))ResizeByteArray(target,(int)(destination+count));
                        if(count!=0) {
                            if(destination+count>(uint)ByteArrayLength(target))ResizeByteArray(target,(int)(destination+count));
                            else if(Marshal.ReadIntPtr(target,39)!=IntPtr.Zero)ResizeByteArray(target,ByteArrayLength(target));
                            Marshal.Copy(source,(int)start,IntPtr.Add(ByteArrayData(target),(int)destination),(int)count);
                        }
                        Marshal.WriteInt32(target,59,read?unchecked((int)saved):(int)(position+count));
                        if(read)Marshal.WriteInt32(value,59,(int)(position+count));return IntPtr.Zero;
                    default:return ByteArrayDataOperation(context,operation);
                }
                Marshal.WriteIntPtr(Marshal.ReadIntPtr(context,8),MovieFunction<ByteArrayNumber>(0x449a0,0x46be0)(vm,result));return IntPtr.Zero;
            } catch(Exception ex) {
                ByteArrayFailure failure=ex as ByteArrayFailure;
                string kind=failure!=null?failure.Kind:ex is EndOfStreamException?"EOFError":ex is InvalidDataException||ex is OutOfMemoryException?"RangeError":null;
                if(kind==null)throw;
                return RaiseByteArrayError(vm,kind,ex.Message);
            }
        }
        internal IntPtr RaiseByteArrayError(IntPtr vm,string kind,string message) {
            int shift=modernDll?0:24,exception=10528-shift;
            IntPtr value=MovieFunction<ByteArrayNew>(0x44b40,0x47890)(vm,byteArrayErrors[kind]);
            // Root the exception before allocating its message: native strings
            // may trigger the moving collector, including during an AS3 call.
            Marshal.WriteIntPtr(vm,exception,value);
            IntPtr text=Marshal.StringToHGlobalUni(message);
            try {
                IntPtr nativeText=MovieFunction<MovieString>(0x452a0,0x47140)(vm,text,message.Length);
                Marshal.WriteIntPtr(Marshal.ReadIntPtr(vm,exception),39,nativeText);
            } finally {Marshal.FreeHGlobal(text);}
            Marshal.WriteIntPtr(vm,10560-shift,Marshal.ReadIntPtr(vm,10576-shift));
            IntPtr continuation=Marshal.ReadIntPtr(vm,10584-shift);
            Marshal.WriteIntPtr(vm,10552-shift,continuation);Marshal.WriteIntPtr(vm,10544-shift,IntPtr.Zero);
            Marshal.WriteIntPtr(vm,10576-shift,IntPtr.Zero);Marshal.WriteIntPtr(vm,10584-shift,IntPtr.Zero);return continuation;
        }
    }
}
