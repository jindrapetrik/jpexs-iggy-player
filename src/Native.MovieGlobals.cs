using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace IggyViewer {
    internal unsafe sealed partial class Native {
        sealed class GlobalScript {internal string Anchor;internal int Domain;internal bool Function;}
        readonly Dictionary<string,GlobalScript> globalScripts=new Dictionary<string,GlobalScript>(StringComparer.Ordinal);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void MovieSetProperty(IntPtr vm,IntPtr owner,IntPtr name,IntPtr value,int initialize);
        void RegisterGlobalScripts(byte[] source,int domain=1) {
            foreach(byte[] abc in Container.ReadAbcs(source)) {
                RuntimeAbc parsed=new RuntimeAbc(abc);
                foreach(KeyValuePair<string,string> pair in parsed.GlobalScripts)
                    if(!globalScripts.ContainsKey(pair.Key))globalScripts.Add(pair.Key,new GlobalScript {Anchor=pair.Value,Domain=domain,Function=parsed.GlobalFunctions.Contains(pair.Key)});
            }
        }
        void PublishGlobalScripts(IntPtr vm) {
            foreach(KeyValuePair<string,GlobalScript> pair in globalScripts) {
                IntPtr metadata=MovieLookup(vm,pair.Value.Anchor);if(metadata==IntPtr.Zero)continue;
                IntPtr script=Marshal.ReadIntPtr(metadata,104),name=MovieName(vm,pair.Key,true);
                if(script==IntPtr.Zero||name.ToInt64()<=1)continue;
                MovieFunction<MovieSetProperty>(0xb0860,0xb6d20)(vm,Marshal.ReadIntPtr(vm,112),name,script,1);
                // The method table exists before the script initializer runs.
                // Patch its function without triggering user code, so lexical
                // calls from another domain also execute in the defining one.
                if(pair.Value.Function&&Activity!=null)Activity.PatchGlobal(MovieFunction<MovieProperty>(0xaf4d0,0xb6b90)(vm,script,name),pair.Value.Domain);
            }
        }
    }
}
