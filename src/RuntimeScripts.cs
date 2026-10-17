using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace IggyViewer {
    // Rewrite selected names without changing existing pool indices or string
    // literals. Constructor bootstrapping is handled in RuntimeBootstrap.cs.
    internal sealed partial class RuntimeAbc {
        readonly byte[] bytes;
        readonly List<string> strings=new List<string>{""};
        readonly List<int> namespaces=new List<int>{0};
        readonly List<byte> namespaceKinds=new List<byte>{0};
        readonly List<int[]> namespaceSets=new List<int[]>{new int[0]};
        sealed class Name {public int Kind,Ns,Text;public byte[] Raw;}
        readonly List<Name> names=new List<Name>{null};
        readonly int stringsStart,setsStart,setsEnd,tail;
        int cursor;
        public readonly HashSet<string> References=new HashSet<string>(StringComparer.Ordinal);
        public readonly HashSet<string> Definitions=new HashSet<string>(StringComparer.Ordinal);
        public readonly HashSet<string> Classes=new HashSet<string>(StringComparer.Ordinal);
        internal readonly HashSet<string> PublicGlobals=new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<string,int> constructors=new Dictionary<string,int>(StringComparer.Ordinal);
        readonly int bodiesStart;
        readonly int methodsEnd,classCountAt,instancesStart,classInfoStart,scriptsStart,methodCount,classCount;
        sealed class Script {
            internal int Initializer,TraitsStart,End;
            internal string Anchor;
            internal readonly List<string> Globals=new List<string>();
        }
        readonly List<Script> scripts=new List<Script>();
        internal readonly Dictionary<string,string> GlobalScripts=new Dictionary<string,string>(StringComparer.Ordinal);
        internal readonly HashSet<string> GlobalFunctions=new HashSet<string>(StringComparer.Ordinal);
        readonly List<Tuple<int,int,int>> traitNames=new List<Tuple<int,int,int>>();
        readonly HashSet<int> globalTraits=new HashSet<int>();
        readonly HashSet<int> scriptInitializers=new HashSet<int>();
        readonly Dictionary<int,HashSet<int>> methodMembers=new Dictionary<int,HashSet<int>>();
        HashSet<int> currentMembers;
        int Byte(){if(cursor>=bytes.Length)throw new InvalidDataException("Truncated runtime ABC.");return bytes[cursor++];}
        uint Var(){uint v=0;for(int i=0;i<5;i++){int b=Byte();v|=(uint)(b&127)<<(i*7);if((b&128)==0)return v;}throw new InvalidDataException("Invalid ABC integer.");}
        int Count(){uint n=Var();if(n>1000000)throw new InvalidDataException("ABC count limit exceeded.");return (int)n;}
        void Skip(int n){if(n<0||n>bytes.Length-cursor)throw new InvalidDataException("Truncated ABC data.");cursor+=n;}
        string Qualified(int index) {
            if(index<=0||index>=names.Count)return null;
            Name n=names[index];if(n.Kind!=7&&n.Kind!=13&&n.Kind!=9&&n.Kind!=14)return null;
            if(n.Ns==0)return null;
            if(n.Ns<=0||n.Ns>=namespaces.Count||n.Text<0||n.Text>=strings.Count)throw new InvalidDataException("Invalid ABC QName.");
            if(namespaceKinds[n.Ns]!=0x16&&namespaceKinds[n.Ns]!=0x17)return null;
            return strings[namespaces[n.Ns]]+"::"+strings[n.Text];
        }
        void Traits(bool global=false) {
            for(int i=Count();i>0;i--) {
                int position=cursor,name=Count(),kind=Byte();traitNames.Add(Tuple.Create(position,name,kind&15));
                if(currentMembers!=null)currentMembers.Add(name);
                if(global){globalTraits.Add(position);string qualified=Qualified(name);if(qualified!=null)Definitions.Add(qualified);}
                switch(kind&15) {
                    case 0:case 6:Var();Var();if(Var()!=0)Byte();break;
                    case 1:case 2:case 3:case 5:Var();int method=Count();if(currentMembers!=null)methodMembers[method]=currentMembers;break;
                    case 4:Var();Var();break;
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
            setsStart=cursor;
            for(int n=Count()-1;n>0;n--) {int[] set=new int[Count()];for(int i=0;i<set.Length;i++)set[i]=Count();namespaceSets.Add(set);}
            setsEnd=cursor;
            for(int n=Count()-1;n>0;n--) {
                int start=cursor;Name name=new Name {Kind=Byte()};
                switch(name.Kind) {
                    case 7:case 13:name.Ns=Count();name.Text=Count();break;
                    case 15:case 16:case 27:case 28:Var();break;
                    case 17:case 18:break;
                    case 9:case 14:
                        name.Text=Count();int set=Count();
                        if(set<=0||set>=namespaceSets.Count)throw new InvalidDataException("Invalid ABC namespace set.");
                        if(namespaceSets[set].Length==1)name.Ns=namespaceSets[set][0];
                        break;
                    case 29:Var();for(int k=Count();k>0;k--)Var();break;
                    default:throw new InvalidDataException("Unknown ABC multiname.");
                }
                name.Raw=new byte[cursor-start];Buffer.BlockCopy(bytes,start,name.Raw,0,name.Raw.Length);names.Add(name);
            }
            tail=cursor;
            for(int i=1;i<names.Count;i++){string q=Qualified(i);if(q!=null)References.Add(q);}
            methodCount=Count();
            for(int n=methodCount;n>0;n--) {
                int parameters=Count();Var();for(int i=0;i<parameters;i++)Var();Var();int flags=Byte();
                if((flags&8)!=0)for(int i=Count();i>0;i--){Var();Byte();}
                if((flags&128)!=0)for(int i=0;i<parameters;i++)Var();
            }
            methodsEnd=cursor;
            for(int n=Count();n>0;n--){Var();int pairs=Count();for(int i=0;i<pairs*2;i++)Var();}
            classCountAt=cursor;int classes=Count();classCount=classes;instancesStart=cursor;
            List<HashSet<int>> members=new List<HashSet<int>>();
            for(int n=classes;n>0;n--) {
                currentMembers=new HashSet<int>();members.Add(currentMembers);
                string q=Qualified(Count());if(q!=null){Definitions.Add(q);Classes.Add(q);}Var();int flags=Byte();if((flags&8)!=0)Var();
                for(int i=Count();i>0;i--)Var();int initializer=Count();methodMembers[initializer]=currentMembers;if(q!=null)constructors[q]=initializer;Traits();
            }
            classInfoStart=cursor;
            for(int i=0;i<classes;i++){currentMembers=members[i];int initializer=Count();methodMembers[initializer]=currentMembers;Traits();}
            currentMembers=null;
            scriptsStart=cursor;
            for(int n=Count();n>0;n--) {
                Script script=new Script {Initializer=Count(),TraitsStart=cursor};scriptInitializers.Add(script.Initializer);
                int first=traitNames.Count;Traits(true);script.End=cursor;scripts.Add(script);
                for(int i=first;i<traitNames.Count;i++) {
                    Tuple<int,int,int> trait=traitNames[i];string qualified=Qualified(trait.Item2);if(qualified==null)continue;
                    if(trait.Item3==4){if(script.Anchor==null)script.Anchor=qualified;}else {
                        script.Globals.Add(qualified);if(trait.Item3==1||trait.Item3==5)GlobalFunctions.Add(qualified);
                        if(namespaceKinds[names[trait.Item2].Ns]==0x16)PublicGlobals.Add(qualified);
                    }
                    if(trait.Item3==4&&qualified.StartsWith("com.jpexs.iggy.globals.",StringComparison.Ordinal))script.Anchor=qualified;
                }
                if(script.Anchor!=null)foreach(string name in script.Globals)GlobalScripts[name]=script.Anchor;
            }
            bodiesStart=cursor;
        }
        static void WriteVar(BinaryWriter w,int n){uint value=(uint)n;while(value>=128){w.Write((byte)(value|128));value>>=7;}w.Write((byte)value);}
        internal byte[] RenameClasses(Dictionary<string,string> classes) {
            // The DLL shares script globals by qualified name. Give loaded
            // classes domain-specific packages while retaining public aliases.
            Dictionary<int,string> changed=new Dictionary<int,string>();
            for(int i=1;i<names.Count;i++) {
                Name name=names[i];string qualified=Qualified(i),replacement;
                if(qualified!=null&&classes.TryGetValue(qualified,out replacement)&&qualified!=replacement)changed[i]=replacement;
                // Multi-namespace property lookups can share a class's text
                // name (object.Main); they must retain their public lookup.
            }
            if(changed.Count==0)return bytes;
            List<string> texts=new List<string>(strings);List<int> packages=new List<int>();
            Dictionary<int,int> textIndices=new Dictionary<int,int>(),namespaceIndices=new Dictionary<int,int>();
            Dictionary<int,int> writes=new Dictionary<int,int>();int alias=0;
            foreach(KeyValuePair<int,string> change in changed) {
                int split=change.Value.LastIndexOf("::",StringComparison.Ordinal);
                texts.Add(change.Value.Substring(0,split));packages.Add(texts.Count-1);namespaceIndices[change.Key]=namespaces.Count+packages.Count-1;
                texts.Add(change.Value.Substring(split+2));textIndices[change.Key]=texts.Count-1;
                writes[change.Key]=names.Count+alias;alias++;
            }
            using(MemoryStream output=new MemoryStream())using(BinaryWriter w=new BinaryWriter(output)) {
                w.Write(bytes,0,stringsStart);WriteVar(w,texts.Count);
                for(int i=1;i<texts.Count;i++){byte[] text=Encoding.UTF8.GetBytes(texts[i]);WriteVar(w,text.Length);w.Write(text);}
                WriteVar(w,namespaces.Count+packages.Count);
                for(int i=1;i<namespaces.Count;i++){w.Write(namespaceKinds[i]);WriteVar(w,namespaces[i]);}
                foreach(int package in packages){w.Write((byte)0x16);WriteVar(w,package);}
                w.Write(bytes,setsStart,setsEnd-setsStart);WriteVar(w,names.Count+changed.Count);
                for(int i=1;i<names.Count;i++) {
                    int text;if(textIndices.TryGetValue(i,out text)){w.Write((byte)(names[i].Kind==13||names[i].Kind==14?13:7));WriteVar(w,namespaceIndices[i]);WriteVar(w,text);}
                    else w.Write(names[i].Raw);
                }
                foreach(KeyValuePair<int,string> change in changed) {
                    w.Write(names[change.Key].Raw);
                }
                RewriteClassBodies(w,writes);return output.ToArray();
            }
        }
        public byte[] Redirect(bool loader,bool fileList,bool domainsOnly=false) {
            if(!loader&&!fileList)return bytes;
            Dictionary<int,string> changes=new Dictionary<int,string>();
            for(int i=1;i<names.Count;i++) {
                string q=Qualified(i);
                if(loader&&!domainsOnly&&q=="flash.display::Loader")changes.Add(i,"HostLoader");
                if(loader&&!domainsOnly&&q=="flash.net::URLRequest")changes.Add(i,"HostURLRequest");
                if(loader&&!domainsOnly&&q=="flash.events::IOErrorEvent")changes.Add(i,"HostIOErrorEvent");
                if(loader&&q=="flash.system::ApplicationDomain")changes.Add(i,"HostApplicationDomain");
                if(loader&&q=="flash.system::LoaderContext")changes.Add(i,"HostLoaderContext");
                if(loader&&!domainsOnly&&q=="flash.utils::getDefinitionByName")changes.Add(i,"HostGetDefinitionByName");
                if(loader&&!domainsOnly&&q=="flash.utils::getQualifiedClassName")changes.Add(i,"HostGetQualifiedClassName");
                if(loader&&!domainsOnly&&q=="flash.utils::getQualifiedSuperclassName")changes.Add(i,"HostGetQualifiedSuperclassName");
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
                    int text;if(nameText.TryGetValue(i,out text)){w.Write((byte)(names[i].Kind==9?7:names[i].Kind==14?13:names[i].Kind));WriteVar(w,namespaces.Count);WriteVar(w,text);}
                    else w.Write(names[i].Raw);
                }
                w.Write(bytes,tail,bytes.Length-tail);return output.ToArray();
            }
        }
    }

    internal sealed class RuntimeScriptPlan {
        internal readonly HashSet<string> Classes=new HashSet<string>(StringComparer.Ordinal);
        internal readonly HashSet<string> Definitions=new HashSet<string>(StringComparer.Ordinal);
        internal readonly HashSet<string> Globals=new HashSet<string>(StringComparer.Ordinal);
        internal readonly HashSet<string> ClassReferences=new HashSet<string>(StringComparer.Ordinal);
        readonly bool shared,loader,fileList,audio,timers;
        internal bool NeedsSharedObject {get{return shared;}}
        internal bool NeedsAudio {get{return audio||loader;}}
        internal bool NeedsLoader {get{return loader||timers;}}
        public RuntimeScriptPlan(IEnumerable<byte[]> sources) {
            HashSet<string> references=new HashSet<string>(StringComparer.Ordinal),definitions=new HashSet<string>(StringComparer.Ordinal);
            foreach(byte[] source in sources)foreach(byte[] abc in Container.ReadAbcs(source)) {
                RuntimeAbc parsed=new RuntimeAbc(abc);references.UnionWith(parsed.References);definitions.UnionWith(parsed.Definitions);Classes.UnionWith(parsed.Classes);ClassReferences.UnionWith(parsed.References);
                Globals.UnionWith(parsed.PublicGlobals);
            }
            Definitions.UnionWith(definitions);
            shared=references.Contains("flash.net::SharedObject")&&!definitions.Contains("flash.net::SharedObject");
            loader=references.Contains("flash.display::Loader")&&!definitions.Contains("flash.display::Loader")&&!definitions.Contains("flash.net::URLRequest")&&!definitions.Contains("flash.events::IOErrorEvent");
            loader|=references.Contains("flash.system::ApplicationDomain")||references.Contains("flash.system::LoaderContext");
            loader|=references.Contains("::applicationDomain");
            loader|=references.Contains("flash.utils::getDefinitionByName");
            foreach(string timer in new string[]{"setInterval","setTimeout","clearInterval","clearTimeout"})if(references.Contains("flash.utils::"+timer)&&!definitions.Contains("flash.utils::"+timer))timers=true;
            shared|=loader&&!definitions.Contains("flash.net::SharedObject");
            fileList=references.Contains("flash.net::FileReferenceList")&&!definitions.Contains("flash.net::FileReferenceList");
            audio=references.Contains("flash.media::Sound")||references.Contains("flash.media::SoundChannel");
            if((audio||loader)&&references.Contains("::__IggyHostSoundEvents"))throw new InvalidDataException("Movie conflicts with the host sound completion class.");
            if(definitions.Contains("com.jpexs.iggy.runtime::HostLoader")||definitions.Contains("com.jpexs.iggy.runtime::HostURLRequest")||definitions.Contains("com.jpexs.iggy.runtime::HostLoaderInfo")||definitions.Contains("com.jpexs.iggy.runtime::HostIOErrorEvent")||definitions.Contains("com.jpexs.iggy.runtime::HostFileReferenceList")||definitions.Contains("com.jpexs.iggy.runtime::HostLoadedBitmap")||definitions.Contains("com.jpexs.iggy.runtime::HostProgressEvent")||definitions.Contains("com.jpexs.iggy.runtime::HostMovieRequest"))
                throw new InvalidDataException("Movie conflicts with the Iggy runtime compatibility namespace.");
            if(shared&&definitions.Contains("com.jpexs.iggy.runtime::HostCodec"))throw new InvalidDataException("Movie conflicts with the SharedObject codec namespace.");
            if(definitions.Contains("com.jpexs.iggy.runtime::HostTimers")||definitions.Contains("com.jpexs.iggy.runtime::HostTimerTask"))throw new InvalidDataException("Movie conflicts with the host timer namespace.");
            foreach(string name in new string[]{"flash.system::ApplicationDomain","flash.system::LoaderContext","com.jpexs.iggy.runtime::HostDefinitionRequest","com.jpexs.iggy.runtime::HostBytesRequest","com.jpexs.iggy.runtime::HostDomainBinding","com.jpexs.iggy.runtime::HostDomainFactory","com.jpexs.iggy.runtime::HostDomainNames","com.jpexs.iggy.runtime::HostApplicationDomain","com.jpexs.iggy.runtime::HostLoaderContext","com.jpexs.iggy.runtime::HostGetDefinitionByName","com.jpexs.iggy.runtime::HostGetQualifiedClassName","com.jpexs.iggy.runtime::HostGetQualifiedSuperclassName"})
                if(definitions.Contains(name))throw new InvalidDataException("Movie conflicts with the host application domain namespace.");
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
                if(loader||fileList||timers)abcs.Add(new RuntimeAbc(Resource("IggyViewer.Loader.abc")).Redirect(true,false,true));
                if(audio||loader)abcs.Add(Resource("IggyViewer.Audio.abc"));
            }
            bool changed=abcs.Count!=0;
            string document=main&&(loader||timers)?Container.DocumentClass(source):null;
            foreach(byte[] abc in Container.ReadAbcs(source)) {
                byte[] bound=loader||timers?new RuntimeAbc(abc).BindGlobals():abc;
                byte[] rewritten=new RuntimeAbc(bound).Redirect(loader,fileList);
                if(document!=null)rewritten=new RuntimeAbc(rewritten).BootstrapDomain(document);
                abcs.Add(rewritten);changed|=!Object.ReferenceEquals(abc,rewritten);
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
        internal static byte[] ReplaceAbcs(byte[] bytes,List<byte[]> abcs,Log log,Dictionary<string,string> imports=null) {
            int entry=MainEntry(bytes);if(entry<0||(abcs.Count==0&&imports==null))return bytes;
            int size=BitConverter.ToInt32(bytes,entry+4),main=BitConverter.ToInt32(bytes,entry+12),blocks=BitConverter.ToInt32(bytes,28),target=0;
            for(int at=32;at<entry;at+=16){int kind=BitConverter.ToUInt16(bytes,at);if(kind==1||kind==8)target++;}
            List<Field> pointers=new List<Field>();byte[] data;
            using(MemoryStream block=new MemoryStream())using(BinaryWriter w=new BinaryWriter(block)) {
                w.Write(bytes,main,size);int previous=152;
                if(imports!=null) {
                    string[] urls=Imports(bytes);long table=112L+BitConverter.ToInt64(bytes,main+112);
                    for(int i=0;i<urls.Length;i++) {
                        string replacement;if(!imports.TryGetValue(urls[i],out replacement))continue;
                        block.Position=block.Length;int text=checked((int)block.Position);w.Write(Encoding.Unicode.GetBytes(replacement+"\0"));
                        block.Position=table+24*i;w.Write(text-block.Position);
                    }
                }
                block.Position=152;w.Write(1L);
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
