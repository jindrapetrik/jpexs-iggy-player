using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace IggyViewer {
    internal unsafe sealed partial class Native {
        static readonly HashSet<string> ByteArrayMethods=new HashSet<string>(new string[]{
            "readByte","readUnsignedByte","writeByte","readBytes","writeBytes",
            "readBoolean","writeBoolean","readShort","readUnsignedShort","writeShort",
            "readInt","readUnsignedInt","writeInt","writeUnsignedInt","readFloat","readDouble","writeFloat","writeDouble",
            "readUTF","readUTFBytes","writeUTF","writeUTFBytes","readMultiByte","writeMultiByte","toString","compress","uncompress","readObject","writeObject"
        },StringComparer.Ordinal);
        sealed class ByteArrayState {internal IntPtr Reference;internal bool LittleEndian;internal int Encoding=3;}
        readonly List<ByteArrayState> byteArrayStates=new List<ByteArrayState>();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int ByteArrayConvert(IntPtr vm,IntPtr value,out double number);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr ByteArrayAtom(IntPtr vm,uint words);
        IntPtr ByteArrayNumericResult(IntPtr vm,double number) {
            // The DLL's normal number factory folds -0 into integer zero.
            if(number==0&&BitConverter.DoubleToInt64Bits(number)<0) {
                IntPtr raw=MovieFunction<ByteArrayAtom>(0x45650,0x4bb70)(vm,2);
                Marshal.WriteByte(raw,0,7);Marshal.WriteInt32(raw,4,2);Marshal.WriteInt64(raw,8,long.MinValue);return IntPtr.Add(raw,1);
            }
            return MovieFunction<ByteArrayNumber>(0x449a0,0x46be0)(vm,number);
        }
        ByteArrayState ByteArraySettings(IntPtr value,bool create=false) {
            foreach(ByteArrayState state in byteArrayStates)if(Marshal.ReadIntPtr(state.Reference)==value)return state;
            if(!create)return null;
            ByteArrayState result=new ByteArrayState {Reference=Activity.HostReference(value,1)};byteArrayStates.Add(result);return result;
        }
        internal void PruneByteArrays() {
            for(int i=byteArrayStates.Count-1;i>=0;i--)if(Marshal.ReadIntPtr(byteArrayStates[i].Reference).ToInt64()==7) {
                Activity.FreeHostReference(byteArrayStates[i].Reference);byteArrayStates.RemoveAt(i);
            }
        }
        void DetachByteArrays() {DetachAmfInterfaces();foreach(ByteArrayState state in byteArrayStates)Activity.FreeHostReference(state.Reference);byteArrayStates.Clear();}
        bool ByteArrayLittleEndian(IntPtr value) {ByteArrayState state=ByteArraySettings(value);return state!=null&&state.LittleEndian;}
        static IntPtr ByteArrayReceiver(IntPtr context) {return Marshal.ReadIntPtr(Marshal.ReadIntPtr(context,24));}
        static IntPtr ByteArrayParameter(IntPtr context,int index) {return Marshal.ReadInt32(context,16)<index?IntPtr.Zero:Marshal.ReadIntPtr(Marshal.ReadIntPtr(context,24),8*index);}
        static string ByteArrayText(IntPtr context,int index) {
            IntPtr value=ByteArrayParameter(context,index);
            if(value.ToInt64()<65536||(value.ToInt64()&3)!=1||Marshal.ReadByte(IntPtr.Subtract(value,1))!=8)
                throw new ByteArrayFailure("TypeError","Expected a non-null String.");
            IntPtr text=Marshal.ReadIntPtr(value,7);int length=Marshal.ReadInt32(text,7);
            if(length<0||length>ByteArrayLimit/2)throw new InvalidDataException("ByteArray string is too long.");
            return Marshal.PtrToStringUni(IntPtr.Add(text,11),length);
        }
        double ByteArrayNumeric(IntPtr context,int index=1) {
            double number;
            if(MovieFunction<ByteArrayConvert>(0xb2300,0xb80d0)(Marshal.ReadIntPtr(context),ByteArrayParameter(context,index),out number)==0)
                throw new ByteArrayFailure("TypeError","Cannot convert ByteArray argument to Number.");
            return number;
        }
        static void ByteArrayCheckRead(IntPtr value,uint length) {
            if(length==0)return;
            uint position=(uint)Marshal.ReadInt32(value,59),size=(uint)ByteArrayLength(value);
            if(position>size||length>size-position)throw new EndOfStreamException("End of ByteArray data.");
        }
        ulong ReadByteArrayNumber(IntPtr value,int width) {
            ByteArrayCheckRead(value,(uint)width);uint position=(uint)Marshal.ReadInt32(value,59);byte* data=(byte*)ByteArrayData(value)+position;
            ulong result=0;bool little=ByteArrayLittleEndian(value);
            for(int i=0;i<width;i++)result|=(ulong)data[i]<<(8*(little?i:width-1-i));
            Marshal.WriteInt32(value,59,(int)position+width);return result;
        }
        void WriteByteArrayData(IntPtr value,byte[] data) {
            uint position=(uint)Marshal.ReadInt32(value,59);
            if((ulong)position+(uint)data.Length>ByteArrayLimit)throw new InvalidDataException("ByteArray exceeds 256 MiB.");
            if(data.Length==0)return;
            int length=ByteArrayLength(value);
            if(position+data.Length>length)ResizeByteArray(value,(int)position+data.Length);
            else if(Marshal.ReadIntPtr(value,39)!=IntPtr.Zero)ResizeByteArray(value,length);
            Marshal.Copy(data,0,IntPtr.Add(ByteArrayData(value),(int)position),data.Length);Marshal.WriteInt32(value,59,(int)position+data.Length);
        }
        void WriteByteArrayNumber(IntPtr value,ulong number,int width) {
            byte[] data=new byte[width];bool little=ByteArrayLittleEndian(value);
            for(int i=0;i<width;i++)data[i]=(byte)(number>>(8*(little?i:width-1-i)));WriteByteArrayData(value,data);
        }
        static string ReadByteArrayText(IntPtr value,uint count,Encoding encoding,bool utf) {
            ByteArrayCheckRead(value,count);int position=Marshal.ReadInt32(value,59);byte[] bytes=new byte[count];
            if(count!=0)Marshal.Copy(IntPtr.Add(ByteArrayData(value),position),bytes,0,(int)count);
            Marshal.WriteInt32(value,59,position+(int)count);
            int start=utf&&count>=3&&bytes[0]==239&&bytes[1]==187&&bytes[2]==191?3:0,length=(int)count-start;
            // Flash's UTF reader consumes the requested range, skips a UTF-8
            // BOM, and terminates the returned string at an embedded NUL.
            if(utf){int zero=Array.IndexOf(bytes,(byte)0,start);if(zero>=0)length=zero-start;}
            return encoding.GetString(bytes,start,length);
        }
        static Encoding ByteArrayEncoding(string name) {
            if(name.Equals("shift-jis",StringComparison.OrdinalIgnoreCase))name="shift_jis";
            if(name.Equals("cn-gb",StringComparison.OrdinalIgnoreCase))name="gb2312";
            try {return Encoding.GetEncoding(name);}catch(ArgumentException){return Encoding.Default;}
        }
        IntPtr ByteArrayDataOperation(IntPtr context,string operation) {
            IntPtr value=ByteArrayReceiver(context),vm=Marshal.ReadIntPtr(context);double result=0;
            switch(operation) {
                case "endian":return ReturnMovieString(context,ByteArrayLittleEndian(value)?"littleEndian":"bigEndian");
                case "set_endian":
                    string endian=ByteArrayText(context,1);
                    if(endian!="bigEndian"&&endian!="littleEndian")throw new ByteArrayFailure("ArgumentError","Invalid endian value.");
                    ByteArraySettings(value,true).LittleEndian=endian=="littleEndian";return IntPtr.Zero;
                case "readBoolean":
                    bool flag=ReadByteArrayNumber(value,1)!=0;
                    Marshal.WriteIntPtr(Marshal.ReadIntPtr(context,8),Marshal.ReadIntPtr(module,(modernDll?0x110a90:0x101d00)+(flag?8:0)));return IntPtr.Zero;
                case "writeBoolean":WriteByteArrayNumber(value,Activity.True(ByteArrayParameter(context,1))?1UL:0UL,1);return IntPtr.Zero;
                case "readShort":result=unchecked((short)ReadByteArrayNumber(value,2));break;
                case "readUnsignedShort":result=ReadByteArrayNumber(value,2);break;
                case "readInt":result=unchecked((int)ReadByteArrayNumber(value,4));break;
                case "readUnsignedInt":result=ReadByteArrayNumber(value,4);break;
                case "writeShort":case "writeInt":case "writeUnsignedInt":
                    uint integer=ByteArrayArgument(context,1);WriteByteArrayNumber(ByteArrayReceiver(context),integer,operation=="writeShort"?2:4);return IntPtr.Zero;
                case "readFloat":uint floatBits=(uint)ReadByteArrayNumber(value,4);result=*(float*)&floatBits;break;
                case "readDouble":ulong doubleBits=ReadByteArrayNumber(value,8);result=*(double*)&doubleBits;break;
                case "writeFloat":case "writeDouble":
                    double number=ByteArrayNumeric(context);value=ByteArrayReceiver(context);
                    if(operation=="writeFloat"){float single=(float)number;WriteByteArrayNumber(value,*(uint*)&single,4);}
                    else WriteByteArrayNumber(value,*(ulong*)&number,8);return IntPtr.Zero;
                case "readUTF":case "readUTFBytes":case "readMultiByte":
                    uint count=operation=="readUTF"?(uint)ReadByteArrayNumber(value,2):ByteArrayArgument(context,1);
                    value=ByteArrayReceiver(context);
                    Encoding encoding=operation=="readMultiByte"?ByteArrayEncoding(ByteArrayText(context,2)):Encoding.UTF8;
                    return ReturnMovieString(context,ReadByteArrayText(value,count,encoding,operation!="readMultiByte"));
                case "writeUTF":case "writeUTFBytes":case "writeMultiByte":
                    string text=ByteArrayText(context,1);Encoding output=operation=="writeMultiByte"?ByteArrayEncoding(ByteArrayText(context,2)):Encoding.UTF8;
                    int encodedLength=output.GetByteCount(text);uint writePosition=(uint)Marshal.ReadInt32(value,59);
                    if(operation=="writeUTF"&&encodedLength>65535)throw new InvalidDataException("UTF-8 string exceeds 65535 bytes.");
                    if((ulong)writePosition+(uint)encodedLength+(operation=="writeUTF"?2UL:0UL)>ByteArrayLimit)throw new InvalidDataException("ByteArray exceeds 256 MiB.");
                    byte[] bytes=output.GetBytes(text);
                    if(operation=="writeUTF") {
                        WriteByteArrayNumber(value,(uint)bytes.Length,2);
                    }
                    WriteByteArrayData(value,bytes);return IntPtr.Zero;
                case "toString":
                    byte[] content=CopyByteArray(value);int skip=0;Encoding decoder=Encoding.UTF8;
                    if(content.Length>=2&&content[0]==255&&content[1]==254){decoder=Encoding.Unicode;skip=2;}
                    else if(content.Length>=2&&content[0]==254&&content[1]==255){decoder=Encoding.BigEndianUnicode;skip=2;}
                    else if(content.Length>=3&&content[0]==239&&content[1]==187&&content[2]==191)skip=3;
                    return ReturnMovieString(context,decoder.GetString(content,skip,content.Length-skip));
                case "compress":case "uncompress":
                    string algorithm=Marshal.ReadInt32(context,16)==0?"zlib":ByteArrayText(context,1);
                    if(algorithm!="zlib"&&algorithm!="deflate")throw new ByteArrayFailure("ArgumentError","Supported compression algorithms are zlib and deflate.");
                    byte[] transformed;
                    try {transformed=ByteArrayCompression.Transform(CopyByteArray(value),operation=="compress",algorithm=="zlib",ByteArrayLimit);}
                    catch(InvalidDataException ex){throw new ByteArrayFailure("Error",ex.Message);}
                    ResizeByteArray(value,transformed.Length);
                    if(transformed.Length!=0)Marshal.Copy(transformed,0,ByteArrayData(value),transformed.Length);
                    Marshal.WriteInt32(value,59,operation=="compress"?transformed.Length:0);return IntPtr.Zero;
                default:return ByteArrayAmfProperty(context,operation);
            }
            Marshal.WriteIntPtr(Marshal.ReadIntPtr(context,8),ByteArrayNumericResult(vm,result));return IntPtr.Zero;
        }
    }
}
