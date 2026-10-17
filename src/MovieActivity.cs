using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace IggyViewer {
    // Track activity by loaded instance, not by library: two Loaders can use
    // the same cached classes while only one of their movies is being stopped.
    internal unsafe sealed class MovieActivity {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr Builtin(IntPtr context);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr Reference(IntPtr player,IntPtr value,int weak);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr BoundMethod(IntPtr vm,IntPtr receiver,IntPtr function);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void ReferenceLimit(IntPtr player,int count);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate byte BooleanValue(IntPtr value);
        internal sealed class Scope {internal Scope Parent;internal bool Stopped;}
        sealed class Owner {internal IntPtr Value;internal Scope Scope;internal bool Timer;}
        sealed class Method {internal IntPtr Value;internal Builtin Original;internal Scope Scope;internal bool Registered,Constructor;}
        sealed class Listener {internal IntPtr Target,Function,Bound,Type;internal Scope Scope;internal bool Capture;}
        readonly Native native;
        readonly IntPtr vm,module,invokePointer;
        readonly bool modern;
        readonly int stackOffset;
        readonly Reference nativeReference;
        readonly Native.VoidPP freeReference;
        readonly ReferenceLimit referenceLimit;
        readonly int originalLimit;
        int references,budget;
        readonly BoundMethod bind;
        readonly BooleanValue boolean;
        readonly List<Owner> owners=new List<Owner>();
        readonly Dictionary<IntPtr,Owner> ownerIndex=new Dictionary<IntPtr,Owner>();
        readonly List<Method> methods=new List<Method>();
        readonly Dictionary<IntPtr,Method> methodIndex=new Dictionary<IntPtr,Method>();
        readonly List<Listener> listeners=new List<Listener>();
        readonly List<Builtin> callbacks=new List<Builtin>();
        readonly Builtin invoke,add,remove,timerStop,timerStart,originalStart;
        internal Scope Current;
        internal bool CollectRequested;
        bool retireTimers;

        internal MovieActivity(Native native,IntPtr module,bool modern) {
            this.native=native;this.module=module;this.modern=modern;
            vm=Marshal.ReadIntPtr(native.Player,modern?10560:10480);stackOffset=modern?27448:27416;
            nativeReference=native.Bind<Reference>("IggyValueRefFromTempRef");freeReference=native.Bind<Native.VoidPP>("IggyValueRefFree");
            referenceLimit=native.Bind<ReferenceLimit>("IggySetValueRefLimit");budget=originalLimit=Marshal.ReadInt32(native.Player,modern?10684:10604);
            bind=Function<BoundMethod>(modern?0x44880:0x47a50);
            boolean=Function<BooleanValue>(modern?0xb2bf0:0xb8ad0);
            invoke=Invoke;invokePointer=Marshal.GetFunctionPointerForDelegate(invoke);
            Dictionary<string,IntPtr> classes=new Dictionary<string,IntPtr>();
            int count=Marshal.ReadInt32(vm,modern?9648:9624);
            if(count<1||count>250)throw new InvalidDataException("Unexpected activity class registry.");
            for(int i=0;i<count;i++) {
                IntPtr entry=IntPtr.Add(vm,(modern?3648:3624)+24*i);
                classes.Add(Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(entry)),Marshal.ReadIntPtr(entry,16));
            }
            remove=FindMethod(classes["EventDispatcher"],"removeEventListener",null);
            add=FindMethod(classes["EventDispatcher"],"addEventListener",AddListener);
            timerStop=FindMethod(classes["Timer"],"stop",null);
            timerStart=StartTimer;originalStart=FindMethod(classes["Timer"],"start",timerStart);
            foreach(string name in new string[]{"EventDispatcher","Timer","Shape","SimpleButton","TextField","Bitmap","Sound"}) {
                IntPtr metadata=classes[name];Builtin original=Function<Builtin>(Marshal.ReadIntPtr(metadata,56));bool timer=name=="Timer",dispatcher=name=="EventDispatcher";
                Builtin replacement=delegate(IntPtr context) {
                    Track(Marshal.ReadIntPtr(Marshal.ReadIntPtr(context,24)),timer,dispatcher);
                    return original(context);
                };
                callbacks.Add(replacement);Marshal.WriteIntPtr(metadata,56,Marshal.GetFunctionPointerForDelegate(replacement));
            }
        }
        T Function<T>(int offset) where T:class {return Function<T>(IntPtr.Add(module,offset));}
        static T Function<T>(IntPtr address) where T:class {return (T)(object)Marshal.GetDelegateForFunctionPointer(address,typeof(T));}
        static bool Heap(IntPtr value) {return value.ToInt64()>65536&&(value.ToInt64()&3)==1;}
        static IntPtr Value(IntPtr weak) {return weak==IntPtr.Zero?IntPtr.Zero:Marshal.ReadIntPtr(weak);}
        static bool Alive(IntPtr weak) {IntPtr v=Value(weak);return v!=IntPtr.Zero&&v.ToInt64()!=7;}
        IntPtr reference(IntPtr player,IntPtr value,int weak) {
            // Account for intentional host tracking without hiding growth in
            // the runtime's own references behind a blanket disabled limit.
            int next=originalLimit+references+1;if(next>budget){budget=next;referenceLimit(player,budget);}
            IntPtr result=nativeReference(player,value,weak);if(result!=IntPtr.Zero)references++;return result;
        }
        void free(IntPtr player,IntPtr value) {freeReference(player,value);references--;}
        internal bool TryOwner(IntPtr value,out Scope scope) {
            Owner indexed;
            if(ownerIndex.TryGetValue(value,out indexed)&&Value(indexed.Value)==value){scope=indexed.Scope;return true;}
            foreach(Owner owner in owners)if(Value(owner.Value)==value){ReindexOwners();scope=owner.Scope;return true;}
            scope=null;return false;
        }
        void ReindexOwners() {ownerIndex.Clear();foreach(Owner owner in owners)if(Alive(owner.Value))ownerIndex[Value(owner.Value)]=owner;}
        internal Scope OwnerOf(IntPtr value) {Scope scope;return TryOwner(value,out scope)?scope:Current;}
        internal void Track(IntPtr value,bool timer=false,bool parent=false) {
            if(Current==null&&!parent)return;
            if(!Heap(value)||Marshal.ReadByte(IntPtr.Subtract(value,1))<32||Marshal.ReadByte(IntPtr.Subtract(value,1))==38)return;
            Owner owner;
            if(ownerIndex.TryGetValue(value,out owner)&&Value(owner.Value)==value){owner.Timer|=timer;return;}
            owner=new Owner {Value=reference(native.Player,value,1),Scope=Current,Timer=timer};owners.Add(owner);ownerIndex[value]=owner;
        }
        Builtin FindMethod(IntPtr metadata,string name,Builtin replacement) {
            IntPtr traits=Marshal.ReadIntPtr(metadata,72),table=Marshal.ReadIntPtr(traits,64);int count=(ushort)Marshal.ReadInt16(traits,22);
            if(count>10000)throw new InvalidDataException("Invalid activity method table.");
            for(int i=0;i<count;i++) {
                IntPtr function=IntPtr.Subtract(Marshal.ReadIntPtr(table,8*i),1);
                if(function.ToInt64()<65536||Marshal.ReadByte(function)!=16)continue;
                IntPtr text=Marshal.ReadIntPtr(function,8);if(text==IntPtr.Zero)continue;
                if(Marshal.ReadInt32(text,48)!=name.Length||Marshal.PtrToStringUni(IntPtr.Add(text,52),name.Length)!=name)continue;
                Builtin original=Function<Builtin>(Marshal.ReadIntPtr(function,16));
                if(replacement!=null){callbacks.Add(replacement);Marshal.WriteIntPtr(function,16,Marshal.GetFunctionPointerForDelegate(replacement));}
                return original;
            }
            throw new InvalidDataException("Missing activity method: "+name);
        }
        internal void PatchClass(IntPtr metadata) {
            foreach(int offset in new int[]{72,80}) {
                IntPtr traits=Marshal.ReadIntPtr(metadata,offset);if(traits==IntPtr.Zero)continue;
                PatchFunction(Marshal.ReadIntPtr(traits,8),false,true);
                foreach(int table in new int[]{64,72,80}) {
                    int count=(ushort)Marshal.ReadInt16(traits,table==64?22:table==72?26:24);
                    if(count>10000)throw new InvalidDataException("Invalid script activity method table.");
                    IntPtr entries=Marshal.ReadIntPtr(traits,table);
                    for(int i=0;i<count;i++) {
                        IntPtr function=Marshal.ReadIntPtr(entries,8*i);
                        PatchFunction(table==64?function:IntPtr.Add(function,1),false);
                    }
                }
            }
        }
        void PatchFunction(IntPtr value,bool registered,bool constructor=false) {
            if(!Heap(value))return;
            byte kind=Marshal.ReadByte(IntPtr.Subtract(value,1));
            if(kind==18){PatchFunction(IntPtr.Add(Marshal.ReadIntPtr(value,7),1),registered);return;}
            if(kind!=16&&kind!=17&&kind!=20)return;
            IntPtr function=IntPtr.Subtract(value,1),callback=Marshal.ReadIntPtr(function,16);
            if(callback==invokePointer) {
                foreach(Method method in methods)if(Value(method.Value)==value){if(registered&&!method.Registered){method.Scope=Current;method.Registered=true;}method.Constructor|=constructor;return;}
                throw new InvalidDataException("Missing script activity wrapper.");
            }
            long offset=callback.ToInt64()-module.ToInt64();
            if(offset!=(modern?0x79d70:0x7d5a0)&&offset!=(modern?0x79e10:0x85b70))return;
            Method added=new Method {Value=reference(native.Player,value,1),Original=Function<Builtin>(callback),Scope=Current,Registered=registered,Constructor=constructor};
            methods.Add(added);methodIndex[function]=added;Marshal.WriteIntPtr(function,16,invokePointer);
        }
        IntPtr Invoke(IntPtr context) {
            IntPtr function=Marshal.ReadIntPtr(context,32);Method method;
            if(!methodIndex.TryGetValue(function,out method)||Value(method.Value)!=IntPtr.Add(function,1)) {
                method=null;foreach(Method candidate in methods)if(Value(candidate.Value)==IntPtr.Add(function,1)){method=candidate;methodIndex[function]=candidate;break;}
            }
            if(method==null)throw new InvalidOperationException("Unknown script activity callback.");
            Scope previous=Current,owner;IntPtr receiver=Marshal.ReadIntPtr(Marshal.ReadIntPtr(context,24));
            if(!method.Constructor&&TryOwner(receiver,out owner))Current=owner;
            else if(method.Registered)Current=method.Scope;
            // Unregistered class methods inherit their caller's loaded instance.
            if(Heap(receiver)&&Marshal.ReadByte(IntPtr.Subtract(receiver,1))!=38)Track(receiver,parent:method.Registered);
            try {
                // Dispatch can already have queued a callback when a different
                // listener unloads this instance during the same native tick.
                if(Current!=null&&Current.Stopped&&method.Registered){Marshal.WriteIntPtr(Marshal.ReadIntPtr(context,8),IntPtr.Zero);return IntPtr.Zero;}
                return method.Original(context);
            }finally {Current=previous;}
        }
        IntPtr AddListener(IntPtr context) {
            if(Marshal.ReadInt32(context,16)<2)return add(context);
            // Native void methods may use the receiver slot for their return
            // value. Root copies also follow any moving GC during registration.
            IntPtr saved=Marshal.ReadIntPtr(vm,stackOffset),args=Marshal.ReadIntPtr(context,24);
            for(int i=0;i<3;i++)Marshal.WriteIntPtr(saved,8*i,Marshal.ReadIntPtr(args,8*i));
            Marshal.WriteIntPtr(vm,stackOffset,IntPtr.Add(saved,24));
            try {return RegisteredListener(context,saved,add(context));}finally {Marshal.WriteIntPtr(vm,stackOffset,saved);}
        }
        IntPtr RegisteredListener(IntPtr context,IntPtr kept,IntPtr result) {
            IntPtr args=Marshal.ReadIntPtr(context,24),target=Marshal.ReadIntPtr(kept),type=Marshal.ReadIntPtr(kept,8),function=Marshal.ReadIntPtr(kept,16);
            Scope owner=Current;
            if(Heap(function)&&Marshal.ReadByte(IntPtr.Subtract(function,1))==18) {
                Scope boundOwner;if(TryOwner(Marshal.ReadIntPtr(function,23),out boundOwner))owner=boundOwner;
            }
            Scope previous=Current;Current=owner;
            try {PatchFunction(function,true);}finally {Current=previous;}
            if(!Heap(type)||Marshal.ReadByte(IntPtr.Subtract(type,1))!=8)return result;
            string name=ReadString(type);
            bool periodic=name=="enterFrame"||name=="frameConstructed"||name=="exitFrame"||name=="activate"||name=="deactivate",timer=false;
            Scope targetOwner;bool knownTarget=TryOwner(target,out targetOwner);
            foreach(Owner item in owners)if(item.Timer&&Value(item.Value)==target){timer=true;break;}
            if((periodic||timer)&&knownTarget&&targetOwner!=null)owner=targetOwner;
            if(owner==null||(!native.IsStageObject(target)&&!periodic&&!timer))return result;
            IntPtr bound=IntPtr.Zero;
            if(Heap(function)&&Marshal.ReadByte(IntPtr.Subtract(function,1))==18){bound=Marshal.ReadIntPtr(function,23);function=IntPtr.Add(Marshal.ReadIntPtr(function,7),1);}
            if(!Heap(function))return result;
            bool capture=Marshal.ReadInt32(context,16)>=3&&True(Marshal.ReadIntPtr(args,24));
            foreach(Listener existing in listeners)if(Value(existing.Target)==target&&Value(existing.Function)==function&&Value(existing.Bound)==bound&&existing.Capture==capture&&ReadString(Value(existing.Type))==name)return result;
            listeners.Add(new Listener {Target=reference(native.Player,target,1),Function=reference(native.Player,function,1),Bound=bound==IntPtr.Zero?IntPtr.Zero:reference(native.Player,bound,1),Type=reference(native.Player,type,0),Scope=owner,Capture=capture});
            return result;
        }
        static string ReadString(IntPtr value) {
            if(!Heap(value)||Marshal.ReadByte(IntPtr.Subtract(value,1))!=8)return null;
            IntPtr text=Marshal.ReadIntPtr(value,7);int length=Marshal.ReadInt32(text,7);
            return length>=0&&length<1048576?Marshal.PtrToStringUni(IntPtr.Add(text,11),length):null;
        }
        internal bool True(IntPtr value) {return boolean(value)!=0;}
        IntPtr StartTimer(IntPtr context) {
            Scope scope=OwnerOf(Marshal.ReadIntPtr(Marshal.ReadIntPtr(context,24)));
            if(scope!=null&&scope.Stopped)return IntPtr.Zero;
            return originalStart(context);
        }
        internal static bool Within(Scope owner,Scope scope) {for(int i=0;owner!=null&&i<256;i++,owner=owner.Parent)if(owner==scope)return true;return false;}
        void Call(Builtin callback,params IntPtr[] args) {
            IntPtr saved=Marshal.ReadIntPtr(vm,stackOffset);for(int i=0;i<args.Length;i++)Marshal.WriteIntPtr(saved,i*8,args[i]);
            Marshal.WriteIntPtr(saved,8*args.Length,IntPtr.Zero);
            Marshal.WriteIntPtr(vm,stackOffset,IntPtr.Add(saved,8*(args.Length+1)));
            IntPtr* context=stackalloc IntPtr[5];context[0]=vm;context[1]=IntPtr.Add(saved,8*args.Length);context[2]=new IntPtr(args.Length-1);context[3]=saved;context[4]=IntPtr.Zero;
            try {callback((IntPtr)context);}finally {Marshal.WriteIntPtr(vm,stackOffset,saved);}
        }
        void Release(Listener listener) {
            free(native.Player,listener.Target);free(native.Player,listener.Function);free(native.Player,listener.Type);
            if(listener.Bound!=IntPtr.Zero)free(native.Player,listener.Bound);listeners.Remove(listener);
        }
        internal void Stop(IntPtr value,bool collect) {
            CollectRequested|=collect;
            if(!native.IsLoadedMovieRoot(value))return;
            Scope scope;if(!TryOwner(value,out scope)||scope==null)return;
            scope.Stopped=true;
            HashSet<IntPtr> clips=new HashSet<IntPtr>();
            foreach(Owner owner in owners)if(Within(owner.Scope,scope)) {
                owner.Scope.Stopped=true;IntPtr item=Value(owner.Value);if(!Alive(owner.Value))continue;
                if(owner.Timer)Call(timerStop,item);
                native.StopMovieClip(item,clips);
            }
            foreach(Listener listener in listeners.ToArray())if(Within(listener.Scope,scope)) {
                if(Alive(listener.Target)&&Alive(listener.Function)&&(listener.Bound==IntPtr.Zero||Alive(listener.Bound))) {
                    IntPtr function=Value(listener.Function);
                    if(listener.Bound!=IntPtr.Zero)function=bind(vm,Value(listener.Bound),function);
                    Call(remove,Value(listener.Target),Value(listener.Type),function,new IntPtr(listener.Capture?2:0));
                }
                Release(listener);
            }
            native.CancelMovieLoads(scope);native.StopMovieSounds(scope);CollectRequested|=collect;retireTimers=true;
        }
        internal void AfterTick() {
            if(!retireTimers)return;retireTimers=false;
            // The native timer chain can keep stopped timers and their captured
            // movie alive. Retire them after dispatch has left that chain; an
            // unloadAndStop inside a TIMER listener must not truncate iteration.
            IntPtr link=IntPtr.Add(vm,136);
            for(int count=0;count<100000;count++) {
                IntPtr timer=Marshal.ReadIntPtr(link);if(timer==IntPtr.Zero)break;
                Scope owner;
                if(TryOwner(timer,out owner)&&owner!=null&&owner.Stopped) {
                    Marshal.WriteIntPtr(link,Marshal.ReadIntPtr(timer,87));Marshal.WriteIntPtr(timer,87,IntPtr.Zero);
                } else link=IntPtr.Add(timer,87);
            }
        }
        internal void Prune() {
            for(int i=owners.Count-1;i>=0;i--)if(!Alive(owners[i].Value)){free(native.Player,owners[i].Value);owners.RemoveAt(i);}
            ReindexOwners();
            foreach(Listener listener in listeners.ToArray())if(!Alive(listener.Target)||!Alive(listener.Function)||(listener.Bound!=IntPtr.Zero&&!Alive(listener.Bound)))Release(listener);
            methodIndex.Clear();
            for(int i=methods.Count-1;i>=0;i--) {
                if(!Alive(methods[i].Value)){free(native.Player,methods[i].Value);methods.RemoveAt(i);}
                else methodIndex[IntPtr.Subtract(Value(methods[i].Value),1)]=methods[i];
            }
        }
        internal void Detach() {
            foreach(Listener listener in listeners.ToArray())Release(listener);
            foreach(Owner owner in owners)free(native.Player,owner.Value);owners.Clear();ownerIndex.Clear();
            foreach(Method method in methods)if(Alive(method.Value))Marshal.WriteIntPtr(Value(method.Value),15,Marshal.GetFunctionPointerForDelegate(method.Original));
            foreach(Method method in methods)free(native.Player,method.Value);methods.Clear();methodIndex.Clear();
        }
        internal void KeepAlive() {GC.KeepAlive(callbacks);GC.KeepAlive(invoke);}
    }
}
