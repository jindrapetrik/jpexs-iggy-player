using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace IggyViewer {
    internal unsafe sealed partial class Native {
        void NormalizeAudio(IntPtr generic) {
            if(!AudioEnabled)return;
            int count=Marshal.ReadInt32(generic,160),normalized=0;
            IntPtr dictionary=Marshal.ReadIntPtr(generic,152);
            if(count<0||count>65536)throw new InvalidDataException("Invalid audio dictionary length.");
            HashSet<IntPtr> visited=new HashSet<IntPtr>();
            for(int i=0;i<count;i++) {
                IntPtr sound=Marshal.ReadIntPtr(dictionary,i*8);
                if(sound==IntPtr.Zero||!visited.Add(sound)||Marshal.ReadByte(sound)!=21)continue;
                int format=Marshal.ReadByte(sound,54),bits=Marshal.ReadByte(sound,55);
                if(format==0&&bits==16) {
                    // Serialized format-0 samples are big endian. The x64
                    // mixer treats formats 0 and 3 alike and reads little endian.
                    int length=Marshal.ReadInt32(sound,48);IntPtr bytes=Marshal.ReadIntPtr(sound,40);
                    if(length<0||(length&1)!=0||(length>0&&bytes==IntPtr.Zero))throw new InvalidDataException("Invalid native PCM payload.");
                    byte* data=(byte*)bytes;
                    for(int j=0;j<length;j+=2){byte b=data[j];data[j]=data[j+1];data[j+1]=b;}
                    Marshal.WriteByte(sound,54,3);normalized++;
                } else if(format>3)log.Write("Audio: unsupported sound codec "+format+" (character "+(ushort)Marshal.ReadInt16(sound,2)+").");
            }
            if(normalized>0)log.Write("Audio: normalized "+normalized+" native-endian PCM definitions in memory.");
        }
    }
}
