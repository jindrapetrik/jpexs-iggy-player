using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace IggyViewer {
    // RFC 1950/1951 framing and validation. DeflateStream handles the actual
    // compression/decompression; the parser rejects truncated streams before
    // .NET Framework's inflater can silently return a partial result.
    internal static class ByteArrayCompression {
        sealed class LimitedStream : MemoryStream {
            readonly int limit;
            internal LimitedStream(int limit){this.limit=limit;}
            public override void Write(byte[] data,int offset,int count) {if(Length+count>limit)throw Invalid("Compressed data exceeds the ByteArray limit.");base.Write(data,offset,count);}
            public override void WriteByte(byte value) {if(Length>=limit)throw Invalid("Compressed data exceeds the ByteArray limit.");base.WriteByte(value);}
        }
        static InvalidDataException Invalid(string message="Invalid compressed ByteArray data."){return new InvalidDataException(message);}
        internal static uint Adler32(byte[] bytes) {
            uint low=1,high=0;for(int i=0;i<bytes.Length;i++){low=(low+bytes[i])%65521;high=(high+low)%65521;}return (high<<16)|low;
        }
        internal static byte[] Transform(byte[] source,bool compress,bool zlib,int limit) {
            if(compress)using(LimitedStream output=new LimitedStream(limit)) {
                if(zlib){output.WriteByte(0x78);output.WriteByte(0x9c);}
                if(source.Length==0){output.WriteByte(3);output.WriteByte(0);}
                else using(DeflateStream encoder=new DeflateStream(output,CompressionMode.Compress,true))encoder.Write(source,0,source.Length);
                if(zlib){uint checksum=Adler32(source);for(int shift=24;shift>=0;shift-=8)output.WriteByte((byte)(checksum>>shift));}
                return output.ToArray();
            }
            int start=0;
            if(zlib) {
                if(source.Length<6||(source[0]&15)!=8||(source[0]>>4)>7||((source[0]<<8)|source[1])%31!=0||(source[1]&32)!=0)throw Invalid();
                start=2;
            }
            DeflateValidator validator=new DeflateValidator(source,start,limit,zlib?1<<(8+(source[0]>>4)):32768);int size=validator.Validate(),consumed=validator.Consumed;
            if(zlib&&source.Length-start-consumed<4)throw Invalid();
            byte[] result=new byte[size];
            using(MemoryStream input=new MemoryStream(source,start,consumed,false))using(DeflateStream decoder=new DeflateStream(input,CompressionMode.Decompress)) {
                int at=0;while(at<size){int count=decoder.Read(result,at,size-at);if(count==0)throw Invalid();at+=count;}
                if(decoder.ReadByte()!=-1)throw Invalid();
            }
            if(zlib) {
                int at=start+consumed;uint expected=((uint)source[at]<<24)|((uint)source[at+1]<<16)|((uint)source[at+2]<<8)|source[at+3];
                if(Adler32(result)!=expected)throw Invalid("Compressed ByteArray checksum mismatch.");
            }
            return result;
        }
        sealed class Huffman {
            readonly Dictionary<int,int> symbols=new Dictionary<int,int>();
            internal Huffman(int[] lengths,bool allowEmpty=false,bool codeLengths=false) {
                int[] counts=new int[16],next=new int[16];int used=0,max=0;
                foreach(int length in lengths){if(length<0||length>15)throw Invalid();if(length!=0){counts[length]++;used++;max=Math.Max(max,length);}}
                if(used==0){if(allowEmpty)return;throw Invalid();}
                int free=1,code=0;
                for(int i=1;i<=15;i++){free=2*free-counts[i];if(free<0)throw Invalid();code=(code+counts[i-1])<<1;next[i]=code;}
                if(free!=0&&(codeLengths||used!=1||max!=1))throw Invalid();
                for(int i=0;i<lengths.Length;i++)if(lengths[i]!=0)symbols.Add((1<<lengths[i])|next[lengths[i]]++,i);
            }
            internal int Read(DeflateValidator input) {
                int code=0,symbol;
                for(int length=1;length<=15;length++){code=(code<<1)|input.Bits(1);if(symbols.TryGetValue((1<<length)|code,out symbol))return symbol;}
                throw Invalid();
            }
        }
        sealed class DeflateValidator {
            static readonly int[] CodeLengthOrder={16,17,18,0,8,7,9,6,10,5,11,4,12,3,13,2,14,1,15};
            static readonly int[] Lengths={3,4,5,6,7,8,9,10,11,13,15,17,19,23,27,31,35,43,51,59,67,83,99,115,131,163,195,227,258};
            static readonly int[] LengthBits={0,0,0,0,0,0,0,0,1,1,1,1,2,2,2,2,3,3,3,3,4,4,4,4,5,5,5,5,0};
            static readonly int[] Distances={1,2,3,4,5,7,9,13,17,25,33,49,65,97,129,193,257,385,513,769,1025,1537,2049,3073,4097,6145,8193,12289,16385,24577};
            static readonly int[] DistanceBits={0,0,0,0,1,1,2,2,3,3,4,4,5,5,6,6,7,7,8,8,9,9,10,10,11,11,12,12,13,13};
            readonly byte[] source;readonly int start,limit,window;long bit,output;
            internal DeflateValidator(byte[] source,int start,int limit,int window){this.source=source;this.start=start;this.limit=limit;this.window=window;}
            internal int Consumed {get{return (int)((bit+7)/8);}}
            internal int Bits(int count) {
                if(bit+count>(source.LongLength-start)*8)throw Invalid("Truncated compressed ByteArray data.");
                int result=0;for(int i=0;i<count;i++,bit++)result|=((source[start+(int)(bit/8)]>>(int)(bit&7))&1)<<i;return result;
            }
            void Advance(int count){output+=count;if(output>limit)throw Invalid("Decompressed data exceeds the ByteArray limit.");}
            internal int Validate() {
                bool final;
                do {
                    final=Bits(1)!=0;int type=Bits(2);
                    if(type==0) {
                        bit=(bit+7)&~7L;int length=Bits(16),inverse=Bits(16);if((length^inverse)!=65535)throw Invalid();
                        if(bit+8L*length>(source.LongLength-start)*8)throw Invalid();bit+=8L*length;Advance(length);continue;
                    }
                    if(type==3)throw Invalid();
                    Huffman literals,distances;
                    if(type==1) {
                        int[] lengths=new int[288],distanceLengths=new int[32];
                        for(int i=0;i<288;i++)lengths[i]=i<144?8:i<256?9:i<280?7:8;
                        for(int i=0;i<32;i++)distanceLengths[i]=5;
                        literals=new Huffman(lengths);distances=new Huffman(distanceLengths);
                    } else {
                        int literalCount=Bits(5)+257,distanceCount=Bits(5)+1,codeCount=Bits(4)+4;if(literalCount>286)throw Invalid();
                        int[] codeLengths=new int[19];for(int i=0;i<codeCount;i++)codeLengths[CodeLengthOrder[i]]=Bits(3);
                        Huffman codes=new Huffman(codeLengths,codeLengths:true);int[] lengths=new int[literalCount+distanceCount];int at=0;
                        while(at<lengths.Length) {
                            int symbol=codes.Read(this);if(symbol<16){lengths[at++]=symbol;continue;}
                            if(symbol==16&&at==0)throw Invalid();
                            int repeat=symbol==16?Bits(2)+3:symbol==17?Bits(3)+3:Bits(7)+11;
                            if(repeat>lengths.Length-at)throw Invalid();int previous=symbol==16?lengths[at-1]:0;
                            while(repeat-->0)lengths[at++]=previous;
                        }
                        int[] literalLengths=new int[literalCount],distanceLengths=new int[distanceCount];
                        Array.Copy(lengths,0,literalLengths,0,literalCount);Array.Copy(lengths,literalCount,distanceLengths,0,distanceCount);
                        if(literalLengths[256]==0)throw Invalid();literals=new Huffman(literalLengths);distances=new Huffman(distanceLengths,true);
                    }
                    while(true) {
                        int symbol=literals.Read(this);if(symbol==256)break;
                        if(symbol<256){Advance(1);continue;}if(symbol>285)throw Invalid();
                        int index=symbol-257,length=Lengths[index]+Bits(LengthBits[index]),distanceSymbol=distances.Read(this);
                        if(distanceSymbol>=30)throw Invalid();int distance=Distances[distanceSymbol]+Bits(DistanceBits[distanceSymbol]);
                        if(distance>output||distance>window)throw Invalid();Advance(length);
                    }
                } while(!final);
                return (int)output;
            }
        }
    }
}
