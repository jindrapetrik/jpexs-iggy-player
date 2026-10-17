using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace IggyViewer {
    // VM closures are replaced per movie; native code and serialized ABC stay
    // unchanged. All AS3 event delivery runs on the movie thread after a tick.
    internal unsafe sealed class SoundCompatibility {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr Builtin(IntPtr context);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int ParsePlay(IntPtr context,IntPtr format,ref float start,ref int loops,IntPtr transformClass,ref IntPtr transform);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr Number(IntPtr vm,double value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr Error(IntPtr vm,int code);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr Reference(IntPtr player,IntPtr value,int weak);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr FastName(IntPtr player,IntPtr text,int length);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate uint CallMethod(IntPtr player,IntPtr result,IntPtr receiver,IntPtr name,int count,IntPtr args);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr GlobalReference(IntPtr player,IntPtr name);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr LookupGlobal(IntPtr vm,IntPtr name);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr ReleaseInternalReference(IntPtr vm,IntPtr value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void ReferencePath(IntPtr output,IntPtr player,IntPtr reference);
        sealed class Channel {internal IntPtr Reference;internal int Slot;internal float Duration;internal MovieActivity.Scope Owner;}
        readonly List<Channel> channels=new List<Channel>();
        readonly IntPtr player,vm,nullValue,completionName;
        readonly int slotOffset,stackOffset;
        readonly Builtin originalPlay,originalStop,originalStopAll,play,stop,stopAll;
        readonly ParsePlay parse;
        readonly Number number;
        readonly Error error;
        readonly Reference reference;
        readonly Native.VoidPP free;
        readonly CallMethod call;
        readonly GlobalReference global;
        readonly LookupGlobal legacyGlobal;
        readonly ReleaseInternalReference releaseInternal;
        readonly IntPtr completionClassName;
        readonly ReferencePath path;
        IntPtr completionClass;
        readonly bool modern;
        readonly Native native;
        internal int Active {get{return channels.Count;}}

        internal SoundCompatibility(Native native,IntPtr module,bool modern,Log log) {
            this.native=native;player=native.Player;this.modern=modern;slotOffset=modern?11648:11568;stackOffset=modern?27448:27416;
            vm=Marshal.ReadIntPtr(player,modern?10560:10480);
            if(vm==IntPtr.Zero)return;
            nullValue=IntPtr.Add(module,modern?0x103979:0xF3959);
            parse=Function<ParsePlay>(module,modern?0xb2d60:0xb8d40);
            number=Function<Number>(module,modern?0x449a0:0x46be0);
            error=Function<Error>(module,modern?0x79930:0x7c7a0);
            reference=native.Bind<Reference>("IggyValueRefFromTempRef");free=native.Bind<Native.VoidPP>("IggyValueRefFree");
            releaseInternal=Function<ReleaseInternalReference>(module,modern?0x65980:0x681f0);
            call=native.Bind<CallMethod>("IggyPlayerCallMethodRS");path=native.Bind<ReferencePath>("IggyValuePathFromRef");
            if(modern)global=native.Bind<GlobalReference>("IggyValueRefGetGlobal");
            else legacyGlobal=Function<LookupGlobal>(module,0x80010);
            IntPtr text=Marshal.StringToHGlobalAnsi("complete");
            try {completionName=native.Bind<FastName>("IggyPlayerCreateFastNameUTF8")(player,text,8);}finally {Marshal.FreeHGlobal(text);}
            text=Marshal.StringToHGlobalAnsi("__IggyHostSoundEvents");
            try {completionClassName=native.Bind<FastName>("IggyPlayerCreateFastNameUTF8")(player,text,"__IggyHostSoundEvents".Length);}finally {Marshal.FreeHGlobal(text);}
            play=Play;stop=Stop;stopAll=StopAll;
            int count=Marshal.ReadInt32(vm,modern?9648:9624),patched=0;
            if(count<1||count>250)throw new InvalidDataException("Unexpected sound class registry.");
            for(int i=0;i<count;i++) {
                IntPtr entry=IntPtr.Add(vm,(modern?3648:3624)+24*i),metadata=Marshal.ReadIntPtr(entry,16);
                string name=Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(entry));
                if(name=="Sound"){originalPlay=Install(module,metadata,false,"play",modern?0xa4c70:0xa9e40,play);patched++;}
                if(name=="SoundChannel"){originalStop=Install(module,metadata,false,"stop",modern?0xa4750:0xaa790,stop);patched++;}
                if(name=="SoundMixer"){originalStopAll=Install(module,metadata,true,"stopAll",modern?0xa4860:0xaa4a0,stopAll);patched++;}
            }
            if(patched!=3)throw new InvalidDataException("Cannot locate native sound methods.");
            log.Write("Sound compatibility: Flash loop counts, asynchronous SOUND_COMPLETE and stopped-channel lifetime.");
        }
        static T Function<T>(IntPtr module,int offset) where T:class {return (T)(object)Marshal.GetDelegateForFunctionPointer(IntPtr.Add(module,offset),typeof(T));}
        static Builtin Install(IntPtr module,IntPtr metadata,bool isStatic,string name,int offset,Builtin replacement) {
            IntPtr traits=Marshal.ReadIntPtr(metadata,isStatic?80:72),methods=Marshal.ReadIntPtr(traits,64);
            int count=(ushort)Marshal.ReadInt16(traits,22);if(count>256)throw new InvalidDataException("Unexpected sound method table.");
            for(int i=0;i<count;i++) {
                IntPtr tagged=Marshal.ReadIntPtr(methods,8*i);if(tagged.ToInt64()<65536)continue;
                IntPtr function=IntPtr.Subtract(tagged,1);if(Marshal.ReadByte(function)!=16)continue;
                IntPtr text=Marshal.ReadIntPtr(function,8);int size=Marshal.ReadInt32(text,48);
                if(size<0||size>200)throw new InvalidDataException("Invalid sound method name.");
                if(Marshal.PtrToStringUni(IntPtr.Add(text,52),size)!=name)continue;
                if(Marshal.ReadIntPtr(function,16)!=IntPtr.Add(module,offset))throw new InvalidDataException("Unexpected Sound callback: "+name);
                Builtin original=Function<Builtin>(module,offset);
                Marshal.WriteIntPtr(function,16,Marshal.GetFunctionPointerForDelegate(replacement));return original;
            }
            throw new InvalidDataException("Missing sound method: "+name);
        }
        IntPtr Play(IntPtr context) {
            MovieActivity.Scope owner=native.Activity==null?null:native.Activity.Current??native.Activity.OwnerOf(Marshal.ReadIntPtr(Marshal.ReadIntPtr(context,24)));
            if(owner!=null&&owner.Stopped){Marshal.WriteIntPtr(Marshal.ReadIntPtr(context,8),nullValue);return IntPtr.Zero;}
            float start=0;int loops=0;IntPtr transform=nullValue,format=Marshal.StringToHGlobalAnsi("?Fd!O");int status;
            try {status=parse(context,format,ref start,ref loops,Marshal.ReadIntPtr(vm,modern?9960:9936),ref transform);}finally {Marshal.FreeHGlobal(format);}
            if(status!=0)return error(vm,120);
            // Conversion can run valueOf and GC. Forward primitive values to
            // the native implementation so those user conversions occur once.
            IntPtr saved=Marshal.ReadIntPtr(vm,stackOffset);IntPtr* args=(IntPtr*)saved;
            args[0]=Marshal.ReadIntPtr(Marshal.ReadIntPtr(context,24));args[1]=transform;
            Marshal.WriteIntPtr(vm,stackOffset,IntPtr.Add(saved,16));
            try {
                IntPtr startValue=number(vm,start);
                args[3]=args[1];args[1]=startValue;args[2]=IntPtr.Zero;
                int total=loops<0?1:loops==int.MaxValue?int.MaxValue:loops+1;
                Marshal.WriteIntPtr(vm,stackOffset,IntPtr.Add(saved,32));
                args[2]=number(vm,total);
                IntPtr* forwarded=stackalloc IntPtr[4];for(int i=0;i<4;i++)forwarded[i]=Marshal.ReadIntPtr(context,8*i);
                forwarded[2]=new IntPtr(3);forwarded[3]=saved;
                IntPtr result=originalPlay((IntPtr)forwarded),channel=Marshal.ReadIntPtr(Marshal.ReadIntPtr(context,8));
                if(channel!=nullValue&&channel!=IntPtr.Zero&&(channel.ToInt64()&3)==1) {
                    int slot=Marshal.ReadInt32(channel,55);
                    if(slot>=0&&slot<8)channels.Add(new Channel {Reference=reference(player,channel,0),Slot=slot,Duration=*((float*)((byte*)channel+59)),Owner=owner});
                }
                return result;
            } finally {Marshal.WriteIntPtr(vm,stackOffset,saved);}
        }
        IntPtr Slot(Channel channel) {return IntPtr.Add(player,slotOffset+56*channel.Slot);}
        void Release(Channel channel) {channels.Remove(channel);if(channel.Reference!=IntPtr.Zero){free(player,channel.Reference);channel.Reference=IntPtr.Zero;}}
        IntPtr Stop(IntPtr context) {
            IntPtr receiver=Marshal.ReadIntPtr(Marshal.ReadIntPtr(context,24));
            foreach(Channel channel in channels.ToArray())if(Marshal.ReadIntPtr(channel.Reference)==receiver)Release(channel);
            IntPtr result=originalStop(context);
            // The native stop implementation writes zero, which can later
            // make a second stop accidentally target another sound in slot 0.
            receiver=Marshal.ReadIntPtr(Marshal.ReadIntPtr(context,24));Marshal.WriteInt32(receiver,55,-1);return result;
        }
        IntPtr StopAll(IntPtr context) {
            Channel[] stopped=channels.ToArray();IntPtr result=originalStopAll(context);
            foreach(Channel channel in stopped){Marshal.WriteInt32(Marshal.ReadIntPtr(channel.Reference),55,-1);Release(channel);}return result;
        }
        internal void StopMovie(MovieActivity.Scope scope) {
            foreach(Channel channel in channels.ToArray())if(MovieActivity.Within(channel.Owner,scope)) {
                IntPtr* args=stackalloc IntPtr[1];args[0]=Marshal.ReadIntPtr(channel.Reference);
                IntPtr result=IntPtr.Zero;IntPtr* context=stackalloc IntPtr[5];context[0]=vm;context[1]=(IntPtr)(&result);context[2]=IntPtr.Zero;context[3]=(IntPtr)args;context[4]=IntPtr.Zero;
                Stop((IntPtr)context);
            }
        }
        internal void AfterTick() {
            // Native tick has already retired finished voices. Keep our strong
            // references until event dispatch, including across native GC.
            List<Channel> completed=new List<Channel>();
            foreach(Channel channel in channels.ToArray()) {
                IntPtr slot=Slot(channel),nativeReference=Marshal.ReadIntPtr(slot),value=Marshal.ReadIntPtr(channel.Reference);
                if(nativeReference!=IntPtr.Zero&&Marshal.ReadIntPtr(nativeReference)==value) {
                    if(Marshal.ReadInt16(slot,36)>=0)continue;
                    // A timeline stop command cancels a voice without clearing
                    // its native reference. It must not report completion.
                    // Voice slots own internal VM roots, which were never
                    // counted as public ValueRefs. Match the DLL's stop path.
                    Marshal.WriteIntPtr(slot,IntPtr.Zero);releaseInternal(vm,nativeReference);
                    Marshal.WriteInt32(value,55,-1);Release(channel);continue;
                }
                Marshal.WriteInt32(value,55,-1);*((float*)((byte*)value+59))=channel.Duration;
                channels.Remove(channel);completed.Add(channel);
            }
            // Already completed channels no longer belong to stopAll. A
            // listener may stop active sounds without cancelling these events.
            try {
                foreach(Channel channel in completed) {
                    if(channel.Owner!=null&&channel.Owner.Stopped)continue;
                    byte* argument=stackalloc byte[32];for(int i=0;i<32;i++)argument[i]=0;
                    *(int*)argument=9;*(IntPtr*)(argument+16)=channel.Reference;
                    if(completionClass==IntPtr.Zero) {
                        if(modern) {
                            IntPtr text=Marshal.StringToHGlobalAnsi("__IggyHostSoundEvents");
                            try {completionClass=global(player,text);}finally {Marshal.FreeHGlobal(text);}
                        } else {
                            // 1.2.30 has no public GetGlobal export. Resolve only
                            // our injected class, after Tick has left the VM.
                            IntPtr value=legacyGlobal(vm,completionClassName);
                            if(value.ToInt64()!=7&&value!=nullValue)completionClass=reference(player,value,0);
                        }
                        if(completionClass==IntPtr.Zero)throw new InvalidOperationException("Cannot resolve sound completion class.");
                    }
                    byte* receiver=stackalloc byte[40];path((IntPtr)receiver,player,completionClass);
                    uint status=call(player,IntPtr.Zero,(IntPtr)receiver,completionName,1,(IntPtr)argument);
                    if(status!=0&&status!=202)throw new InvalidOperationException("Sound completion dispatch failed: "+status);
                }
            } finally {foreach(Channel channel in completed)Release(channel);}
        }
        internal void Detach() {foreach(Channel channel in channels.ToArray())Release(channel);if(completionClass!=IntPtr.Zero){free(player,completionClass);completionClass=IntPtr.Zero;}}
    }
}
