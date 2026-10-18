using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace IggyViewer {
    internal sealed class SharedObjectStorage {
        const int PayloadLimit=24*1024*1024;
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int ExternalCall(IntPtr user,IntPtr player,IntPtr call);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int SetString(IntPtr path,IntPtr fastName,IntPtr name,IntPtr text,int length);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int SetBoolean(IntPtr path,IntPtr fastName,IntPtr name,int value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr FastName(IntPtr player,IntPtr text,int length);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void ReferencePath(IntPtr output,IntPtr player,IntPtr reference);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate uint CallMethod(IntPtr player,IntPtr result,IntPtr receiver,IntPtr name,int count,IntPtr args);
        sealed class Store {internal IntPtr Reference;internal string Key;internal int Domain;internal MovieActivity.Scope Scope;}
        readonly List<Store> stores=new List<Store>();
        readonly HashSet<string> tracked=new HashSet<string>(StringComparer.Ordinal);
        readonly ReferencePath referencePath;
        readonly CallMethod callMethod;
        readonly IntPtr saveName;
        readonly Native native;
        readonly Log log;
        readonly ExternalCall callback;
        readonly Native.VoidPP install;
        readonly Native.PtrP resultPath;
        readonly SetString setString;
        readonly SetBoolean setBoolean;
        readonly string folder;
        readonly ImageLoader images;
        static string Hash(string text) {using(SHA256 sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").Substring(0,32);}
        internal SharedObjectStorage(Native native,string directory,Log log,ImageLoader images=null) {
            this.native=native;this.log=log;
            this.images=images;if(images!=null)images.Storage=this;
            folder=Path.Combine(directory,Hash(Path.GetFullPath(native.FileName).ToUpperInvariant()));
            // The old DLL emits CESU-8 for surrogate pairs in its UTF-8 bridge.
            // The UTF-16 bridge preserves AS3 strings in both supplied builds.
            install=native.Bind<Native.VoidPP>("IggySetAS3ExternalFunctionCallbackUTF16");
            resultPath=native.Bind<Native.PtrP>("IggyPlayerCallbackResultPath");
            setString=native.Bind<SetString>("IggyValueSetStringUTF16RS");setBoolean=native.Bind<SetBoolean>("IggyValueSetBooleanRS");
            callback=Call;install(Marshal.GetFunctionPointerForDelegate(callback),IntPtr.Zero);
            referencePath=native.Bind<ReferencePath>("IggyValuePathFromRef");callMethod=native.Bind<CallMethod>("IggyPlayerCallMethodRS");
            IntPtr text=Marshal.StringToHGlobalAnsi("__iggyAutoSave");
            try {saveName=native.Bind<FastName>("IggyPlayerCreateFastNameUTF8")(native.Player,text,14);}finally {Marshal.FreeHGlobal(text);}
        }
        internal void Track(IntPtr value,string key) {
            if(tracked.Contains(key))return;
            if(stores.Count>=100000)throw new InvalidDataException("Too many SharedObjects.");
            stores.Add(new Store {Reference=native.Activity.HostReference(value,1),Key=key,Domain=native.ExecutionDomain,Scope=native.Activity.Current});tracked.Add(key);
        }
        internal void AutoSave() {
            IntPtr path=Marshal.AllocHGlobal(40);
            int previous=native.ExecutionDomain;MovieActivity.Scope scope=native.Activity.Current;
            try {
                // Serialization callbacks can open another SharedObject. Visit
                // newly registered stores too, subject to the tracking limit.
                for(int i=0;i<stores.Count;i++) {
                    Store store=stores[i];if(Marshal.ReadIntPtr(store.Reference).ToInt64()==7)continue;
                    native.ExecutionDomain=store.Domain;native.Activity.Current=store.Scope;
                    referencePath(path,native.Player,store.Reference);
                    uint status=callMethod(native.Player,IntPtr.Zero,path,saveName,0,IntPtr.Zero);
                    if(status!=0)log.Write("SharedObject automatic save failed: "+store.Key+" (native status "+status+")");
                }
            } finally {native.ExecutionDomain=previous;native.Activity.Current=scope;Marshal.FreeHGlobal(path);}
        }
        static string Argument(IntPtr call,int index,int maximum=2*1024*1024) {
            int count=Marshal.ReadInt32(call,16);if(index<0||index>=count)throw new InvalidDataException("Missing SharedObject argument.");
            IntPtr value=IntPtr.Add(call,24+32*index);
            if(Marshal.ReadInt32(value)!=6)throw new InvalidDataException("SharedObject argument must be a string.");
            int length=Marshal.ReadInt32(value,24);if(length<0||length>maximum)throw new InvalidDataException("SharedObject argument is too large.");
            return Marshal.PtrToStringUni(Marshal.ReadIntPtr(value,16),length);
        }
        int Call(IntPtr user,IntPtr player,IntPtr call) {
            string name=Marshal.PtrToStringUni(Marshal.ReadIntPtr(call));
            try {
                if(player!=native.Player)throw new InvalidDataException("Wrong SharedObject movie owner.");
                if(images!=null&&name.StartsWith("__iggy_image_",StringComparison.Ordinal)) {
                    int count=Marshal.ReadInt32(call,16);
                    string result=images.Call(name,Argument(call,0),count>1?Argument(call,1):null,count>2?Argument(call,2):null,count>3?Argument(call,3):null);
                    IntPtr buffer=Marshal.StringToHGlobalUni(result);
                    try {setString(resultPath(player),IntPtr.Zero,IntPtr.Zero,buffer,result.Length);}finally {Marshal.FreeHGlobal(buffer);}
                    return 1;
                }
                if(name!="__iggy_shared_read"&&name!="__iggy_shared_write"&&name!="__iggy_shared_clear") {
                    log.Write("ExternalInterface function unavailable in standalone player: "+name);return 1;
                }
                object answer=Execute(name.Substring("__iggy_shared_".Length),Argument(call,0),name=="__iggy_shared_write"?Argument(call,1,PayloadLimit):null);
                if(answer is string) {
                    string text=(string)answer;IntPtr buffer=Marshal.StringToHGlobalUni(text);
                    try {if(setString(resultPath(player),IntPtr.Zero,IntPtr.Zero,buffer,text.Length)==0)throw new InvalidOperationException("Cannot return SharedObject data.");}
                    finally {Marshal.FreeHGlobal(buffer);}
                } else if(answer is bool)setBoolean(resultPath(player),IntPtr.Zero,IntPtr.Zero,(bool)answer?1:0);
            } catch(Exception ex) {
                log.Write("SharedObject host operation failed: "+ex.Message);
                // Distinguish read failures from missing files. A failed read
                // must not become empty data that a later flush overwrites.
                setBoolean(resultPath(player),IntPtr.Zero,IntPtr.Zero,0);
            }
            return 1;
        }
        // The LoaderInfo request bridge keeps large payloads off the DLL's
        // ExternalInterface stack, which copies entire string arguments there.
        internal object Execute(string operation,string key,string payload) {
            try {
                if(key.Length==0||key.Length>2048)throw new InvalidDataException("Invalid SharedObject key.");
                if(operation=="error") {log.Write("SharedObject automatic save failed: "+key+". The previous save is preserved.");return false;}
                string file=Path.Combine(folder,Hash(key)+".dat");
                if(operation=="read") {
                    if(!File.Exists(file))return null;
                    if(new FileInfo(file).Length>PayloadLimit)throw new InvalidDataException("SharedObject file is too large.");
                    return File.ReadAllText(file,new UTF8Encoding(false,true));
                }
                if(operation=="write") {
                    if(payload==null||(!payload.StartsWith("IGGY2:0:",StringComparison.Ordinal)&&!payload.StartsWith("IGGY2:3:",StringComparison.Ordinal)))throw new InvalidDataException("Invalid SharedObject payload.");
                    if(payload.Length>22369632||Convert.FromBase64String(payload.Substring(8)).Length>16*1024*1024)throw new InvalidDataException("SharedObject AMF data exceeds 16 MiB.");
                    SaveText(file,payload);
                } else if(operation=="clear") {if(File.Exists(file))File.Delete(file);}
                else throw new InvalidDataException("Unknown SharedObject operation.");
                return true;
            } catch(Exception ex) {log.Write("SharedObject host operation failed: "+ex.Message);return false;}
        }
        internal static void SaveText(string file,string payload) {
            Directory.CreateDirectory(Path.GetDirectoryName(file));string temporary=file+"."+Guid.NewGuid().ToString("N").Substring(0,12)+".tmp";
            try {
                File.WriteAllText(temporary,payload,new UTF8Encoding(false,true));
                for(int attempt=0;;attempt++) {
                    try {if(File.Exists(file))File.Replace(temporary,file,null);else File.Move(temporary,file);break;}
                    catch(IOException error) {
                        int code=Marshal.GetHRForException(error)&0xffff;
                        // Indexers/scanners can hold a file briefly without delete
                        // sharing. Keep atomic replacement and bound the retry.
                        if(attempt>=5||(code!=32&&code!=33&&code!=1175))throw;
                        Thread.Sleep(25*(attempt+1));
                    }
                }
            } finally {if(File.Exists(temporary))File.Delete(temporary);}
        }
        internal void Detach() {
            foreach(Store store in stores)native.Activity.FreeHostReference(store.Reference);stores.Clear();tracked.Clear();
            install(IntPtr.Zero,IntPtr.Zero);if(images!=null)images.Storage=null;GC.KeepAlive(callback);
        }
    }
}
