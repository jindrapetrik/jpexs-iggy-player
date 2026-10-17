using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace IggyViewer {
    // Append constant-pool entries and change selected QNames. Existing pool
    // indices, instructions, branches, string literals and scripts stay intact.
    internal sealed class RuntimeAbc {
        readonly byte[] bytes;
        readonly List<string> strings=new List<string>{""};
        readonly List<int> namespaces=new List<int>{0};
        readonly List<byte> namespaceKinds=new List<byte>{0};
        sealed class Name {public int Kind,Ns,Text;public byte[] Raw;}
        readonly List<Name> names=new List<Name>{null};
        readonly int stringsStart,setsStart,setsEnd,tail;
        int cursor;
        public readonly HashSet<string> References=new HashSet<string>(StringComparer.Ordinal);
        public readonly HashSet<string> Definitions=new HashSet<string>(StringComparer.Ordinal);
        public readonly HashSet<string> Classes=new HashSet<string>(StringComparer.Ordinal);
        int Byte(){if(cursor>=bytes.Length)throw new InvalidDataException("Truncated runtime ABC.");return bytes[cursor++];}
        uint Var(){uint v=0;for(int i=0;i<5;i++){int b=Byte();v|=(uint)(b&127)<<(i*7);if((b&128)==0)return v;}throw new InvalidDataException("Invalid ABC integer.");}
        int Count(){uint n=Var();if(n>1000000)throw new InvalidDataException("ABC count limit exceeded.");return (int)n;}
        void Skip(int n){if(n<0||n>bytes.Length-cursor)throw new InvalidDataException("Truncated ABC data.");cursor+=n;}
        string Qualified(int index) {
            if(index<=0||index>=names.Count)return null;
            Name n=names[index];if(n.Kind!=7&&n.Kind!=13)return null;
            if(n.Ns<=0||n.Ns>=namespaces.Count||n.Text<0||n.Text>=strings.Count)throw new InvalidDataException("Invalid ABC QName.");
            if(namespaceKinds[n.Ns]!=0x16&&namespaceKinds[n.Ns]!=0x17)return null;
            return strings[namespaces[n.Ns]]+"::"+strings[n.Text];
        }
        void Traits(bool global=false) {
            for(int i=Count();i>0;i--) {
                int name=Count(),kind=Byte();
                if(global){string qualified=Qualified(name);if(qualified!=null)Definitions.Add(qualified);}
                switch(kind&15) {
                    case 0:case 6:Var();Var();if(Var()!=0)Byte();break;
                    case 1:case 2:case 3:case 4:case 5:Var();Var();break;
                    default:throw new InvalidDataException("Unknown ABC trait.");
                }
                if((kind&64)!=0)for(int n=Count();n>0;n--)Var();
            }
        }
        public RuntimeAbc(byte[] data) {
            bytes=data;Skip(4);
            for(int pool=0;pool<2;pool++)for(int n=Count()-1;n>0;n--)Var();
            Skip(Math.Max(0,Count()-1)*8);stringsStart=cursor;
            for(int n=Count()-1;n>0;n--){int size=Count();if(size>bytes.Length-cursor)throw new InvalidDataException("Truncated ABC string.");strings.Add(new UTF8Encoding(false,true).GetString(bytes,cursor,size));Skip(size);}
            for(int n=Count()-1;n>0;n--){namespaceKinds.Add((byte)Byte());int text=Count();if(text>=strings.Count)throw new InvalidDataException("Invalid ABC namespace.");namespaces.Add(text);}
            setsStart=cursor;for(int n=Count()-1;n>0;n--)for(int m=Count();m>0;m--)Var();setsEnd=cursor;
            for(int n=Count()-1;n>0;n--) {
                int start=cursor;Name name=new Name {Kind=Byte()};
                switch(name.Kind) {
                    case 7:case 13:name.Ns=Count();name.Text=Count();break;
                    case 15:case 16:case 27:case 28:Var();break;
                    case 17:case 18:break;
                    case 9:case 14:Var();Var();break;
                    case 29:Var();for(int k=Count();k>0;k--)Var();break;
                    default:throw new InvalidDataException("Unknown ABC multiname.");
                }
                name.Raw=new byte[cursor-start];Buffer.BlockCopy(bytes,start,name.Raw,0,name.Raw.Length);names.Add(name);
            }
            tail=cursor;
            for(int i=1;i<names.Count;i++){string q=Qualified(i);if(q!=null)References.Add(q);}
            for(int n=Count();n>0;n--) {
                int parameters=Count();Var();for(int i=0;i<parameters;i++)Var();Var();int flags=Byte();
                if((flags&8)!=0)for(int i=Count();i>0;i--){Var();Byte();}
                if((flags&128)!=0)for(int i=0;i<parameters;i++)Var();
            }
            for(int n=Count();n>0;n--){Var();int pairs=Count();for(int i=0;i<pairs*2;i++)Var();}
            int classes=Count();
            for(int n=classes;n>0;n--) {
                string q=Qualified(Count());if(q!=null){Definitions.Add(q);Classes.Add(q);}Var();int flags=Byte();if((flags&8)!=0)Var();
                for(int i=Count();i>0;i--)Var();Var();Traits();
            }
            for(int n=classes;n>0;n--){Var();Traits();}
            for(int n=Count();n>0;n--){Var();Traits(true);}
        }
        static void WriteVar(BinaryWriter w,int n){uint value=(uint)n;while(value>=128){w.Write((byte)(value|128));value>>=7;}w.Write((byte)value);}
        public byte[] Redirect(bool loader,bool fileList) {
            if(!loader&&!fileList)return bytes;
            Dictionary<int,string> changes=new Dictionary<int,string>();
            for(int i=1;i<names.Count;i++) {
                string q=Qualified(i);
                if(loader&&q=="flash.display::Loader")changes.Add(i,"HostLoader");
                if(loader&&q=="flash.net::URLRequest")changes.Add(i,"HostURLRequest");
                if(loader&&q=="flash.events::IOErrorEvent")changes.Add(i,"HostIOErrorEvent");
                if(fileList&&q=="flash.net::FileReferenceList")changes.Add(i,"HostFileReferenceList");
            }
            if(changes.Count==0)return bytes;
            List<string> texts=new List<string>(strings);
            texts.Add("com.jpexs.iggy.runtime");int packageText=texts.Count-1;
            Dictionary<int,int> nameText=new Dictionary<int,int>();
            foreach(KeyValuePair<int,string> change in changes){texts.Add(change.Value);nameText.Add(change.Key,texts.Count-1);}
            using(MemoryStream output=new MemoryStream())using(BinaryWriter w=new BinaryWriter(output)) {
                w.Write(bytes,0,stringsStart);WriteVar(w,texts.Count);
                for(int i=1;i<texts.Count;i++){byte[] s=Encoding.UTF8.GetBytes(texts[i]);WriteVar(w,s.Length);w.Write(s);}
                WriteVar(w,namespaces.Count+1);
                for(int i=1;i<namespaces.Count;i++){w.Write(namespaceKinds[i]);WriteVar(w,namespaces[i]);}
                w.Write((byte)0x16);WriteVar(w,packageText);
                w.Write(bytes,setsStart,setsEnd-setsStart);WriteVar(w,names.Count);
                for(int i=1;i<names.Count;i++) {
                    int text;if(nameText.TryGetValue(i,out text)){w.Write((byte)names[i].Kind);WriteVar(w,namespaces.Count);WriteVar(w,text);}
                    else w.Write(names[i].Raw);
                }
                w.Write(bytes,tail,bytes.Length-tail);return output.ToArray();
            }
        }
    }

    internal sealed class RuntimeScriptPlan {
        readonly bool shared,loader,fileList,audio,timers;
        internal bool NeedsSharedObject {get{return shared;}}
        internal bool NeedsAudio {get{return audio||loader;}}
        internal bool NeedsLoader {get{return loader||timers;}}
        public RuntimeScriptPlan(IEnumerable<byte[]> sources) {
            HashSet<string> references=new HashSet<string>(StringComparer.Ordinal),definitions=new HashSet<string>(StringComparer.Ordinal);
            foreach(byte[] source in sources)foreach(byte[] abc in Container.ReadAbcs(source)) {
                RuntimeAbc parsed=new RuntimeAbc(abc);references.UnionWith(parsed.References);definitions.UnionWith(parsed.Definitions);
            }
            shared=references.Contains("flash.net::SharedObject")&&!definitions.Contains("flash.net::SharedObject");
            loader=references.Contains("flash.display::Loader")&&!definitions.Contains("flash.display::Loader")&&!definitions.Contains("flash.net::URLRequest")&&!definitions.Contains("flash.events::IOErrorEvent");
            foreach(string timer in new string[]{"setInterval","setTimeout","clearInterval","clearTimeout"})if(references.Contains("flash.utils::"+timer)&&!definitions.Contains("flash.utils::"+timer))timers=true;
            shared|=loader&&!definitions.Contains("flash.net::SharedObject");
            fileList=references.Contains("flash.net::FileReferenceList")&&!definitions.Contains("flash.net::FileReferenceList");
            audio=references.Contains("flash.media::Sound")||references.Contains("flash.media::SoundChannel");
            if((audio||loader)&&references.Contains("::__IggyHostSoundEvents"))throw new InvalidDataException("Movie conflicts with the host sound completion class.");
            if(definitions.Contains("com.jpexs.iggy.runtime::HostLoader")||definitions.Contains("com.jpexs.iggy.runtime::HostURLRequest")||definitions.Contains("com.jpexs.iggy.runtime::HostLoaderInfo")||definitions.Contains("com.jpexs.iggy.runtime::HostIOErrorEvent")||definitions.Contains("com.jpexs.iggy.runtime::HostFileReferenceList")||definitions.Contains("com.jpexs.iggy.runtime::HostLoadedBitmap")||definitions.Contains("com.jpexs.iggy.runtime::HostProgressEvent")||definitions.Contains("com.jpexs.iggy.runtime::HostMovieRequest"))
                throw new InvalidDataException("Movie conflicts with the Iggy runtime compatibility namespace.");
            if(shared&&definitions.Contains("com.jpexs.iggy.runtime::HostCodec"))throw new InvalidDataException("Movie conflicts with the SharedObject codec namespace.");
            if(definitions.Contains("com.jpexs.iggy.runtime::HostTimers")||definitions.Contains("com.jpexs.iggy.runtime::HostTimerTask"))throw new InvalidDataException("Movie conflicts with the host timer namespace.");
        }
        static byte[] Resource(string name) {
            using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name)) {
                if(stream==null)throw new InvalidDataException("Missing embedded Iggy runtime: "+name);
                using(MemoryStream data=new MemoryStream()){stream.CopyTo(data);return data.ToArray();}
            }
        }
        public byte[] Apply(byte[] source,bool main,Log log) {
            if(!shared&&!loader&&!fileList&&!audio&&!timers)return source;
            List<byte[]> abcs=new List<byte[]>();
            if(main) {
                if(shared)abcs.Add(Resource("IggyViewer.SharedObject.abc"));
                if(loader||fileList||timers)abcs.Add(Resource("IggyViewer.Loader.abc"));
                if(audio||loader)abcs.Add(Resource("IggyViewer.Audio.abc"));
            }
            bool changed=abcs.Count!=0;
            foreach(byte[] abc in Container.ReadAbcs(source)) {
                byte[] rewritten=new RuntimeAbc(abc).Redirect(loader,fileList);abcs.Add(rewritten);changed|=!Object.ReferenceEquals(abc,rewritten);
            }
            return changed?Container.ReplaceAbcs(source,abcs,log):source;
        }
    }

    internal static partial class Container {
        static int MainEntry(byte[] bytes) {
            if(bytes.Length<32||BitConverter.ToUInt32(bytes,0)!=0xed0a6749||bytes[9]!=64)return -1;
            int count=BitConverter.ToInt32(bytes,28);if(count<1||count>256||32L+count*16>bytes.Length)throw new InvalidDataException("Invalid Iggy directory.");
            for(int i=0;i<count;i++)if(BitConverter.ToUInt16(bytes,32+16*i)==1)return 32+16*i;
            return -1;
        }
        internal static List<byte[]> ReadAbcs(byte[] bytes) {
            List<byte[]> result=new List<byte[]>();int entry=MainEntry(bytes);if(entry<0)return result;
            int size=BitConverter.ToInt32(bytes,entry+4),main=BitConverter.ToInt32(bytes,entry+12);
            if(size<184||main<0||main>bytes.Length-size)throw new InvalidDataException("Invalid Iggy script block.");
            long link=BitConverter.ToInt64(bytes,main+152),node=main+152L+link;HashSet<long> visited=new HashSet<long>();
            while(link!=1) {
                if(node<main||node>main+(long)size-12||!visited.Add(node)||visited.Count>65536)throw new InvalidDataException("Invalid Iggy ABC list.");
                int at=(int)node,length=BitConverter.ToInt32(bytes,at+8);
                if(length<4||length>main+(long)size-node-12)throw new InvalidDataException("Invalid Iggy ABC size.");
                byte[] abc=new byte[length];Buffer.BlockCopy(bytes,at+12,abc,0,length);result.Add(abc);
                link=BitConverter.ToInt64(bytes,at);node+=link;
            }
            return result;
        }
        internal static byte[] ReplaceAbcs(byte[] bytes,List<byte[]> abcs,Log log) {
            int entry=MainEntry(bytes);if(entry<0||abcs.Count==0)return bytes;
            int size=BitConverter.ToInt32(bytes,entry+4),main=BitConverter.ToInt32(bytes,entry+12),blocks=BitConverter.ToInt32(bytes,28),target=0;
            for(int at=32;at<entry;at+=16){int kind=BitConverter.ToUInt16(bytes,at);if(kind==1||kind==8)target++;}
            List<Field> pointers=new List<Field>();byte[] data;
            using(MemoryStream block=new MemoryStream())using(BinaryWriter w=new BinaryWriter(block)) {
                w.Write(bytes,main,size);int previous=152;
                foreach(byte[] abc in abcs) {
                    block.Position=block.Length;while(block.Position%8!=0)w.Write((byte)0);
                    int node=checked((int)block.Position);block.Position=previous;w.Write((long)node-previous);
                    block.Position=node;w.Write(1L);w.Write(abc.Length);w.Write(abc);
                    pointers.Add(new Field {Offset=node,Operation=2,Order=pointers.Count});previous=node;
                }
                block.Position=168;w.Write((byte)1);data=block.ToArray();
            }
            using(MemoryStream output=new MemoryStream())using(BinaryWriter w=new BinaryWriter(output)) {
                w.Write(bytes);bool fixedHead=false;
                for(int i=0;i<blocks;i++) {
                    int at=32+16*i;if(BitConverter.ToUInt16(bytes,at)!=0||BitConverter.ToUInt16(bytes,at+2)!=target)continue;
                    int length=BitConverter.ToInt32(bytes,at+4),start=BitConverter.ToInt32(bytes,at+12);
                    bool head=Decode(bytes,start,length,size).Exists(f=>f.Offset==152&&f.Operation==2);
                    if(!head)continue;if(fixedHead)throw new InvalidDataException("Duplicate ABC head relocation.");fixedHead=true;
                    byte[] program=Compact(bytes,start,length,data.Length,pointers);
                    // Prepare partitions large programs into consecutive fixup
                    // entries for the same target before calling the DLL.
                    output.Position=output.Length;int position=checked((int)output.Position);w.Write(program);
                    output.Position=at+4;w.Write(program.Length);w.Write(program.Length);w.Write(position);
                }
                if(!fixedHead)throw new InvalidDataException("Missing Iggy ABC head relocation.");
                output.Position=output.Length;while(output.Position%8!=0)w.Write((byte)0);
                int location=checked((int)output.Position);w.Write(data);output.Position=entry+4;w.Write(data.Length);w.Write(data.Length);w.Write(location);
                log.Write("Installed runtime AS3 compatibility in memory; redirected unsupported runtime QNames.");
                return output.ToArray();
            }
        }
    }
}
