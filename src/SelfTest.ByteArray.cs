using System;
using System.Drawing;
using System.IO;
using System.Threading;

namespace IggyViewer {
    internal static partial class SelfTest {
        public static void RunByteArray(string dll,string movie,string output,Log log,string renderer) {
            Directory.CreateDirectory(output);string hash=Hash(File.ReadAllBytes(movie));
            CheckByteArrayCompression();
            using(Native native=new Native(dll,log,renderer:renderer)) {
                for(int cycle=0;cycle<2;cycle++) {
                    native.Open(movie,550,400);
                    for(int step=0;step<200&&!KeyFlag(native,"imageReady");step++){Thread.Sleep(10);Check(native.Frame(true),"Advance ByteArray fixture");}
                    log.Write("ByteArray: checks="+ReadValue(native,"checks")+", failures="+ReadValue(native,"failures")+", details="+ReadString(native,"failureList"));
                    Check(KeyFlag(native,"complete")&&KeyFlag(native,"imageReady")&&ReadValue(native,"checks")>=50&&ReadValue(native,"failures")==0,"Binary, endian, Unicode, compression and error semantics");
                    // Retire weak bookkeeping after the first moving collection,
                    // then collect objects formerly reachable from that cycle.
                    for(int i=0;i<2;i++){native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);native.Frame(false);}
                    Call(native,"","afterGC");
                    Check(ReadValue(native,"failures")==0,"Endianness and contents survive native moving collection");
                    Check(native.ByteArrayBufferCount==1,"Unreachable ByteArrays release their writable buffers");
                    using(Bitmap image=native.Renderer.Snapshot()){Check(image.GetPixel(13,20).ToArgb()==Color.Red.ToArgb(),"Decompressed embedded PNG reaches loadBytes");image.Save(Path.Combine(output,"bytearray-"+cycle+".png"));}
                    native.Close();Check(native.ByteArrayBufferCount==0,"Close releases retained ByteArray storage");
                }
                native.Open(Path.Combine(Path.GetDirectoryName(movie),"bytearray","only.iggy"),550,400);native.Frame(true);
                Check(KeyFlag(native,"success"),"ByteArray compatibility is installed for movies without Loader references");native.Close();
            }
            Check(hash==Hash(File.ReadAllBytes(movie)),"ByteArray fixture remains unchanged");
            foreach(string message in log.Messages)Check(message.IndexOf("Uncaught exception",StringComparison.Ordinal)<0,"No uncaught ByteArray errors");
            log.Write("PASS: ByteArray binary/IEEE values, endian, UTF/multibyte strings, zlib/DEFLATE interoperability, malformed inputs, typed errors, loadBytes, GC and reopen.");
        }
        static void CheckByteArrayCompression() {
            byte[] payload=new byte[65537];new Random(173).NextBytes(payload);
            foreach(bool zlib in new bool[]{true,false}) {
                byte[] encoded=ByteArrayCompression.Transform(payload,true,zlib,100000),decoded=ByteArrayCompression.Transform(encoded,false,zlib,100000);
                Check(Hash(decoded)==Hash(payload),"Binary compression across stored block boundaries");
                bool limited=false;try{ByteArrayCompression.Transform(encoded,false,zlib,1024);}catch(InvalidDataException){limited=true;}
                Check(limited,"Decompression limit is checked before output allocation");
            }
            foreach(byte[] invalid in new byte[][]{new byte[]{3},new byte[]{7},new byte[]{1,1,0,0,0,42},new byte[]{5,0,0,0}}) {
                bool rejected=false;try{ByteArrayCompression.Transform(invalid,false,false,1024);}catch(InvalidDataException){rejected=true;}
                Check(rejected,"Truncated, reserved, invalid stored and invalid Huffman streams are rejected");
            }
        }
    }
}
