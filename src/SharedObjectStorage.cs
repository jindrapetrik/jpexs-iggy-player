using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace IggyViewer {
    internal sealed class SharedObjectStorage {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int ExternalCall(IntPtr user,IntPtr player,IntPtr call);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int SetString(IntPtr path,IntPtr fastName,IntPtr name,IntPtr text,int length);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int SetBoolean(IntPtr path,IntPtr fastName,IntPtr name,int value);
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
            this.images=images;
            folder=Path.Combine(directory,Hash(Path.GetFullPath(native.FileName).ToUpperInvariant()));
            // The old DLL emits CESU-8 for surrogate pairs in its UTF-8 bridge.
            // The UTF-16 bridge preserves AS3 strings in both supplied builds.
            install=native.Bind<Native.VoidPP>("IggySetAS3ExternalFunctionCallbackUTF16");
            resultPath=native.Bind<Native.PtrP>("IggyPlayerCallbackResultPath");
            setString=native.Bind<SetString>("IggyValueSetStringUTF16RS");setBoolean=native.Bind<SetBoolean>("IggyValueSetBooleanRS");
            callback=Call;install(Marshal.GetFunctionPointerForDelegate(callback),IntPtr.Zero);
        }
        static string Argument(IntPtr call,int index) {
            int count=Marshal.ReadInt32(call,16);if(index<0||index>=count)throw new InvalidDataException("Missing SharedObject argument.");
            IntPtr value=IntPtr.Add(call,24+32*index);
            if(Marshal.ReadInt32(value)!=6)throw new InvalidDataException("SharedObject argument must be a string.");
            int length=Marshal.ReadInt32(value,24);if(length<0||length>2*1024*1024)throw new InvalidDataException("SharedObject argument is too large.");
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
                string key=Argument(call,0);if(key.Length==0||key.Length>2048)throw new InvalidDataException("Invalid SharedObject key.");
                string file=Path.Combine(folder,Hash(key)+".dat");
                if(name=="__iggy_shared_read") {
                    if(!File.Exists(file))return 1; // default result is null
                    if(new FileInfo(file).Length>8*1024*1024)throw new InvalidDataException("SharedObject file is too large.");
                    string text=File.ReadAllText(file,new UTF8Encoding(false,true));
                    IntPtr buffer=Marshal.StringToHGlobalUni(text);
                    try {if(setString(resultPath(player),IntPtr.Zero,IntPtr.Zero,buffer,text.Length)==0)throw new InvalidOperationException("Cannot return SharedObject data.");}
                    finally {Marshal.FreeHGlobal(buffer);}
                } else {
                    if(name=="__iggy_shared_write") {
                        string payload=Argument(call,1);if(!payload.StartsWith("IGGY1:",StringComparison.Ordinal))throw new InvalidDataException("Invalid SharedObject payload.");
                        Directory.CreateDirectory(folder);string temporary=file+"."+Guid.NewGuid().ToString("N").Substring(0,12)+".tmp";
                        try {
                            File.WriteAllText(temporary,payload,new UTF8Encoding(false,true));
                            if(File.Exists(file))File.Replace(temporary,file,null);else File.Move(temporary,file);
                        } finally {if(File.Exists(temporary))File.Delete(temporary);}
                    } else if(File.Exists(file))File.Delete(file);
                    setBoolean(resultPath(player),IntPtr.Zero,IntPtr.Zero,1);
                }
            } catch(Exception ex) {
                log.Write("SharedObject host operation failed: "+ex.Message);
                // False makes flush/clear throw an ordinary, catchable AS3 Error.
                if(name!="__iggy_shared_read")setBoolean(resultPath(player),IntPtr.Zero,IntPtr.Zero,0);
            }
            return 1;
        }
        internal void Detach() {install(IntPtr.Zero,IntPtr.Zero);GC.KeepAlive(callback);}
    }
}
