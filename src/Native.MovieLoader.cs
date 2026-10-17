using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace IggyViewer {
    internal unsafe sealed partial class Native {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int LoadMovieAbc(IntPtr player,IntPtr library,int index,IntPtr options);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr MovieClass(IntPtr vm,IntPtr name);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr Arena(int size,int growth,IntPtr allocator);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr MovieAlloc(IntPtr player,IntPtr owner,uint size,int tag,IntPtr name);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr LegacyMovieAlloc(IntPtr player,IntPtr owner,uint size,int tag);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr MovieFromDefinition(IntPtr player,IntPtr definition,IntPtr args,int count);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr MovieReference(IntPtr player,IntPtr value,int weak);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr MovieString(IntPtr vm,IntPtr text,int length);
        internal IntPtr ReturnMovieString(IntPtr context,string text) {
            IntPtr buffer=Marshal.StringToHGlobalUni(text);
            try {Marshal.WriteIntPtr(Marshal.ReadIntPtr(context,8),MovieFunction<MovieString>(0x452a0,0x47140)(Marshal.ReadIntPtr(context),buffer,text.Length));}
            finally {Marshal.FreeHGlobal(buffer);}
            return IntPtr.Zero;
        }
        internal sealed class LoadedMovie {
            internal string FileName;
            internal int Width,Height,Library,Domain;
            internal IntPtr Definition;
            internal Dictionary<string,string> ClassNames;
        }
        readonly Dictionary<string,LoadedMovie> movieLibraries=new Dictionary<string,LoadedMovie>(StringComparer.OrdinalIgnoreCase);
        readonly List<IntPtr> movieDefinitions=new List<IntPtr>();
        readonly List<IntPtr> movieSymbolNames=new List<IntPtr>();
        readonly List<IntPtr> movieClassReferences=new List<IntPtr>();
        readonly Dictionary<IntPtr,IntPtr> movieClassRoots=new Dictionary<IntPtr,IntPtr>();
        IntPtr KeepMovieClass(IntPtr metadata) {
            IntPtr reference;if(movieClassRoots.TryGetValue(metadata,out reference))return reference;
            IntPtr value=Marshal.ReadIntPtr(metadata,48);if(value==IntPtr.Zero)return IntPtr.Zero;
            reference=Activity.HostReference(value,0);movieClassRoots.Add(metadata,reference);movieClassReferences.Add(reference);return reference;
        }

        T MovieFunction<T>(int modern,int legacy) where T:class {
            return (T)(object)Marshal.GetDelegateForFunctionPointer(IntPtr.Add(module,modernDll?modern:legacy),typeof(T));
        }
        IntPtr MovieAllocate(uint size) {
            if(modernDll)return MovieFunction<MovieAlloc>(0x56990,0)(Player,IntPtr.Zero,size,183,IntPtr.Zero);
            return MovieFunction<LegacyMovieAlloc>(0,0x5b770)(Player,IntPtr.Zero,size,183);
        }
        static List<KeyValuePair<IntPtr,IntPtr>> MovieRegistry(IntPtr vm) {
            int count=Marshal.ReadInt32(vm,168);IntPtr table=Marshal.ReadIntPtr(vm,144);
            if(count<1||count>1048576)throw new InvalidDataException("Invalid native class registry.");
            List<KeyValuePair<IntPtr,IntPtr>> entries=new List<KeyValuePair<IntPtr,IntPtr>>();
            for(int i=0;i<count;i++) {
                IntPtr key=Marshal.ReadIntPtr(table,16*i);
                if(key!=IntPtr.Zero)entries.Add(new KeyValuePair<IntPtr,IntPtr>(key,Marshal.ReadIntPtr(table,16*i+8)));
            }
            return entries;
        }
        static void MovieRegistry(IntPtr vm,List<KeyValuePair<IntPtr,IntPtr>> entries,HashSet<IntPtr> excluded) {
            int count=Marshal.ReadInt32(vm,168);IntPtr table=Marshal.ReadIntPtr(vm,144);
            if(count<1||count>1048576||entries.Count>=count)throw new InvalidDataException("Invalid native class registry capacity.");
            for(int i=0;i<count*2;i++)Marshal.WriteIntPtr(table,i*8,IntPtr.Zero);
            int used=0;
            foreach(KeyValuePair<IntPtr,IntPtr> entry in entries) {
                if(excluded!=null&&excluded.Contains(entry.Key))continue;
                uint hash=(uint)Marshal.ReadInt32(entry.Key),index=hash&(uint)(count-1),step=(hash+(hash>>11))|1;
                while(Marshal.ReadIntPtr(table,(int)index*16)!=IntPtr.Zero)index=(index+step)&(uint)(count-1);
                Marshal.WriteIntPtr(table,(int)index*16,entry.Key);Marshal.WriteIntPtr(table,(int)index*16+8,entry.Value);used++;
            }
            Marshal.WriteInt32(vm,172,used);
        }
        IntPtr MovieLookup(IntPtr vm,string name) {
            IntPtr text=Marshal.StringToHGlobalUni(name.StartsWith("::",StringComparison.Ordinal)?name.Substring(2):name.Replace("::","."));
            try {return MovieFunction<MovieClass>(0x2d80,0x1080)(vm,text);}finally {Marshal.FreeHGlobal(text);}
        }
        IntPtr BuiltinMovieClip(IntPtr vm) {
            int count=Marshal.ReadInt32(vm,modernDll?9648:9624);
            for(int i=0;i<count;i++) {
                IntPtr entry=IntPtr.Add(vm,(modernDll?3648:3624)+24*i);
                if(Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(entry))=="MovieClip")return Marshal.ReadIntPtr(entry,16);
            }
            throw new InvalidDataException("Missing native MovieClip class.");
        }
        // Called outside AS3, between native ticks. No public Iggy entry point
        // is re-entered from the Loader's ExternalInterface callback.
        internal LoadedMovie RegisterMovie(string file,byte[] original,int domain) {
            if(!ValidMovieDomain(domain))throw new InvalidDataException("Invalid target application domain.");
            if(original.Length<32||BitConverter.ToUInt32(original,0)!=0xed0a6749||original[9]!=64)
                throw new InvalidDataException("Dynamic loading requires a 64-bit IGGY container.");
            string key;
            using(SHA256 sha=SHA256.Create())key=domain+"|"+Path.GetFullPath(file)+"|"+BitConverter.ToString(sha.ComputeHash(original));
            LoadedMovie cached;if(movieLibraries.TryGetValue(key,out cached))return cached;
            if(libraries.Count>=32)throw new IOException("The native runtime's 32-library limit has been reached.");
            if(compilerLifetime==null)throw new InvalidOperationException("The movie linker was not retained before startup.");
            List<byte[]> sources=new List<byte[]> {original};
            CollectRuntimeSources(file,original,sources,new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            RuntimeScriptPlan plan=new RuntimeScriptPlan(sources),parentPlan=runtimeScripts;
            try {runtimeScripts=plan;LoadImports(file,original,new HashSet<string>(StringComparer.OrdinalIgnoreCase));}
            finally {runtimeScripts=parentPlan;}
            Dictionary<string,string> renamed=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(byte[] abc in Container.ReadAbcs(original))foreach(string definition in new RuntimeAbc(abc).Classes) {
                int split=definition.LastIndexOf("::",StringComparison.Ordinal);
                string package=definition.Substring(0,split);
                renamed[definition]="com.jpexs.iggy.domain.d"+domain+(package.Length==0?"":"."+package)+"::"+definition.Substring(split+2);
            }
            byte[] prepared=plan.Apply(original,false,log);List<byte[]> domainAbcs=new List<byte[]>();
            foreach(byte[] abc in Container.ReadAbcs(prepared))domainAbcs.Add(new RuntimeAbc(abc).RenameClasses(renamed));
            byte[] bytes=Container.Prepare(Container.ReplaceAbcs(prepared,domainAbcs,log),log),name=Encoding.UTF8.GetBytes("__iggy_dynamic_"+Guid.NewGuid().ToString("N")+".iggy\0");
            byte* options=stackalloc byte[80];for(int i=0;i<80;i++)options[i]=0;
            int id;fixed(byte* p=bytes)fixed(byte* n=name)id=createLibrary((IntPtr)n,(IntPtr)p,(uint)bytes.Length,(IntPtr)options);
            if(id<0||id>31)throw new InvalidDataException("The DLL rejected the loaded movie.");
            libraries.Add(id);
            IntPtr generic=libraryGeneric(id),vm=Marshal.ReadIntPtr(Player,modernDll?10560:10480);
            int frames=Marshal.ReadInt32(generic,104),depth=Marshal.ReadInt32(generic,108);
            if(frames<1||frames>65535||depth<0||depth>65535)throw new InvalidDataException("Loaded movie exceeds native sprite frame/depth limits.");
            NormalizeTimelineMatrices(generic);NormalizeEmptyStyleTables(generic);NormalizeAudio(generic);InstallTextures(generic,file);
            LoadedMovie movie=new LoadedMovie {FileName=Path.GetFullPath(file),Library=id,Domain=domain,ClassNames=renamed,Width=Marshal.ReadInt32(generic,16),Height=Marshal.ReadInt32(generic,20)};
            // The DLL also resolves resource classes through the native symbol
            // table when constructing Sound/Bitmap/font instances. Keep those
            // names consistent with the isolated ABC definitions.
            int symbolCount=Marshal.ReadInt32(generic,560);IntPtr symbolTable=Marshal.ReadIntPtr(generic,552);
            if(symbolCount<0||symbolCount>65536)throw new InvalidDataException("Invalid domain symbol table.");
            for(int i=0;i<symbolCount;i++) {
                IntPtr symbol=IntPtr.Add(symbolTable,24*i);string originalName=Marshal.PtrToStringUni(Marshal.ReadIntPtr(symbol,8)),replacement;
                if(originalName==null)continue;
                if(!renamed.TryGetValue(originalName.IndexOf("::",StringComparison.Ordinal)>=0?originalName:ClassQName(originalName),out replacement))continue;
                IntPtr text=Marshal.StringToHGlobalUni(DomainName(replacement));movieSymbolNames.Add(text);Marshal.WriteIntPtr(symbol,8,text);
            }
            HashSet<IntPtr> excluded=new HashSet<IntPtr>();
            foreach(byte[] source in sources)foreach(byte[] abc in Container.ReadAbcs(source))foreach(string definition in new RuntimeAbc(abc).Classes) {
                string resolved;IntPtr metadata=MovieLookup(vm,renamed.TryGetValue(definition,out resolved)?resolved:definition);
                if(metadata!=IntPtr.Zero)excluded.Add(Marshal.ReadIntPtr(metadata,96));
            }
            List<KeyValuePair<IntPtr,IntPtr>> registry=MovieRegistry(vm);
            HashSet<IntPtr> parentClasses=new HashSet<IntPtr>();
            foreach(KeyValuePair<IntPtr,IntPtr> entry in registry)parentClasses.Add(entry.Value);
            int arenaOffset=modernDll?27016:26992,compileFlag=modernDll?27024:27000;
            IntPtr previousArena=Marshal.ReadIntPtr(vm,arenaOffset);int previousFlag=Marshal.ReadInt32(vm,compileFlag);
            MovieRegistry(vm,registry,excluded);
            Marshal.WriteIntPtr(vm,arenaOffset,MovieFunction<Arena>(0x5a580,0x5e0f0)(8192,8192,Marshal.ReadIntPtr(Player,modernDll?13616:13536)));
            Marshal.WriteInt32(vm,compileFlag,1);
            try {
                uint dependencies=(uint)(Marshal.ReadInt32(generic,608)|Marshal.ReadInt32(generic,612));
                for(int library=0;library<32;library++)if((dependencies&(1u<<library))!=0&&(Marshal.ReadInt32(Player,612)&(1u<<library))==0)
                    LinkMovieLibrary(vm,library,libraryGeneric(library),false,movie);
                LinkMovieLibrary(vm,id,generic,true,movie);
                // The VM GC scans class objects as roots. Keep child definitions
                // alive independently of the parent's restored name registry.
                foreach(KeyValuePair<IntPtr,IntPtr> entry in MovieRegistry(vm)) {
                    if(parentClasses.Contains(entry.Value))continue;
                    if(Activity!=null)Activity.PatchClass(entry.Value,domain);
                    KeepMovieClass(entry.Value);
                }
                foreach(byte[] source in sources)foreach(byte[] abc in Container.ReadAbcs(source))foreach(string definition in new RuntimeAbc(abc).Classes)
                    {string resolved;AddDomainDefinition(domain,definition,MovieLookup(vm,renamed.TryGetValue(definition,out resolved)?resolved:definition));}
            } finally {
                compilerLifetime.Retain(vm);Marshal.WriteIntPtr(vm,arenaOffset,previousArena);Marshal.WriteInt32(vm,compileFlag,previousFlag);
                MovieRegistry(vm,registry,null);
            }
            if(valid(Player)==0)throw new InvalidDataException("Native verification failed while linking the loaded movie.");
            movieLibraries.Add(key,movie);log.Write("Linked local movie into parent VM: "+file+" (library "+id+").");return movie;
        }
        void LinkMovieLibrary(IntPtr vm,int id,IntPtr generic,bool root,LoadedMovie movie) {
            if(MovieFunction<LoadMovieAbc>(0x44e0,0x15a0)(Player,generic,id,IntPtr.Zero)==0)throw new InvalidDataException("Cannot link loaded movie ABC.");
            int count=Marshal.ReadInt32(generic,160),symbolsCount=Marshal.ReadInt32(generic,560);
            if(count<0||count>65536||symbolsCount<0||symbolsCount>65536)throw new InvalidDataException("Invalid loaded movie symbols.");
            if(root&&count==65536)throw new InvalidDataException("Loaded movie needs one unused character slot for its root.");
            int rootId=Math.Max(1,count),mapCount=root?rootId+1:Math.Max(1,count);
            IntPtr map=MovieAllocate((uint)(8*mapCount)),symbols=Marshal.ReadIntPtr(generic,552);
            if(map==IntPtr.Zero)throw new OutOfMemoryException();
            for(int i=0;i<mapCount;i++)Marshal.WriteIntPtr(map,8*i,IntPtr.Zero);
            Marshal.WriteIntPtr(Player,872+8*id,map);
            IntPtr document=IntPtr.Zero;
            for(int i=0;i<symbolsCount;i++) {
                IntPtr symbol=IntPtr.Add(symbols,24*i);int character=Marshal.ReadInt32(symbol,16);
                string className=Marshal.PtrToStringUni(Marshal.ReadIntPtr(symbol,8)),resolved;
                IntPtr metadata=MovieLookup(vm,movie.ClassNames.TryGetValue(className.IndexOf("::",StringComparison.Ordinal)>=0?className:ClassQName(className),out resolved)?resolved:className);
                if(metadata==IntPtr.Zero)throw new InvalidDataException("Loaded movie class cannot be resolved.");
                if(character==0){document=metadata;continue;}
                if(character<0||character>=count)throw new InvalidDataException("Invalid loaded SymbolClass ID.");
                Marshal.WriteIntPtr(map,8*character,metadata);
                IntPtr definition=Marshal.ReadIntPtr(Marshal.ReadIntPtr(generic,152),8*character);
                if(definition!=IntPtr.Zero)Marshal.WriteIntPtr(metadata,32,definition);
            }
            if(root) {
                IntPtr definition=Marshal.AllocHGlobal(72);movieDefinitions.Add(definition);
                for(int i=0;i<72;i++)Marshal.WriteByte(definition,i,0);
                Marshal.WriteByte(definition,0,3);Marshal.WriteByte(definition,1,(byte)id);
                // Character zero tells the native instance destructor to free
                // an instance-owned definition. Our cached template is shared
                // across loads and host-owned, so give it a spare nonzero ID.
                Marshal.WriteInt16(definition,2,(short)rootId);
                Marshal.WriteInt16(definition,40,(short)Marshal.ReadInt32(generic,104));Marshal.WriteInt16(definition,42,(short)Marshal.ReadInt32(generic,108));
                Marshal.WriteIntPtr(definition,56,Marshal.ReadIntPtr(generic,128));Marshal.WriteIntPtr(definition,64,Marshal.ReadIntPtr(generic,136));
                if(document==IntPtr.Zero)document=BuiltinMovieClip(vm);
                else Marshal.WriteIntPtr(document,32,definition);
                Marshal.WriteIntPtr(map,document);Marshal.WriteIntPtr(map,8*rootId,document);movie.Definition=definition;
            }
            if((Marshal.ReadInt32(Player,608)&(1u<<id))==0)Marshal.WriteInt32(generic,536,Marshal.ReadInt32(generic,536)+1);
            Marshal.WriteInt32(Player,608,Marshal.ReadInt32(Player,608)|(1<<id));Marshal.WriteInt32(Player,612,Marshal.ReadInt32(Player,612)|(1<<id));
        }
        internal IntPtr InstantiateMovie(LoadedMovie movie) {
            IntPtr instance=MovieFunction<MovieFromDefinition>(0x11e20,0x19ff0)(Player,movie.Definition,IntPtr.Zero,0);
            IntPtr reference=instance==IntPtr.Zero?IntPtr.Zero:Marshal.ReadIntPtr(instance,32);
            if(reference==IntPtr.Zero)throw new InvalidDataException("Loaded movie did not create a display object.");
            return Marshal.ReadIntPtr(reference);
        }
        internal bool IsStageObject(IntPtr value) {
            return value.ToInt64()>65536&&(value.ToInt64()&3)==1&&Marshal.ReadByte(IntPtr.Subtract(value,1))==36&&Marshal.ReadIntPtr(value,39)==IntPtr.Add(Player,stageInstanceOffset);
        }
        internal void CancelMovieLoads(MovieActivity.Scope scope) {if(imageLoader!=null)imageLoader.CancelScope(scope);}
        internal bool IsLoadedMovieRoot(IntPtr value) {return imageLoader!=null&&imageLoader.IsMovieRoot(value);}
        internal void StopMovieClip(IntPtr value,HashSet<IntPtr> seen) {
            if(value.ToInt64()<65536||(value.ToInt64()&3)!=1||Marshal.ReadByte(IntPtr.Subtract(value,1))!=36)return;
            IntPtr instance=Marshal.ReadIntPtr(value,39);
            Stack<IntPtr> pending=new Stack<IntPtr>();pending.Push(instance);
            while(pending.Count>0) {
                IntPtr item=pending.Pop();if(item==IntPtr.Zero||!seen.Add(item)||seen.Count>100000)continue;
                for(IntPtr child=Marshal.ReadIntPtr(item,16);child!=IntPtr.Zero;child=Marshal.ReadIntPtr(child,8))pending.Push(child);
                IntPtr definition=Marshal.ReadIntPtr(item,168);
                if(definition!=IntPtr.Zero&&Marshal.ReadByte(definition)==3)Marshal.WriteByte(item,185,(byte)(Marshal.ReadByte(item,185)&~1));
                if(Marshal.ReadByte(item,3)==7)for(int i=0;i<4;i++)pending.Push(Marshal.ReadIntPtr(Marshal.ReadIntPtr(item,72),8*i));
            }
        }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void StopMovieVoice(IntPtr player,int slot,IntPtr unused,int unused2);
        internal void StopMovieSounds(MovieActivity.Scope scope) {
            if(soundCompatibility!=null)soundCompatibility.StopMovie(scope);
            for(int slot=0;slot<8;slot++) {
                IntPtr voice=IntPtr.Add(Player,(modernDll?11648:11568)+56*slot),instance=Marshal.ReadIntPtr(voice,40);
                if(Marshal.ReadInt16(voice,36)<0)continue;
                for(int depth=0;instance!=IntPtr.Zero&&depth<256;depth++,instance=Marshal.ReadIntPtr(instance,128)) {
                    IntPtr reference=Marshal.ReadIntPtr(instance,32);MovieActivity.Scope owner;
                    if(reference!=IntPtr.Zero&&Activity.TryOwner(Marshal.ReadIntPtr(reference),out owner)&&MovieActivity.Within(owner,scope)) {
                        MovieFunction<StopMovieVoice>(0xa4e60,0xaa420)(Player,slot,IntPtr.Zero,0);break;
                    }
                }
            }
        }
        void DetachMovieClasses() {
            if(Player==IntPtr.Zero)return;
            foreach(IntPtr reference in movieClassReferences)Activity.FreeHostReference(reference);movieClassReferences.Clear();movieClassRoots.Clear();
        }
        void ReleaseMovieDefinitions() {
            foreach(IntPtr definition in movieDefinitions)Marshal.FreeHGlobal(definition);movieDefinitions.Clear();movieLibraries.Clear();movieDomains.Clear();ExecutionDomain=1;
        }
        void ReleaseMovieSymbolNames() {foreach(IntPtr text in movieSymbolNames)Marshal.FreeHGlobal(text);movieSymbolNames.Clear();}
    }
}
