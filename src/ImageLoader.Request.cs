using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace IggyViewer {
    internal unsafe sealed partial class ImageLoader {
        static readonly HashSet<string> BlockedDataHeaders=new HashSet<string>(("accept-charset|accept-encoding|accept-ranges|age|allow|allowed|authorization|charge-to|connect|connection|content-length|content-location|content-range|cookie|date|delete|etag|expect|get|head|host|if-modified-since|keep-alive|last-modified|location|max-forwards|options|origin|post|proxy-authenticate|proxy-authorization|proxy-connection|public|put|range|referer|request-range|retry-after|server|te|trace|trailer|transfer-encoding|upgrade|uri|user-agent|vary|via|warning|www-authenticate|x-flash-version").Split('|'),StringComparer.OrdinalIgnoreCase);
        static uint RequestUInt(BinaryReader reader) {return ((uint)reader.ReadByte()<<24)|((uint)reader.ReadByte()<<16)|((uint)reader.ReadByte()<<8)|reader.ReadByte();}
        static string RequestString(BinaryReader reader) {
            int length=(reader.ReadByte()<<8)|reader.ReadByte();byte[] bytes=reader.ReadBytes(length);
            if(bytes.Length!=length)throw new InvalidDataException("Truncated URLRequest metadata");
            try{return new UTF8Encoding(false,true).GetString(bytes);}catch(DecoderFallbackException){throw new InvalidDataException("Invalid URLRequest UTF-8");}
        }
        static void ValidateRequestValue(string value) {
            if(value.Length>8192)throw new InvalidDataException("HTTP header value is too long");
            foreach(char code in value)if((code<32&&code!='\t')||code==127||code>255)throw new InvalidDataException("Invalid HTTP header value");
        }
        // Snapshot on the AS3 thread; workers only see managed data, never VM atoms.
        DataJob ReadDataRequest(IntPtr value) {
            byte[] packet=native.CopyByteArray(value,DataLimit+131076);
            using(BinaryReader reader=new BinaryReader(new MemoryStream(packet))) {
                DataJob job=new DataJob {Url=RequestString(reader),Method=RequestString(reader),ContentType=RequestString(reader)};
                if(job.Url.Length==0||(job.Method!="GET"&&job.Method!="POST"))throw new InvalidDataException("Invalid URLRequest URL or method");
                ValidateRequestValue(job.ContentType);uint count=RequestUInt(reader);int headerSize=0;
                if(count>64)throw new InvalidDataException("Too many HTTP request headers");
                WebHeaderCollection validation=new WebHeaderCollection();
                for(uint i=0;i<count;i++) {
                    string name=RequestString(reader),text=RequestString(reader);
                    if(name.Length==0||name.Length>256||BlockedDataHeaders.Contains(name.Replace('_','-')))throw new InvalidDataException("Invalid or restricted HTTP header name");
                    foreach(char code in name)if(!((code>='0'&&code<='9')||(code>='A'&&code<='Z')||(code>='a'&&code<='z')||"!#$%&'*+-.^_`|~".IndexOf(code)>=0))throw new InvalidDataException("Invalid HTTP header name");
                    ValidateRequestValue(text);headerSize+=name.Length+text.Length;
                    if(headerSize>32768)throw new InvalidDataException("HTTP request headers exceed 32 KiB");
                    try{validation.Add(name,text);}catch(ArgumentException){throw new InvalidDataException("Invalid HTTP request header");}
                    job.Headers.Add(new KeyValuePair<string,string>(name,text));
                }
                if(reader.BaseStream.Position>131072)throw new InvalidDataException("URLRequest metadata exceeds 128 KiB");
                uint length=RequestUInt(reader);
                if(length>DataLimit||length!=reader.BaseStream.Length-reader.BaseStream.Position||(job.Method=="GET"&&length!=0))throw new InvalidDataException("Invalid URLRequest body size");
                job.Upload=reader.ReadBytes((int)length);return job;
            }
        }
        static bool SameDataOrigin(Uri first,Uri second) {return first.Scheme==second.Scheme&&first.Port==second.Port&&string.Equals(first.Host,second.Host,StringComparison.OrdinalIgnoreCase);}
    }
}
