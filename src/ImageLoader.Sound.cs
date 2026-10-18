using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace IggyViewer {
    internal unsafe sealed partial class ImageLoader {
        sealed class SoundOwner {internal string Token;internal IntPtr Reference;}
        sealed class SoundData {internal IntPtr Reference,Definition;internal int Size;}
        readonly List<SoundOwner> soundOwners=new List<SoundOwner>();
        readonly List<SoundData> sounds=new List<SoundData>();
        long soundBytes;
        internal long SoundBytes {get{lock(sync)return soundBytes;}}
        internal bool IsExternalSound(IntPtr definition) {foreach(SoundData sound in sounds)if(sound.Definition==definition)return true;return false;}
        IntPtr SoundContent(IntPtr context,AmfRequest request) {
            string operation=request.Operation,answer;
            if(operation=="data-sound-register") {
                SoundOwner owner=new SoundOwner {Token=Guid.NewGuid().ToString("N"),Reference=reference(native.Player,Marshal.ReadIntPtr(request.Value),1)};soundOwners.Add(owner);answer=owner.Token;
            } else if(operation=="data-sound-owner"||operation=="data-sound-unregister") {
                string token=native.AmfString(Marshal.ReadIntPtr(request.Value));IntPtr value=nullValue;
                foreach(SoundOwner owner in soundOwners.ToArray())if(owner.Token==token) {
                    value=Marshal.ReadIntPtr(owner.Reference);if(value.ToInt64()==7)value=nullValue;
                    if(operation=="data-sound-unregister"){freeReference(native.Player,owner.Reference);soundOwners.Remove(owner);}break;
                }
                Marshal.WriteIntPtr(Marshal.ReadIntPtr(context,8),value);return IntPtr.Zero;
            } else if(operation=="data-sound-has")answer=Marshal.ReadIntPtr(Marshal.ReadIntPtr(request.Value),55)==IntPtr.Zero?"no":"yes";
            else if(operation=="data-sound-install") {
                lock(sync) {
                    DataJob job;
                    if(!dataJobs.TryGetValue(request.Extra,out job)||!job.SoundReady||job.Pcm==null)answer="error|Downloaded sound is unavailable";
                    else {
                        PruneSounds();
                        int size=checked(64+job.Pcm.Length);
                        if(soundBytes+size>128L*1024*1024)answer="error|Loaded sound storage exceeds 128 MiB";
                        else {
                            IntPtr weak=reference(native.Player,Marshal.ReadIntPtr(request.Value),1),definition=IntPtr.Zero;
                            try {
                                definition=Marshal.AllocHGlobal(size);for(int i=0;i<64;i++)Marshal.WriteByte(definition,i,0);
                                Marshal.Copy(job.Pcm,0,IntPtr.Add(definition,64),job.Pcm.Length);
                                Marshal.WriteByte(definition,0,21);Marshal.WriteInt32(definition,32,job.Pcm.Length+7);Marshal.WriteInt32(definition,36,job.Pcm.Length/(job.Channels*2));
                                Marshal.WriteIntPtr(definition,40,IntPtr.Add(definition,64));Marshal.WriteInt32(definition,48,job.Pcm.Length);
                                Marshal.WriteInt16(definition,52,(short)job.Rate);Marshal.WriteByte(definition,54,3);Marshal.WriteByte(definition,55,16);Marshal.WriteByte(definition,56,(byte)job.Channels);
                                Marshal.WriteIntPtr(Marshal.ReadIntPtr(weak),55,definition);
                                sounds.Add(new SoundData {Reference=weak,Definition=definition,Size=size});soundBytes+=size;answer=job.ResponseUrl??new Uri(new Uri(job.BaseUrl),job.Url).AbsoluteUri;
                            } catch {freeReference(native.Player,weak);if(definition!=IntPtr.Zero)Marshal.FreeHGlobal(definition);throw;}
                        }
                    }
                }
            } else throw new InvalidDataException("Unknown sound operation");
            return native.ReturnMovieString(context,answer);
        }
        void DecodeSound(DataJob job) {
            try {
                byte[] encoded;lock(sync){if(closed||job.Cancelled||job.Error!=null)return;encoded=job.Bytes;}
                int rate,channels;
                byte[] pcm=WindowsMp3.DecodeExternal(encoded,out rate,out channels,length=> {
                    lock(sync) {
                        if(closed||job.Cancelled)throw new OperationCanceledException();
                        long total=soundBytes+length;foreach(DataJob other in dataJobs.Values)if(other!=job)total+=other.DecodedReservation;
                        if(total>128L*1024*1024)throw new IOException("Pending and loaded sound storage exceeds 128 MiB");
                        if(!ReserveData(job,encoded.Length+length))throw new OperationCanceledException();job.DecodedReservation=length;
                    }
                });
                lock(sync)if(!closed&&!job.Cancelled){job.Pcm=pcm;job.Rate=rate;job.Channels=channels;job.Bytes=null;job.Reserved=job.DecodedReservation=pcm.Length;job.SoundReady=true;}
            } catch(Exception error){FailData(job,"error|"+error.Message.Replace('|',' '));}
        }
        bool SoundPlaying(IntPtr definition) {
            for(int i=0;i<8;i++) {IntPtr voice=IntPtr.Add(native.Player,(native.ModernDll?11648:11568)+56*i);if(Marshal.ReadInt16(voice,36)>=0&&Marshal.ReadIntPtr(voice,8)==definition)return true;}
            return false;
        }
        void PruneSounds() {
            foreach(SoundOwner owner in soundOwners.ToArray())if(Marshal.ReadIntPtr(owner.Reference).ToInt64()==7){freeReference(native.Player,owner.Reference);soundOwners.Remove(owner);}
            foreach(SoundData sound in sounds.ToArray())if(Marshal.ReadIntPtr(sound.Reference).ToInt64()==7&&!SoundPlaying(sound.Definition)) {
                freeReference(native.Player,sound.Reference);Marshal.FreeHGlobal(sound.Definition);sounds.Remove(sound);lock(sync)soundBytes-=sound.Size;
            }
        }
        void DetachSounds() {
            foreach(SoundOwner owner in soundOwners)freeReference(native.Player,owner.Reference);soundOwners.Clear();
            foreach(SoundData sound in sounds)if(sound.Reference!=IntPtr.Zero){freeReference(native.Player,sound.Reference);sound.Reference=IntPtr.Zero;}
        }
        void ReleaseSounds() {foreach(SoundData sound in sounds)Marshal.FreeHGlobal(sound.Definition);sounds.Clear();lock(sync)soundBytes=0;}
    }
}
