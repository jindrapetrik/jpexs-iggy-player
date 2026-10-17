using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IggyViewer {
    internal sealed partial class RuntimeAbc {
        internal byte[] BootstrapDomain(string document) {
            // A balanced prefix initializes the main domain inside AS3 before
            // user code can read LoaderInfo. Relative branches stay unchanged;
            // absolute exception ranges move by the prefix length.
            int method;if(!constructors.TryGetValue(document,out method))return bytes;
            List<string> texts=new List<string>(strings);texts.Add("com.jpexs.iggy.runtime");texts.Add("HostDomainFactory");texts.Add("main");
            byte[] prefix;
            using(MemoryStream code=new MemoryStream())using(BinaryWriter w=new BinaryWriter(code)) {
                w.Write((byte)0x60);WriteVar(w,names.Count); // getlex HostDomainFactory
                w.Write((byte)0x4f);WriteVar(w,names.Count+1);WriteVar(w,0); // callpropvoid main, 0
                prefix=code.ToArray();
            }
            using(MemoryStream output=new MemoryStream())using(BinaryWriter w=new BinaryWriter(output)) {
                w.Write(bytes,0,stringsStart);WriteVar(w,texts.Count);
                for(int i=1;i<texts.Count;i++){byte[] text=Encoding.UTF8.GetBytes(texts[i]);WriteVar(w,text.Length);w.Write(text);}
                WriteVar(w,namespaces.Count+2);
                for(int i=1;i<namespaces.Count;i++){w.Write(namespaceKinds[i]);WriteVar(w,namespaces[i]);}
                w.Write((byte)0x16);WriteVar(w,strings.Count);w.Write((byte)0x16);WriteVar(w,0);
                w.Write(bytes,setsStart,setsEnd-setsStart);WriteVar(w,names.Count+2);
                for(int i=1;i<names.Count;i++)w.Write(names[i].Raw);
                w.Write((byte)7);WriteVar(w,namespaces.Count);WriteVar(w,strings.Count+1);
                w.Write((byte)7);WriteVar(w,namespaces.Count+1);WriteVar(w,strings.Count+2);
                w.Write(bytes,tail,bodiesStart-tail);cursor=bodiesStart;int bodies=Count();WriteVar(w,bodies);
                for(int i=0;i<bodies;i++) {
                    int id=Count(),stack=Count(),locals=Count(),initialScope=Count(),scope=Count(),length=Count(),code=cursor;
                    Skip(length);bool target=id==method;
                    WriteVar(w,id);WriteVar(w,target?Math.Max(1,stack):stack);WriteVar(w,locals);WriteVar(w,initialScope);WriteVar(w,scope);
                    WriteVar(w,length+(target?prefix.Length:0));if(target)w.Write(prefix);w.Write(bytes,code,length);
                    int exceptions=Count();WriteVar(w,exceptions);
                    for(int n=0;n<exceptions;n++) {
                        for(int field=0;field<5;field++){int value=Count();WriteVar(w,value+(target&&field<3?prefix.Length:0));}
                    }
                    int traits=cursor;Traits();w.Write(bytes,traits,cursor-traits);
                }
                if(cursor!=bytes.Length)throw new InvalidDataException("Unexpected ABC body trailer.");
                return output.ToArray();
            }
        }
    }
    internal static partial class Container {
        internal static string DocumentClass(byte[] bytes) {
            int entry=MainEntry(bytes);if(entry<0)return null;
            int main=BitConverter.ToInt32(bytes,entry+12),size=BitConverter.ToInt32(bytes,entry+4),count=BitConverter.ToInt32(bytes,main+176);
            long table=main+160L+BitConverter.ToInt64(bytes,main+160);
            if(count<0||count>65536||table<main||table>main+(long)size-count*24L)throw new InvalidDataException("Invalid document class table.");
            for(int i=0;i<count;i++) {
                int symbol=checked((int)table)+24*i;if(BitConverter.ToInt32(bytes,symbol+16)!=0)continue;
                long start=symbol+8L+BitConverter.ToInt64(bytes,symbol+8),end=start;
                if(start<main||start>main+(long)size-2)throw new InvalidDataException("Invalid document class name.");
                while(end<=main+(long)size-2&&BitConverter.ToUInt16(bytes,(int)end)!=0)end+=2;
                if(end>main+(long)size-2)throw new InvalidDataException("Unterminated document class name.");
                string name=Encoding.Unicode.GetString(bytes,(int)start,(int)(end-start));
                if(name.IndexOf("::",StringComparison.Ordinal)>=0)return name;
                int split=name.LastIndexOf('.');return split<0?"::"+name:name.Substring(0,split)+"::"+name.Substring(split+1);
            }
            return null;
        }
    }
}
