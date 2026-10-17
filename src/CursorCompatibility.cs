using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace IggyViewer {
    // Per-player AS3 cursor properties. The native button-state byte defaults
    // to zero even on ordinary Sprites; useHandCursor is a stub in both DLLs.
    internal sealed class CursorCompatibility {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr Builtin(IntPtr context);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int ParseBoolean(IntPtr context,IntPtr format,out int value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr ArgumentError(IntPtr vm,int code);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr WeakReference(IntPtr player,IntPtr value,int weak);
        sealed class State {internal IntPtr Reference;internal bool ButtonMode,UseHandCursor=true;}
        readonly Dictionary<IntPtr,State> states=new Dictionary<IntPtr,State>();
        readonly Builtin getMode,setMode,getHand,setHand;
        readonly Builtin hideMouse,showMouse;
        internal bool Hidden {get;private set;}
        readonly ParseBoolean parse;
        readonly ArgumentError error;
        readonly WeakReference weakReference;
        readonly Native.VoidPP free;
        readonly IntPtr player,falseValue,trueValue;
        internal CursorCompatibility(Native native,IntPtr module,bool modern,Log log) {
            player=native.Player;
            IntPtr vm=Marshal.ReadIntPtr(player,modern?10560:10480);
            if(vm==IntPtr.Zero)return; // Timeline-only movies have no AS3 VM.
            parse=(ParseBoolean)Marshal.GetDelegateForFunctionPointer(IntPtr.Add(module,modern?0xb2d60:0xb8d40),typeof(ParseBoolean));
            error=(ArgumentError)Marshal.GetDelegateForFunctionPointer(IntPtr.Add(module,modern?0x79930:0x7c7a0),typeof(ArgumentError));
            weakReference=native.Bind<WeakReference>("IggyValueRefFromTempRef");free=native.Bind<Native.VoidPP>("IggyValueRefFree");
            falseValue=Marshal.ReadIntPtr(module,modern?0x110a90:0x101d00);trueValue=Marshal.ReadIntPtr(module,(modern?0x110a90:0x101d00)+8);
            getMode=c=>Get(c,false);setMode=c=>Set(c,false);getHand=c=>Get(c,true);setHand=c=>Set(c,true);
            hideMouse=c=>SetVisibility(true);showMouse=c=>SetVisibility(false);
            int count=Marshal.ReadInt32(vm,modern?9648:9624),patched=0;
            bool mouseFound=false;
            if(count<=0||count>250)throw new InvalidDataException("Unexpected cursor built-in registry.");
            HashSet<IntPtr> visited=new HashSet<IntPtr>();
            for(int i=0;i<count;i++) {
                IntPtr entry=IntPtr.Add(vm,(modern?3648:3624)+24*i);
                string name=Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(entry));
                if(name=="Mouse") {
                    InstallMouse(module,modern,Marshal.ReadIntPtr(entry,16));mouseFound=true;
                }
                if(name!="Sprite"&&name!="MovieClip"&&name!="SimpleButton")continue;
                IntPtr traits=Marshal.ReadIntPtr(Marshal.ReadIntPtr(entry,16),72);
                foreach(int table in new int[]{72,80}) {
                    int length=(ushort)Marshal.ReadInt16(traits,table==72?26:24);
                    if(length>256)throw new InvalidDataException("Unexpected cursor property table.");
                    IntPtr functions=Marshal.ReadIntPtr(traits,table);
                    for(int j=0;j<length;j++) {
                        IntPtr function=Marshal.ReadIntPtr(functions,8*j);
                        if(function==IntPtr.Zero||Marshal.ReadByte(function)!=16||!visited.Add(function))continue;
                        IntPtr text=Marshal.ReadIntPtr(function,8);int size=Marshal.ReadInt32(text,48);
                        if(size<0||size>200)throw new InvalidDataException("Unexpected cursor property name.");
                        string property=Marshal.PtrToStringUni(IntPtr.Add(text,52),size);
                        if(property!="buttonMode"&&property!="useHandCursor")continue;
                        bool hand=property=="useHandCursor",getter=table==72;
                        int expected=hand?(getter?(modern?0x4b740:0x4fb50):(modern?0x11da0:0x85d80)):(getter?(modern?0x8ceb0:0x89da0):(modern?0x8cf70:0x89dd0));
                        if(Marshal.ReadIntPtr(function,16)!=IntPtr.Add(module,expected))throw new InvalidDataException("Unexpected "+property+" callback.");
                        Builtin replacement=hand?(getter?getHand:setHand):(getter?getMode:setMode);
                        Marshal.WriteIntPtr(function,16,Marshal.GetFunctionPointerForDelegate(replacement));patched++;
                    }
                }
            }
            if(patched<4||!mouseFound)throw new InvalidDataException("Cannot locate cursor properties or Mouse methods.");
            log.Write("Cursor compatibility: buttonMode defaults false, useHandCursor is retained with weak object references; Mouse.hide/show control the canvas cursor.");
        }
        IntPtr SetVisibility(bool hidden) {Hidden=hidden;return IntPtr.Zero;}
        void InstallMouse(IntPtr module,bool modern,IntPtr metadata) {
            IntPtr traits=Marshal.ReadIntPtr(metadata,80),methods=Marshal.ReadIntPtr(traits,64);
            int count=(ushort)Marshal.ReadInt16(traits,22),patched=0;
            if(count>256)throw new InvalidDataException("Unexpected Mouse method table.");
            for(int i=0;i<count;i++) {
                IntPtr tagged=Marshal.ReadIntPtr(methods,8*i);
                if(tagged.ToInt64()<65536)continue;
                IntPtr function=IntPtr.Subtract(tagged,1);
                if(Marshal.ReadByte(function)!=16)continue;
                IntPtr text=Marshal.ReadIntPtr(function,8);int size=Marshal.ReadInt32(text,48);
                if(size<0||size>200)throw new InvalidDataException("Unexpected Mouse method name.");
                string name=Marshal.PtrToStringUni(IntPtr.Add(text,52),size);
                if(name!="hide"&&name!="show")continue;
                if(Marshal.ReadIntPtr(function,16)!=IntPtr.Add(module,modern?0x11da0:0x85d80))throw new InvalidDataException("Unexpected Mouse."+name+" callback.");
                Marshal.WriteIntPtr(function,16,Marshal.GetFunctionPointerForDelegate(name=="hide"?hideMouse:showMouse));patched++;
            }
            if(patched!=2)throw new InvalidDataException("Cannot locate Mouse.hide/show.");
        }
        static IntPtr Receiver(IntPtr context) {return Marshal.ReadIntPtr(Marshal.ReadIntPtr(context,24));}
        State Find(IntPtr instance) {
            State state;
            if(!states.TryGetValue(instance,out state))return null;
            // A weak reference becomes tagged null (7) when the AS3 object is
            // collected. A reused native instance address must not inherit it.
            IntPtr value=Marshal.ReadIntPtr(state.Reference);
            if(value.ToInt64()==7||value==IntPtr.Zero||Marshal.ReadIntPtr(value,39)!=instance)return null;
            return state;
        }
        IntPtr Get(IntPtr context,bool hand) {
            State state=Find(Marshal.ReadIntPtr(Receiver(context),39));
            bool result=hand?(state==null||state.UseHandCursor):(state!=null&&state.ButtonMode);
            Marshal.WriteIntPtr(Marshal.ReadIntPtr(context,8),result?trueValue:falseValue);
            return IntPtr.Zero;
        }
        IntPtr Set(IntPtr context,bool hand) {
            // Use the native Boolean conversion once; it can run AS3 and GC.
            IntPtr format=Marshal.StringToHGlobalAnsi("b");int value,status;
            try {status=parse(context,format,out value);}finally {Marshal.FreeHGlobal(format);}
            if(status!=0)return error(Marshal.ReadIntPtr(context),120);
            IntPtr receiver=Receiver(context),instance=Marshal.ReadIntPtr(receiver,39);
            State state=Find(instance);
            if(state==null) {
                Prune();state=new State {Reference=weakReference(player,receiver,1)};states[instance]=state;
            }
            if(hand)state.UseHandCursor=value!=0;
            else {
                state.ButtonMode=value!=0;
                // Keep native rollover/down state handling consistent.
                if(value==0)Marshal.WriteByte(instance,275,255);
                else if(Marshal.ReadByte(instance,275)>=128)Marshal.WriteByte(instance,275,0);
            }
            return IntPtr.Zero;
        }
        internal bool IsButton(IntPtr instance) {
            State state=Find(instance);byte type=Marshal.ReadByte(instance,3);
            return (state==null||state.UseHandCursor)&&(type==7?(Marshal.ReadByte(instance)&0x20)!=0:state!=null&&state.ButtonMode);
        }
        internal void Prune() {
            List<IntPtr> dead=null;
            foreach(KeyValuePair<IntPtr,State> pair in states)if(Find(pair.Key)==null){if(dead==null)dead=new List<IntPtr>();dead.Add(pair.Key);}
            if(dead!=null)foreach(IntPtr key in dead){free(player,states[key].Reference);states.Remove(key);}
        }
    }
}
