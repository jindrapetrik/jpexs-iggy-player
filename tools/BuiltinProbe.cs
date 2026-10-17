using System;
using System.IO;
using System.Runtime.InteropServices;

namespace IggyViewer {
    internal static class BuiltinProbe {
        [DllImport("kernel32",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
        static void Dump(IntPtr metadata,IntPtr module,Log log,string kind) {
            foreach(int table in new int[]{64,72,80}) {
                // method/getter/setter counts: +22/+26/+24, pointers +64/+72/+80.
                int count=(ushort)Marshal.ReadInt16(metadata,table==64?22:table==72?26:24);
                if(count>500)throw new Exception("Bad table");
                IntPtr values=Marshal.ReadIntPtr(metadata,table);
                for(int i=0;i<count;i++) {
                    IntPtr function=Marshal.ReadIntPtr(values,8*i);
                    if(function.ToInt64()<65536)continue;
                    if(table==64)function=IntPtr.Subtract(function,1);
                    byte type=Marshal.ReadByte(function);
                    if(type!=16){log.Write(kind+" "+table+"["+i+"] type="+type);continue;}
                    IntPtr name=Marshal.ReadIntPtr(function,8);string text="?";
                    if(name!=IntPtr.Zero) {
                        int size=Marshal.ReadInt32(name,48);
                        if(size>=0&&size<200)text=Marshal.PtrToStringUni(IntPtr.Add(name,52),size);
                    }
                    log.Write(kind+" "+table+"["+i+"] "+text+" callback="+(Marshal.ReadIntPtr(function,16).ToInt64()-module.ToInt64()).ToString("x"));
                }
            }
        }
        static int Main(string[] args) {
            Native.SetErrorMode(3);Log log=new Log(args[2]);
            using(Native native=new Native(args[0],log,true,false)) {
                native.Open(args[1],550,550);bool modern=native.Version.StartsWith("Iggy version 1.2.56\n");
                IntPtr module=GetModuleHandle(Path.GetFileName(args[0]));
                IntPtr vm=Marshal.ReadIntPtr(native.Player,modern?10560:10480);
                int count=Marshal.ReadInt32(vm,modern?9648:9624);
                for(int i=0;i<count;i++) {
                    IntPtr entry=IntPtr.Add(vm,(modern?3648:3624)+24*i);
                    string name=Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(entry));
                    if(Array.IndexOf(new string[]{"Mouse","SoundChannel","SoundTransform","Sound","SoundMixer","SharedObject","Loader","URLRequest","URLLoader","EventDispatcher","IOErrorEvent","TextEvent","Transform","PerspectiveProjection","MovieClip"},name)<0)continue;
                    IntPtr cls=Marshal.ReadIntPtr(entry,16);
                    log.Write("CLASS "+name+" ctor="+(Marshal.ReadIntPtr(cls,56).ToInt64()-module.ToInt64()).ToString("x"));
                    Dump(Marshal.ReadIntPtr(cls,72),module,log,"instance");
                    Dump(Marshal.ReadIntPtr(cls,80),module,log,"static");
                }
            }
            return 0;
        }
    }
}
