using System;
using System.IO;
using System.Runtime.InteropServices;

namespace IggyViewer {
    // The supplied DLLs replace a zero-length Y axis with scale 1 when
    // assigning scaleY again. Patch the VM setter, never the DLL code/file.
    internal unsafe sealed class ZeroScaleCompatibility {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr Setter(IntPtr context);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int ParseFloat(IntPtr context,out float value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr Scale3D(IntPtr instance,int axis,float value);
        readonly Setter original,replacement;
        readonly ParseFloat parse;
        readonly Scale3D scale3D;
        internal ZeroScaleCompatibility(IntPtr module,bool modern,IntPtr metadata,Log log) {
            IntPtr callback=IntPtr.Add(module,modern?0x84de0:0x86f20);
            original=(Setter)Marshal.GetDelegateForFunctionPointer(callback,typeof(Setter));
            parse=(ParseFloat)Marshal.GetDelegateForFunctionPointer(IntPtr.Add(module,modern?0xb3440:0xb9320),typeof(ParseFloat));
            scale3D=(Scale3D)Marshal.GetDelegateForFunctionPointer(IntPtr.Add(module,modern?0x92230:0x86c60),typeof(Scale3D));
            IntPtr traits=Marshal.ReadIntPtr(metadata,72),setters=Marshal.ReadIntPtr(traits,80),closure=IntPtr.Zero;
            int count=(ushort)Marshal.ReadInt16(traits,24);
            if(count>256)throw new InvalidDataException("Unexpected DisplayObject setter table.");
            for(int i=0;i<count;i++) {
                IntPtr function=Marshal.ReadIntPtr(setters,8*i);
                if(function!=IntPtr.Zero&&Marshal.ReadByte(function)==16&&Marshal.ReadIntPtr(function,16)==callback) {
                    if(closure!=IntPtr.Zero&&closure!=function)throw new InvalidDataException("Ambiguous scaleY setter.");
                    closure=function;
                }
            }
            if(closure==IntPtr.Zero)throw new InvalidDataException("Cannot locate DisplayObject.scaleY setter.");
            replacement=Set;
            Marshal.WriteIntPtr(closure,16,Marshal.GetFunctionPointerForDelegate(replacement));
            log.Write("scaleY compatibility: assignments to a zero-length 2D axis retain the requested scale.");
        }
        static byte* Instance(IntPtr context) {
            IntPtr receiver=Marshal.ReadIntPtr(Marshal.ReadIntPtr(context,24));
            return (byte*)Marshal.ReadIntPtr(receiver,39);
        }
        IntPtr Set(IntPtr context) {
            byte* instance=Instance(context);
            if((instance[1]&8)!=0||*(IntPtr*)(instance+88)!=IntPtr.Zero||*(float*)(instance+220)!=0||*(float*)(instance+228)!=0)
                return original(context);
            // Use native argument conversion exactly once, including valueOf
            // side effects and the native exception for invalid arguments.
            float value;if(parse(context,out value)==0)return IntPtr.Zero;
            if(float.IsNaN(value)||float.IsInfinity(value))value=0;
            instance=Instance(context); // Conversion may run AS3 and native GC.
            if((instance[1]&8)==0) {
                instance[1]|=4;
                float c=*(float*)(instance+220),d=*(float*)(instance+228);
                float length=(float)Math.Sqrt((float)(c*c+d*d));
                byte signs=instance[2];
                if(signs==0)signs=(byte)(d*(*(float*)(instance+216))>=c*(*(float*)(instance+224))?1:5);
                if(length==0) {
                    *(float*)(instance+220)=0;
                    *(float*)(instance+228)=value;
                } else {
                    float factor=value/length;if((signs&4)!=0)factor=-factor;
                    *(float*)(instance+220)=c*factor;*(float*)(instance+228)=d*factor;
                }
                instance[2]=(byte)(value>=0?signs&~4:signs|4);
            }
            // Preserve native behavior if argument conversion promoted the
            // receiver to a 3D transform in the meantime.
            if(*(IntPtr*)(instance+88)!=IntPtr.Zero)scale3D((IntPtr)instance,1,value);
            return IntPtr.Zero;
        }
    }
}
