using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace IggyViewer {
    internal unsafe sealed partial class Native {
        sealed class DomainDefinition {internal IntPtr Metadata,Value;internal string Name;}
        sealed class MovieDomain {
            internal int Parent;
            internal readonly Dictionary<string,DomainDefinition> Definitions=new Dictionary<string,DomainDefinition>(StringComparer.Ordinal);
        }
        readonly List<MovieDomain> movieDomains=new List<MovieDomain>();
        internal int ExecutionDomain=1;
        internal int CurrentMovieDomain {get{return ExecutionDomain;}}
        static string DomainName(string name) {return name.StartsWith("::",StringComparison.Ordinal)?name.Substring(2):name.Replace("::",".");}
        static string ClassQName(string name) {int split=name.LastIndexOf('.');return split<0?"::"+name:name.Substring(0,split)+"::"+name.Substring(split+1);}
        void AddDomainDefinition(int domain,string name,IntPtr metadata) {
            string key=DomainName(name);
            if(metadata==IntPtr.Zero||movieDomains[domain].Definitions.ContainsKey(key))return;
            IntPtr reference=KeepMovieClass(metadata);
            movieDomains[domain].Definitions.Add(key,new DomainDefinition {Metadata=metadata,Value=reference,Name=name.StartsWith("::",StringComparison.Ordinal)?name.Substring(2):name});
        }
        internal void InitializeMovieDomains(Dictionary<string,IntPtr> builtins) {
            movieDomains.Add(new MovieDomain {Parent=-1});
            movieDomains.Add(new MovieDomain {Parent=0});
            IntPtr vm=Marshal.ReadIntPtr(Player,modernDll?10560:10480);
            HashSet<IntPtr> user=new HashSet<IntPtr>();
            foreach(string name in runtimeScripts.Classes) {
                IntPtr metadata=MovieLookup(vm,name);
                if(metadata==IntPtr.Zero)continue;
                user.Add(metadata);AddDomainDefinition(1,name,metadata);
                if(Activity!=null)Activity.PatchClass(metadata,1);
            }
            // Built-ins and injected compatibility classes form the system domain.
            HashSet<string> builtinNames=new HashSet<string>(runtimeScripts.ClassReferences,StringComparer.Ordinal);
            foreach(string name in new string[]{"::Object","::String","::Array","::Class","flash.display::Sprite","flash.display::MovieClip","flash.system::ApplicationDomain","flash.system::LoaderContext"})builtinNames.Add(name);
            foreach(string name in builtinNames) {
                string lookup=name;
                if(name=="flash.system::ApplicationDomain")lookup="com.jpexs.iggy.runtime::HostApplicationDomain";
                if(name=="flash.system::LoaderContext")lookup="com.jpexs.iggy.runtime::HostLoaderContext";
                if(name=="flash.display::Loader")lookup="com.jpexs.iggy.runtime::HostLoader";
                if(name=="flash.net::URLRequest")lookup="com.jpexs.iggy.runtime::HostURLRequest";
                IntPtr metadata=MovieLookup(vm,lookup);
                if(metadata==IntPtr.Zero)builtins.TryGetValue(name.Substring(name.LastIndexOf("::",StringComparison.Ordinal)+2),out metadata);
                if(metadata!=IntPtr.Zero&&!user.Contains(metadata))AddDomainDefinition(0,name,metadata);
            }
        }
        internal string MovieDomainCall(string operation,string value) {
            if(operation=="__iggy_image_domain_current")return CurrentMovieDomain.ToString();
            if(operation=="__iggy_image_domain_init") {
                string[] parts=value.Split('|');int parent,existing;
                if(parts.Length!=2||!int.TryParse(parts[0],out parent)||!ValidMovieDomain(parent)||!int.TryParse(parts[1],out existing))return "error|Invalid application domain";
                if(existing>=0)return ValidMovieDomain(existing)?existing.ToString():"error|Invalid application domain";
                operation="__iggy_image_domain_create";value=parent.ToString();
            }
            int domain;
            if(!int.TryParse(value,out domain)||domain<0||domain>=movieDomains.Count)return "error|Invalid application domain";
            if(operation=="__iggy_image_domain_create") {
                if(movieDomains.Count>=1024)return "error|Too many application domains";
                movieDomains.Add(new MovieDomain {Parent=domain});return (movieDomains.Count-1).ToString();
            }
            if(operation=="__iggy_image_domain_parent")return movieDomains[domain].Parent.ToString();
            if(operation=="__iggy_image_domain_names") {
                List<string> names=new List<string>();foreach(DomainDefinition definition in movieDomains[domain].Definitions.Values)names.Add(definition.Name);
                names.Sort(StringComparer.Ordinal);return string.Join("|",names.ToArray());
            }
            return "error|Unknown application domain operation";
        }
        internal bool ValidMovieDomain(int domain) {return domain>=0&&domain<movieDomains.Count;}
        DomainDefinition FindDomainDefinition(int domain,string name) {
            if(!ValidMovieDomain(domain))return null;
            MovieDomain current=movieDomains[domain];
            DomainDefinition inherited=current.Parent<0?null:FindDomainDefinition(current.Parent,name);
            if(inherited!=null)return inherited;
            DomainDefinition local;return current.Definitions.TryGetValue(DomainName(name),out local)?local:null;
        }
        internal IntPtr MovieDomainDefinition(int domain,string name) {
            DomainDefinition definition=FindDomainDefinition(domain,name);
            if(definition==null)return IntPtr.Zero;
            if(definition.Value==IntPtr.Zero) {
                definition.Value=KeepMovieClass(definition.Metadata);
                if(definition.Value==IntPtr.Zero)return IntPtr.Zero;
            }
            return Marshal.ReadIntPtr(definition.Value);
        }
    }
}
