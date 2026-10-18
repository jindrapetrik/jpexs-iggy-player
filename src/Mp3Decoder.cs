using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace IggyViewer {
    // Iggy's MP3 plugin interface, backed by the Windows ACM decoder. The
    // native mixer retains ownership of timing, looping and SoundChannels.
    internal sealed class Mp3Decoder : IDisposable {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Install(IntPtr table);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void InstallDecoder(IntPtr callback);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr Open(IntPtr user,IntPtr read,int length);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Read(IntPtr user,IntPtr destination,int length,int offset);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void Close(IntPtr decoder);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Decode(IntPtr decoder,IntPtr destination,int capacity,IntPtr format);
        readonly Install install;
        readonly Open open;
        readonly Close close;
        readonly Decode decode;
        readonly Log log;
        readonly Dictionary<IntPtr,Session> sessions=new Dictionary<IntPtr,Session>();
        readonly object sync=new object();
        int opened,closed,failures,outputBytes;
        public int Opened {get{return Volatile.Read(ref opened);}}
        public int Closed {get{return Volatile.Read(ref closed);}}
        public int Failures {get{return Volatile.Read(ref failures);}}
        public int OutputBytes {get{return Volatile.Read(ref outputBytes);}}
        public int Active {get{lock(sync)return sessions.Count;}}
        sealed class Session {internal byte[] Pcm;internal int Rate,Channels,Position,Length;internal IntPtr User;internal Read Reader;}

        internal Mp3Decoder(Native native,Log log) {
            this.log=log;open=Create;close=Release;decode=Next;
            install=delegate(IntPtr table) {
                if(Marshal.ReadInt32(table)!=48)return 0;
                Marshal.WriteIntPtr(table,24,Marshal.GetFunctionPointerForDelegate(open));
                Marshal.WriteIntPtr(table,32,Marshal.GetFunctionPointerForDelegate(close));
                Marshal.WriteIntPtr(table,40,Marshal.GetFunctionPointerForDelegate(decode));return 1;
            };
            native.Bind<InstallDecoder>("IggyAudioInstallMP3DecoderExplicit")(Marshal.GetFunctionPointerForDelegate(install));
            log.Write("Audio: installed Windows ACM MP3 decoder; no separate MP3 DLL required.");
        }
        IntPtr Create(IntPtr user,IntPtr callback,int length) {
            try {
                if(length<6||length>32*1024*1024)throw new InvalidDataException("Invalid MP3 data size.");
                Read reader=(Read)Marshal.GetDelegateForFunctionPointer(callback,typeof(Read));
                byte[] bytes=new byte[length];GCHandle input=GCHandle.Alloc(bytes,GCHandleType.Pinned);
                try {
                    int at=0;
                    while(at<length) {int count=reader(user,IntPtr.Add(input.AddrOfPinnedObject(),at),length-at,at);if(count<=0||count>length-at)throw new InvalidDataException("Truncated native MP3 data.");at+=count;}
                } finally {input.Free();}
                int rate,channels;byte[] pcm=WindowsMp3.Decode(bytes,out rate,out channels);
                Session session=new Session {Pcm=pcm,Rate=rate,Channels=channels,User=user,Reader=reader,Length=length};
                GCHandle handle=GCHandle.Alloc(session);IntPtr token=GCHandle.ToIntPtr(handle);
                try {lock(sync)sessions.Add(token,session);}catch {handle.Free();throw;}
                Interlocked.Increment(ref opened);return token;
            } catch(Exception error){Fail(error);return IntPtr.Zero;}
        }
        void Fail(Exception error) {
            Interlocked.Increment(ref failures);
            try {log.Write("Audio: MP3 decoding failed: "+error.Message);}catch(IOException){}catch(UnauthorizedAccessException){}
        }
        int Next(IntPtr token,IntPtr destination,int capacity,IntPtr format) {
            try {
                lock(sync) {
                    Session session;if(!sessions.TryGetValue(token,out session))return 0;
                    Marshal.WriteInt32(format,session.Rate);Marshal.WriteInt32(format,4,session.Channels);
                    // Both verified DLLs rewind the encoded cursor at voice+112
                    // when a loop reaches its out-point, without reopening the
                    // decoder. Rewind the decoded PCM too, even if encoder
                    // padding meant its EOF had not yet been requested.
                    if(capacity>0&&Marshal.ReadInt32(session.User,112)==0) {
                        session.Position=0;
                        session.Reader(session.User,destination,1,session.Length-1);
                    }
                    if(session.Position==session.Pcm.Length){session.Position=0;return 0;}
                    int count=Math.Min(Math.Max(0,capacity),session.Pcm.Length-session.Position);
                    count-=count%(session.Channels*2);
                    Marshal.Copy(session.Pcm,session.Position,destination,count);session.Position+=count;
                    Interlocked.Add(ref outputBytes,count);return count;
                }
            } catch(Exception error){Fail(error);return 0;}
        }
        void Release(IntPtr token) {
            lock(sync)if(sessions.Remove(token)){GCHandle.FromIntPtr(token).Free();Interlocked.Increment(ref closed);}
        }
        public void Dispose() {
            // Called after IggyShutdown has joined the mixer and released voices.
            lock(sync){foreach(IntPtr token in new List<IntPtr>(sessions.Keys))Release(token);}
            GC.KeepAlive(install);GC.KeepAlive(open);GC.KeepAlive(close);GC.KeepAlive(decode);
        }
    }

    internal static unsafe class WindowsMp3 {
        static readonly int[] Rates={44100,48000,32000};
        static readonly int[] HighBitrates={0,32,40,48,56,64,80,96,112,128,160,192,224,256,320};
        static readonly int[] LowBitrates={0,8,16,24,32,40,48,56,64,80,96,112,128,144,160};
        static bool FrameHeader(byte[] bytes,int at,out int rate,out int channels,out int frame,out int samples,out int bitrate) {
            rate=channels=frame=samples=bitrate=0;
            if(at<0||at>bytes.Length-4)return false;
            uint h=(uint)bytes[at]<<24|(uint)bytes[at+1]<<16|(uint)bytes[at+2]<<8|bytes[at+3];
            int version=(int)(h>>19)&3,layer=(int)(h>>17)&3,index=(int)(h>>12)&15,r=(int)(h>>10)&3;
            if((h&0xffe00000)!=0xffe00000||version==1||layer!=1||index==0||index==15||r==3)return false;
            rate=Rates[r]/(version==3?1:version==2?2:4);bitrate=(version==3?HighBitrates:LowBitrates)[index]*1000;
            channels=((h>>6)&3)==3?1:2;frame=(version==3?144:72)*bitrate/rate+(int)(h>>9&1);samples=version==3?1152:576;
            return frame>=4&&frame<=bytes.Length-at;
        }
        [StructLayout(LayoutKind.Sequential,Pack=1)] struct Header {
            internal uint Size,Status;internal IntPtr User,Source;internal uint SourceLength,SourceUsed;
            internal IntPtr SourceUser,Destination;internal uint DestinationLength,DestinationUsed;
            internal IntPtr DestinationUser;internal fixed uint Reserved[15];
        }
        [DllImport("msacm32.dll")] static extern uint acmStreamOpen(out IntPtr stream,IntPtr driver,byte[] source,byte[] destination,IntPtr filter,IntPtr callback,IntPtr instance,uint flags);
        [DllImport("msacm32.dll")] static extern uint acmStreamClose(IntPtr stream,uint flags);
        [DllImport("msacm32.dll")] static extern uint acmStreamSize(IntPtr stream,uint input,out uint output,uint flags);
        [DllImport("msacm32.dll")] static extern uint acmStreamPrepareHeader(IntPtr stream,ref Header header,uint flags);
        [DllImport("msacm32.dll")] static extern uint acmStreamUnprepareHeader(IntPtr stream,ref Header header,uint flags);
        [DllImport("msacm32.dll")] static extern uint acmStreamConvert(IntPtr stream,ref Header header,uint flags);
        static void Check(uint result,string operation) {if(result!=0)throw new InvalidDataException(operation+" failed (ACM "+result+").");}
        static byte[] Format(int tag,int channels,int rate,int average,int bits,int extra) {
            using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)) {
                w.Write((ushort)tag);w.Write((ushort)channels);w.Write(rate);w.Write(average);
                w.Write((ushort)(bits==0?1:channels*bits/8));w.Write((ushort)bits);w.Write((ushort)extra);return m.ToArray();
            }
        }
        internal static byte[] DecodeExternal(byte[] bytes,out int rate,out int channels,Action<int> reserve) {
            int start=0;
            if(bytes.Length>=3&&bytes[0]==73&&bytes[1]==68&&bytes[2]==51) {
                if(bytes.Length<10)throw new InvalidDataException("Truncated ID3 header");
                for(int i=6;i<10;i++)if(bytes[i]>=128)throw new InvalidDataException("Invalid ID3 tag size");
                start=10+(bytes[6]<<21|bytes[7]<<14|bytes[8]<<7|bytes[9]);
                if(bytes[3]==4&&(bytes[5]&16)!=0)start=checked(start+10);
                if(start>bytes.Length)throw new InvalidDataException("Truncated ID3 tag");
            }
            byte[] encoded=new byte[checked(bytes.Length-start+2)];Buffer.BlockCopy(bytes,start,encoded,2,bytes.Length-start);
            return Decode(encoded,out rate,out channels,64*1024*1024,reserve);
        }
        internal static byte[] Decode(byte[] bytes,out int rate,out int channels,int maximum=256*1024*1024,Action<int> reserve=null) {
            rate=0;channels=0;int start=-1,frame=0,bitrate=0,samples;
            for(int at=2;at<bytes.Length-3;at++) {
                if(FrameHeader(bytes,at,out rate,out channels,out frame,out samples,out bitrate)){start=at;break;}
            }
            if(start<0)throw new InvalidDataException("No MPEG Layer III frame header.");
            // ACM's size estimate can be too small for variable-bitrate MP3.
            // Count complete frames instead of sizing by the first bitrate.
            int end=start;long frames=0;
            while(end<=bytes.Length-4) {
                int nextRate,nextChannels,nextFrame,nextSamples,nextBitrate;
                if(!FrameHeader(bytes,end,out nextRate,out nextChannels,out nextFrame,out nextSamples,out nextBitrate)||nextRate!=rate||nextChannels!=channels)break;
                end+=nextFrame;frames+=nextSamples;
            }
            long expected=frames*channels*2;
            if(expected>maximum-8192)throw new InvalidDataException("MP3 decoded audio exceeds its storage limit.");
            byte[] source;
            using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)) {
                w.Write(Format(0x55,channels,rate,bitrate/8,0,12));w.Write((ushort)1);w.Write(0u);
                w.Write((ushort)frame);w.Write((ushort)1);w.Write((ushort)0);source=m.ToArray();
            }
            IntPtr stream;Check(acmStreamOpen(out stream,IntPtr.Zero,source,Format(1,channels,rate,rate*channels*2,16,0),IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,4),"Opening MP3 decoder");
            try {
                uint capacity;Check(acmStreamSize(stream,(uint)(end-start),out capacity,0),"Sizing MP3 output");
                capacity=Math.Max(capacity,(uint)expected);
                if(capacity>maximum-8192)throw new InvalidDataException("MP3 decoded audio exceeds its storage limit.");
                if(reserve!=null)reserve((int)capacity+8192);
                byte[] output=new byte[capacity+8192];GCHandle input=new GCHandle(),destination=new GCHandle();
                try {
                    input=GCHandle.Alloc(bytes,GCHandleType.Pinned);destination=GCHandle.Alloc(output,GCHandleType.Pinned);
                    Header header=new Header {Size=(uint)Marshal.SizeOf(typeof(Header)),Source=IntPtr.Add(input.AddrOfPinnedObject(),start),SourceLength=(uint)(end-start),Destination=destination.AddrOfPinnedObject(),DestinationLength=(uint)output.Length};
                    Check(acmStreamPrepareHeader(stream,ref header,0),"Preparing MP3 conversion");
                    try {
                        Check(acmStreamConvert(stream,ref header,0x30),"Decoding MP3");
                        if(header.DestinationUsed==0||header.DestinationUsed>output.Length||header.SourceUsed<header.SourceLength)throw new InvalidDataException("Incomplete MP3 conversion.");
                        int seek=BitConverter.ToInt16(bytes,0),skip=Math.Max(0,seek)*channels*2,padding=Math.Max(0,-seek)*channels*2;
                        int count=Math.Max(0,(int)header.DestinationUsed-skip);count-=count%(channels*2);
                        byte[] pcm=new byte[count+padding];Buffer.BlockCopy(output,Math.Min(skip,(int)header.DestinationUsed),pcm,padding,count);return pcm;
                    } finally {acmStreamUnprepareHeader(stream,ref header,0);}
                } finally {if(input.IsAllocated)input.Free();if(destination.IsAllocated)destination.Free();}
            } finally {acmStreamClose(stream,0);}
        }
    }
}
