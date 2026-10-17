using System;
using System.Runtime.InteropServices;
using System.Text;

namespace IggyViewer {
    internal unsafe sealed partial class Native {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int AmfInvoke(IntPtr vm,IntPtr result,IntPtr function,IntPtr args,int count,int parameter);
        int byteArrayDefaultEncoding=3;
        ByteArrayCall byteArrayConstructor;
        internal string ReadAmfText(IntPtr value,uint length) {return ReadByteArrayText(value,length,Encoding.UTF8,false);}
        void InstallByteArrayConstructor() {
            ByteArrayCall original=(ByteArrayCall)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(byteArrayMetadata,56),typeof(ByteArrayCall));
            byteArrayDefaultEncoding=3;
            byteArrayConstructor=context=> {
                IntPtr result=original(context);
                if(byteArrayDefaultEncoding!=3)ByteArraySettings(ByteArrayReceiver(context),true).Encoding=byteArrayDefaultEncoding;
                return result;
            };
            Marshal.WriteIntPtr(byteArrayMetadata,56,Marshal.GetFunctionPointerForDelegate(byteArrayConstructor));
        }
        IntPtr InvokeAmf(IntPtr context,bool write) {
            IntPtr vm=Marshal.ReadIntPtr(context);
            int stackOffset=modernDll?27448:27416,count=write?2:1;
            IntPtr stack=Marshal.ReadIntPtr(vm,stackOffset);
            Marshal.WriteIntPtr(stack,IntPtr.Zero);Marshal.WriteIntPtr(stack,8,ByteArrayReceiver(context));
            if(write)Marshal.WriteIntPtr(stack,16,ByteArrayParameter(context,1));
            Marshal.WriteIntPtr(vm,stackOffset,IntPtr.Add(stack,8*(count+1)));
            try {
                // Resolve through the global script so its initializer establishes
                // lexical scope. Merely reading the Class metadata skips that step.
                // Receiver and arguments remain rooted on the VM's moving GC stack.
                IntPtr codecName=MovieName(vm,"com.jpexs.iggy.runtime::HostAmf",true);
                Marshal.WriteIntPtr(stack,MovieFunction<MovieGlobal>(0x79990,0x7ff90)(vm,codecName));
                Marshal.WriteIntPtr(stack,MovieFunction<MovieProperty>(0xaf4d0,0xb6b90)(vm,Marshal.ReadIntPtr(stack),codecName));
                IntPtr name=MovieName(vm,write?"::write":"::read",true);
                IntPtr function=MovieFunction<MovieProperty>(0xaf4d0,0xb6b90)(vm,Marshal.ReadIntPtr(stack),name);
                MovieFunction<AmfInvoke>(0x794c0,0x7fbd0)(vm,Marshal.ReadIntPtr(context,8),function,stack,count,-1);
                return Marshal.ReadIntPtr(vm,(modernDll?10528:10504))==IntPtr.Zero?IntPtr.Zero:Marshal.ReadIntPtr(vm,modernDll?10552:10528);
            } finally {Marshal.WriteIntPtr(vm,stackOffset,stack);}
        }
        IntPtr ByteArrayAmfProperty(IntPtr context,string operation) {
            IntPtr value=ByteArrayReceiver(context);int encoding;
            switch(operation) {
                case "readObject":return InvokeAmf(context,false);
                case "writeObject":return InvokeAmf(context,true);
                case "objectEncoding":ByteArrayState state=ByteArraySettings(value);encoding=state==null?3:state.Encoding;break;
                case "static_defaultObjectEncoding":encoding=byteArrayDefaultEncoding;break;
                case "set_objectEncoding":case "set_static_defaultObjectEncoding":
                    uint next=ByteArrayArgument(context,1);if(next!=0&&next!=3)throw new ByteArrayFailure("ArgumentError","objectEncoding must be AMF0 (0) or AMF3 (3).");
                    if(operation=="set_objectEncoding")ByteArraySettings(ByteArrayReceiver(context),true).Encoding=(int)next;else byteArrayDefaultEncoding=(int)next;
                    return IntPtr.Zero;
                default:throw new InvalidOperationException("Unknown AMF property.");
            }
            Marshal.WriteIntPtr(Marshal.ReadIntPtr(context,8),new IntPtr(2*encoding));return IntPtr.Zero;
        }
    }
}
