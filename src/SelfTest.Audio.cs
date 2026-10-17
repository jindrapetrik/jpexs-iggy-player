using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace IggyViewer {
    internal unsafe sealed partial class Native {
        // Diagnostic access is limited to the two verified DLL builds.
        internal byte[] AudioSnapshot() {
            int size=Marshal.ReadInt32(module,modernDll?0x107AD0:0xF91F0);
            IntPtr buffer=Marshal.ReadIntPtr(module,modernDll?0x1093C8:0xFABB0);
            if(buffer==IntPtr.Zero||size<=0||size>1024*1024)return new byte[0];
            buffer=new IntPtr((buffer.ToInt64()+15)&~15L);
            IntPtr driver=IntPtr.Add(module,modernDll?0x107A70:0xF9190);
            int blocks=Marshal.ReadInt32(driver,modernDll?112:108),blockSize=Marshal.ReadInt32(driver,modernDll?120:116);
            CheckAudioBuffer(blocks,blockSize,size);
            // WaveOut puts its WAVEHDR table before the actual PCM blocks.
            byte[] bytes=new byte[blocks*blockSize];Marshal.Copy(IntPtr.Add(buffer,48*blocks),bytes,0,bytes.Length);return bytes;
        }
        static void CheckAudioBuffer(int blocks,int blockSize,int size) {if(blocks<1||blocks>24||blockSize<1||(long)blocks*(blockSize+48)>size)throw new InvalidDataException("Unexpected WaveOut buffer layout.");}
        internal bool AudioRunning {get{return Marshal.ReadInt32(module,modernDll?0x107A68:0xF92C4)!=0;}}
        internal int AudioChannels {get{int count=0;for(int i=0;i<8;i++)if(Marshal.ReadInt16(Player,(modernDll?11684:11604)+i*56)>=0)count++;return count;}}
    }
    internal static partial class SelfTest {
        static void AudioWait(Native native,int milliseconds) {
            for(int i=0;i<milliseconds;i+=10){Thread.Sleep(10);Check(native.Frame(true),"Advance audio fixture");}
        }
        static void AudioKey(Native native,int key) {native.Key(key,true);native.Key(key,false);}
        static uint AudioPosition(Native native) {AudioKey(native,84);return ReadValue(native,"position");}
        static double AudioRms(byte[] bytes) {
            double sum=0;for(int i=0;i+1<bytes.Length;i+=2){int value=BitConverter.ToInt16(bytes,i);sum+=(double)value*value;}
            return Math.Sqrt(sum/Math.Max(1,bytes.Length/2));
        }
        static double AudioPitchCorrelation(byte[] bytes,int frequency) {
            int frames=bytes.Length/4,window=Math.Min(2048,frames),best=0;double energy=-1;
            for(int at=0;at+window<=frames;at+=256) {
                double sum=0;for(int j=0;j<window;j++){int v=BitConverter.ToInt16(bytes,(at+j)*4);sum+=(double)v*v;}
                if(sum>energy){energy=sum;best=at;}
            }
            int lag=(int)Math.Round(44100.0/frequency);double cross=0,a=0,b=0;
            for(int j=lag;j<window;j++){double x=BitConverter.ToInt16(bytes,(best+j)*4),y=BitConverter.ToInt16(bytes,(best+j-lag)*4);cross+=x*y;a+=x*x;b+=y*y;}
            return cross/Math.Max(1,Math.Sqrt(a*b));
        }
        public static void RunAudio(string dll,string fixture,string output,Log log) {
            Directory.CreateDirectory(output);string hash=Hash(File.ReadAllBytes(fixture));
            byte[] vbr=File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(fixture)),"audio","tone-vbr.mp3"));
            byte[] encoded=new byte[vbr.Length+2];Buffer.BlockCopy(vbr,0,encoded,2,vbr.Length);
            int rate,channels;byte[] decoded=WindowsMp3.Decode(encoded,out rate,out channels);
            Check(rate==44100&&channels==2&&decoded.Length>=44100*4,"VBR output is not truncated by ACM size estimates");
            encoded[0]=128;byte[] advanced=WindowsMp3.Decode(encoded,out rate,out channels);
            Check(advanced.Length==decoded.Length-128*4&&advanced[0]==decoded[128*4],"Positive MP3 seek prefix");
            encoded[0]=128;encoded[1]=255;byte[] delayed=WindowsMp3.Decode(encoded,out rate,out channels);
            bool silent=true;for(int i=0;i<128*4;i++)silent&=delayed[i]==0;
            Check(delayed.Length==decoded.Length+128*4&&silent,"Negative MP3 seek inserts silence");
            bool invalid=false;try{WindowsMp3.Decode(new byte[20],out rate,out channels);}catch(InvalidDataException){invalid=true;}
            Check(invalid,"Invalid MP3 rejected as a managed error");
            using(Native native=new Native(dll,log,audio:true)) {
                Check(native.AudioEnabled&&native.Mp3!=null,"Install native audio and MP3 callbacks");
                native.Open(fixture,500,200);
                AudioWait(native,140);
                Check(native.AudioRunning,"WaveOut device started");
                Check(AudioRms(native.AudioSnapshot())>100,"Timeline StartSound produces PCM output");
                AudioWait(native,600);
                foreach(int key in new int[]{49,50,51,52,53,54}) {
                    uint completed=ReadValue(native,"completed");
                    AudioKey(native,key);Check(ReadValue(native,"started","IggyValueGetBooleanRS")==1,"Sound.play returns a channel for "+key);
                    AudioWait(native,140);byte[] mixed=native.AudioSnapshot();double rms=AudioRms(mixed);
                    log.Write("Audio format key "+key+": output bytes="+mixed.Length+", RMS="+rms.ToString("F2"));
                    File.WriteAllBytes(Path.Combine(output,"mixed-"+key+".pcm"),mixed);
                    Check(rms>100,"Audible mixed output for format "+key);
                    Check(AudioPitchCorrelation(mixed,new int[]{440,330,550,440,660,880}[key-49])>.90,"Decoded pitch preserved for format "+key);
                    Check(AudioPosition(native)>20,"SoundChannel position advances for "+key);
                    if(key==51) {
                        // The first ADPCM block alone does not catch incorrectly
                        // concatenated WAV packets. Inspect later output as well.
                        for(int part=0;part<3;part++) {
                            AudioWait(native,80);byte[] later=native.AudioSnapshot();
                            int peak=0;for(int i=0;i+1<later.Length;i+=2)peak=Math.Max(peak,Math.Abs((int)BitConverter.ToInt16(later,i)));
                            log.Write("ADPCM later output "+part+": RMS="+AudioRms(later).ToString("F2")+", peak="+peak);
                            Check(peak<1500,"ADPCM remains a quiet tone across block boundaries and the tail");
                        }
                    }
                    AudioWait(native,650);
                    Check(native.AudioChannels==0,"Sound ends after its sample count for "+key);
                    Check(ReadValue(native,"completed")==completed+1,"Exactly one SOUND_COMPLETE for "+key);
                    Check(ReadValue(native,"completedPosition")==400,"Completed channel position is its duration");
                    Check(native.TrackedSoundChannels==0,"Completed channel reference released");
                }
                Check(ReadValue(native,"eventTargetCorrect","IggyValueGetBooleanRS")==1,"Sound event target/currentTarget and flags");
                Check(native.Mp3.Opened>=2&&native.Mp3.OutputBytes>10000&&native.Mp3.Failures==0,"Mono/stereo MP3 decoded by callback");
                uint completion=ReadValue(native,"completed");AudioKey(native,76);AudioWait(native,120);
                native.SetPaused(true);AudioWait(native,100);uint paused=AudioPosition(native);AudioWait(native,250);
                Check(AudioPosition(native)==paused,"Pause freezes SoundChannel position");
                Check(ReadValue(native,"completed")==completion,"Pause does not complete sound");
                native.SetPaused(false);AudioWait(native,100);
                Check(AudioPosition(native)>paused,"Resume continues sound");
                AudioWait(native,200);Check(native.Mp3.Active==1,"MP3 remains active in its second iteration");
                AudioWait(native,900);Check(native.Mp3.Active==0,"MP3 finishes after both iterations");
                Check(ReadValue(native,"completed")==completion+1,"Loop dispatches one completion after both iterations");
                completion=ReadValue(native,"completed");AudioKey(native,68);AudioWait(native,600);
                Check(native.AudioChannels==1&&ReadValue(native,"completed")==completion,"loops=2 plays three iterations");
                AudioWait(native,700);Check(ReadValue(native,"completed")==completion+1,"Triple loop completes once");
                completion=ReadValue(native,"completed");AudioKey(native,79);AudioWait(native,800);
                Check(ReadValue(native,"completed")==completion+2,"Concurrent channels each complete once");
                completion=ReadValue(native,"completed");AudioKey(native,67);AudioWait(native,1600);
                Check(ReadValue(native,"completed")==completion+3&&ReadValue(native,"chained")==2,"Completion listeners can start new sounds");
                completion=ReadValue(native,"completed");uint conversions=ReadValue(native,"conversions");AudioKey(native,86);
                Check(ReadValue(native,"conversions")==conversions+2,"Native valueOf argument conversions run once");
                AudioWait(native,700);Check(ReadValue(native,"completed")==completion+1,"Fractional start time and SoundTransform complete");
                AudioKey(native,52);AudioWait(native,80);AudioKey(native,82);
                Check(native.Mp3.Active==1,"Stopping a completed channel cannot stop a reused voice slot");
                AudioKey(native,80);AudioWait(native,100);
                completion=ReadValue(native,"completed");
                AudioKey(native,79);AudioWait(native,100);Check(native.Mp3.Active==2,"Concurrent MP3 channels");
                native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);GC.Collect();GC.WaitForPendingFinalizers();
                AudioWait(native,80);Check(native.Mp3.Failures==0,"Decoder survives native and managed GC");
                AudioKey(native,80);AudioWait(native,100);Check(native.Mp3.Active==0,"SoundChannel.stop releases MP3 decoders");
                AudioWait(native,600);Check(ReadValue(native,"completed")==completion&&native.TrackedSoundChannels==0,"stop does not dispatch completion");
                AudioKey(native,79);AudioWait(native,80);AudioKey(native,83);AudioWait(native,100);
                Check(native.Mp3.Active==0,"SoundMixer.stopAll releases MP3 decoders");
                AudioWait(native,600);Check(ReadValue(native,"completed")==completion&&native.TrackedSoundChannels==0,"stopAll does not dispatch completion");
                AudioKey(native,49);AudioWait(native,60);AudioKey(native,78);AudioWait(native,700);
                Check(native.AudioChannels==0&&native.TrackedSoundChannels==0&&ReadValue(native,"completed")==completion,"Timeline stop cancels AS3 sound without completion");
                AudioKey(native,52);AudioWait(native,80);
                native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);GC.Collect();GC.WaitForPendingFinalizers();
                AudioWait(native,700);Check(ReadValue(native,"completed")==completion+1,"Completion delivery and helper survive native/managed GC");
                native.SetMuted(true);AudioKey(native,76);AudioWait(native,340);
                Check(AudioRms(native.AudioSnapshot())<1,"Mute silences mixed output");
                native.SetMuted(false);AudioWait(native,80);Check(AudioRms(native.AudioSnapshot())>100,"Unmute restores output");
                AudioKey(native,79);AudioWait(native,80);native.Close();Check(native.Mp3.Active==0,"Close releases active decoders");
                native.Open(fixture,500,200);AudioKey(native,52);AudioWait(native,100);Check(native.Mp3.Active==1,"Reopen restores audio");
                native.Close();Check(native.Mp3.Active==0&&native.Mp3.Opened==native.Mp3.Closed&&native.TrackedSoundChannels==0,"No decoder or sound references remain");
            }
            Check(Hash(File.ReadAllBytes(fixture))==hash,"Audio fixture unchanged on disk");
            foreach(string message in log.Messages)Check(message.IndexOf("DLL 408:",StringComparison.Ordinal)<0,"No native audio reference errors");
            log.Write("PASS: timeline and AS3 PCM8/PCM16/ADPCM/mono/stereo MP3, VBR/seek prefixes, Flash loop counts, SOUND_COMPLETE targets/flags, chained listeners, one-time coercion, stop/stopAll without completion, reused slots, pause/resume, mute, GC and reopen.");
        }
    }
}
