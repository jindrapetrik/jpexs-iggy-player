using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace IggyViewer {
    internal unsafe sealed partial class Native {
        // Keep native Sound instances and embedded definitions intact. Scripted
        // loading/event state is invoked on the current, GC-rooted VM stack.
        readonly List<ByteArrayCall> soundLoadCallbacks=new List<ByteArrayCall>();
        static ByteArrayCall SoundCallback(IntPtr pointer) {
            Delegate callback=Marshal.GetDelegateForFunctionPointer(pointer,typeof(ByteArrayCall));
            return callback as ByteArrayCall??(ByteArrayCall)Delegate.CreateDelegate(typeof(ByteArrayCall),callback.Target,callback.Method);
        }
        internal void InstallSoundLoading(IntPtr metadata) {
            ByteArrayCall original=SoundCallback(Marshal.ReadIntPtr(metadata,56));
            ByteArrayCall constructor=context=> {
                IntPtr vm=Marshal.ReadIntPtr(context);
                if(Marshal.ReadIntPtr(vm,modernDll?10488:10464)!=IntPtr.Zero){IntPtr error=original(context);if(error!=IntPtr.Zero)return error;}
                return InvokeSoundLoading(context,"construct",true);
            };
            soundLoadCallbacks.Add(constructor);Marshal.WriteIntPtr(metadata,56,Marshal.GetFunctionPointerForDelegate(constructor));
            IntPtr traits=Marshal.ReadIntPtr(metadata,72);
            foreach(int table in new int[]{64,72}) {
                int count=(ushort)Marshal.ReadInt16(traits,table==64?22:26);IntPtr entries=Marshal.ReadIntPtr(traits,table);
                for(int i=0;i<count;i++) {
                    IntPtr function=Marshal.ReadIntPtr(entries,8*i);if(table==64)function=IntPtr.Subtract(function,1);
                    if(function.ToInt64()<65536||Marshal.ReadByte(function)!=16)continue;
                    IntPtr name=Marshal.ReadIntPtr(function,8);string text=Marshal.PtrToStringUni(IntPtr.Add(name,52),Marshal.ReadInt32(name,48));
                    if(table==64&&text!="load"&&text!="close"&&text!="play"||table==72&&text!="bytesLoaded"&&text!="bytesTotal"&&text!="length"&&text!="url"&&text!="isBuffering"&&text!="isURLInaccessible")continue;
                    ByteArrayCall previous=SoundCallback(Marshal.ReadIntPtr(function,16));string operation=text;
                    ByteArrayCall callback=context=> {
                        IntPtr definition=Marshal.ReadIntPtr(ByteArrayReceiver(context),55);
                        if(operation=="play"){if(definition!=IntPtr.Zero)return previous(context);Marshal.WriteIntPtr(Marshal.ReadIntPtr(context,8),IntPtr.Add(module,modernDll?0x103979:0xf3959));return IntPtr.Zero;}
                        if(operation=="length") {if(definition!=IntPtr.Zero)return previous(context);Marshal.WriteIntPtr(Marshal.ReadIntPtr(context,8),IntPtr.Zero);return IntPtr.Zero;}
                        if((operation=="bytesLoaded"||operation=="bytesTotal")&&definition!=IntPtr.Zero&&!imageLoader.IsExternalSound(definition))return previous(context);
                        return InvokeSoundLoading(context,operation,operation=="load");
                    };
                    soundLoadCallbacks.Add(callback);Marshal.WriteIntPtr(function,16,Marshal.GetFunctionPointerForDelegate(callback));
                }
            }
        }
        IntPtr InvokeSoundLoading(IntPtr context,string operation,bool arguments) {
            IntPtr vm=Marshal.ReadIntPtr(context);int offset=modernDll?27448:27416,count=arguments?Math.Min(2,Marshal.ReadInt32(context,16))+1:1;
            IntPtr stack=Marshal.ReadIntPtr(vm,offset),source=Marshal.ReadIntPtr(context,24);
            Marshal.WriteIntPtr(stack,IntPtr.Zero);for(int i=0;i<count;i++)Marshal.WriteIntPtr(stack,8*(i+1),Marshal.ReadIntPtr(source,8*i));
            Marshal.WriteIntPtr(vm,offset,IntPtr.Add(stack,8*(count+1)));
            try {
                IntPtr name=MovieName(vm,"com.jpexs.iggy.runtime::HostSoundLoading",true);
                Marshal.WriteIntPtr(stack,MovieFunction<MovieGlobal>(0x79990,0x7ff90)(vm,name));
                Marshal.WriteIntPtr(stack,MovieFunction<MovieProperty>(0xaf4d0,0xb6b90)(vm,Marshal.ReadIntPtr(stack),name));
                IntPtr member=MovieName(vm,"::"+operation,true),function=MovieFunction<MovieProperty>(0xaf4d0,0xb6b90)(vm,Marshal.ReadIntPtr(stack),member);
                MovieFunction<AmfInvoke>(0x794c0,0x7fbd0)(vm,Marshal.ReadIntPtr(context,8),function,stack,count,-1);
                return Marshal.ReadIntPtr(vm,modernDll?10528:10504)==IntPtr.Zero?IntPtr.Zero:Marshal.ReadIntPtr(vm,modernDll?10552:10528);
            } finally {Marshal.WriteIntPtr(vm,offset,stack);}
        }
    }
}
