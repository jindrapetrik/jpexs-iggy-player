using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace IggyViewer {
    internal unsafe sealed partial class Native {
        sealed class DomainLibrary {
            internal int Id;
            internal string Url;
            internal byte[] Source;
            internal Dictionary<string,string> ClassNames;
            internal List<DomainLibrary> Dependencies;
        }
        readonly Dictionary<string,DomainLibrary> domainLibraries=new Dictionary<string,DomainLibrary>(StringComparer.OrdinalIgnoreCase);

        byte[] PrepareDomainMovie(byte[] original,RuntimeScriptPlan plan,Dictionary<string,string> names,Dictionary<string,string> imports) {
            byte[] prepared=plan.Apply(original,false,log);List<byte[]> abcs=new List<byte[]>();
            foreach(byte[] abc in Container.ReadAbcs(prepared))abcs.Add(new RuntimeAbc(abc).RenameClasses(names));
            return Container.Prepare(Container.ReplaceAbcs(prepared,abcs,log,imports),log);
        }
        Dictionary<string,string> RegisterDomainImports(string from,byte[] source,int domain,RuntimeScriptPlan plan,Dictionary<string,string> names,List<DomainLibrary> ordered,HashSet<string> visiting,MovieResources resources=null) {
            from=Path.GetFullPath(from);if(!visiting.Add(from))throw new InvalidDataException("Cyclic Iggy imports: "+from);
            Dictionary<string,string> urls=new Dictionary<string,string>(StringComparer.Ordinal);
            try {
                foreach(string url in Container.Imports(source)) {
                    string file=resources==null?ResolveImport(from,url):resources.Resolve(from,url);
                    if(file==null){log.Write("Missing import: "+url+" (requested by "+from+")");continue;}
                    if(visiting.Contains(file))throw new InvalidDataException("Cyclic Iggy imports: "+file);
                    byte[] original=resources==null?File.ReadAllBytes(file):resources.Movies[file].Bytes;string key;
                    using(SHA256 sha=SHA256.Create())key=domain+"|"+file+"|"+(resources==null?"":resources.Movies[file].Key)+"|"+BitConverter.ToString(sha.ComputeHash(original));
                    DomainLibrary library;
                    if(!domainLibraries.TryGetValue(key,out library)) {
                        Dictionary<string,string> dependencies=RegisterDomainImports(file,original,domain,plan,names,ordered,visiting,resources);
                        if(libraries.Count>=32)throw new IOException("The native runtime's 32-library limit has been reached.");
                        string privateUrl="__iggy_domain_"+domain+"_"+Guid.NewGuid().ToString("N")+".iggy";
                        byte[] bytes=PrepareDomainMovie(original,plan,names,dependencies),name=Encoding.UTF8.GetBytes(privateUrl+"\0");
                        RegisterGlobalScripts(bytes,domain);
                        byte* options=stackalloc byte[80];for(int j=0;j<80;j++)options[j]=0;
                        int id;fixed(byte* data=bytes)fixed(byte* text=name)id=createLibrary((IntPtr)text,(IntPtr)data,(uint)bytes.Length,(IntPtr)options);
                        if(id<0||id>31)throw new InvalidDataException("DLL rejected domain import: "+url);
                        libraries.Add(id);IntPtr generic=libraryGeneric(id);
                        NormalizeTimelineMatrices(generic);NormalizeEmptyStyleTables(generic);NormalizeAudio(generic);InstallTextures(generic,file,resources);
                        foreach(string font in Container.EmbeddedFonts(bytes))embeddedFonts.Add(font);
                        library=new DomainLibrary {Id=id,Url=privateUrl,Source=original,ClassNames=names,Dependencies=new List<DomainLibrary>(ordered)};domainLibraries.Add(key,library);
                        log.Write("Registered domain "+domain+" import "+url+" from "+file);
                    }
                    foreach(DomainLibrary dependency in library.Dependencies)if(!ordered.Contains(dependency))ordered.Add(dependency);
                    if(!ordered.Contains(library))ordered.Add(library);
                    urls[url]=library.Url;
                }
                return urls;
            } finally {visiting.Remove(from);}
        }
        void RenameMovieSymbols(IntPtr generic,Dictionary<string,string> names) {
            int count=Marshal.ReadInt32(generic,560);IntPtr symbols=Marshal.ReadIntPtr(generic,552);
            if(count<0||count>65536)throw new InvalidDataException("Invalid domain symbol table.");
            for(int i=0;i<count;i++) {
                IntPtr symbol=IntPtr.Add(symbols,24*i);string name=Marshal.PtrToStringUni(Marshal.ReadIntPtr(symbol,8)),replacement;
                if(name==null||!names.TryGetValue(name.IndexOf("::",StringComparison.Ordinal)>=0?name:ClassQName(name),out replacement))continue;
                SetMovieClassName(symbol,8,replacement);
            }
            HashSet<IntPtr> visited=new HashSet<IntPtr>();RenameTimelineClasses(Marshal.ReadIntPtr(generic,128),names,visited);
            int definitions=Marshal.ReadInt32(generic,160);IntPtr dictionary=Marshal.ReadIntPtr(generic,152);
            if(definitions<0||definitions>65536)throw new InvalidDataException("Invalid domain dictionary.");
            for(int i=0;i<definitions;i++) {
                IntPtr definition=Marshal.ReadIntPtr(dictionary,8*i);
                if(definition!=IntPtr.Zero&&Marshal.ReadByte(definition)==3)RenameTimelineClasses(Marshal.ReadIntPtr(definition,56),names,visited);
            }
        }
        void SetMovieClassName(IntPtr owner,int offset,string name) {
            IntPtr text=Marshal.StringToHGlobalUni(DomainName(name));movieSymbolNames.Add(text);Marshal.WriteIntPtr(owner,offset,text);
        }
        void RenameTimelineClasses(IntPtr command,Dictionary<string,string> names,HashSet<IntPtr> visited) {
            while(command!=IntPtr.Zero&&visited.Add(command)) {
                if(visited.Count>1000000)throw new InvalidDataException("Invalid domain timeline.");
                int type=Marshal.ReadByte(command,8),flags=Marshal.ReadByte(command,9);
                int field=type==2&&(flags&40)!=0?80:type==6?16:-1;
                if(field>=0) {
                    IntPtr pointer=Marshal.ReadIntPtr(command,field);string replacement;
                    if(pointer!=IntPtr.Zero) {
                        string name=Marshal.PtrToStringUni(pointer);
                        if(names.TryGetValue(name.IndexOf("::",StringComparison.Ordinal)>=0?name:ClassQName(name),out replacement))SetMovieClassName(command,field,replacement);
                    }
                }
                command=Marshal.ReadIntPtr(command);
            }
        }
        void AddMovieDomainDefinitions(int domain,byte[] source,Dictionary<string,string> names,IntPtr vm) {
            foreach(byte[] abc in Container.ReadAbcs(source)) {
                RuntimeAbc parsed=new RuntimeAbc(abc);
                HashSet<string> definitions=new HashSet<string>(parsed.Classes,StringComparer.Ordinal);definitions.UnionWith(parsed.PublicGlobals);
                foreach(string name in definitions) {
                    if(FindDomainDefinition(domain,name)!=null)continue;
                    bool global=!parsed.Classes.Contains(name);string link;
                    names.TryGetValue(name,out link);
                    AddDomainDefinition(domain,name,global?IntPtr.Zero:MovieLookup(vm,link??name),link??name,global:global);
                }
            }
        }
    }
}
