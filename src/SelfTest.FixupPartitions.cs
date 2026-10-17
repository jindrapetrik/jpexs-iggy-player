using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace IggyViewer {
    internal static partial class Container {
        internal static void TestFixupInstructions(Log log) {
            byte[] program;
            using(MemoryStream stream=new MemoryStream())using(BinaryWriter w=new BinaryWriter(stream)) {
                w.Write((byte)3);
                w.Write(new byte[]{8,1,0,2,0,2,0,12,0,15,16,2,0,2,8,8});
                for(int i=0;i<1000;i++) {
                    w.Write((byte)0);w.Write((byte)191);w.Write((byte)2);
                    w.Write((byte)1); // Two host writes at the same cursor.
                    w.Write((byte)0xfe);w.Write((byte)(i%256));
                    w.Write(new byte[]{0xfd,8,2,0,9,0,10,0xfc,123});
                    w.Write((byte)0xff);w.Write((uint)(4096+i));
                    foreach(byte code in new byte[]{0xd2,0xd4,0xd5,0xd6}){w.Write(code);w.Write((byte)2);}
                    w.Write((byte)0xc0);
                }
                program=stream.ToArray();
            }
            foreach(int limit in new int[]{128,1024,4096}) {
                List<byte[]> chunks=SplitFixups(program,16000000,limit);
                if(chunks.Count<2)throw new InvalidDataException("Fixup test did not partition instructions.");
                foreach(byte[] chunk in chunks)if(chunk.Length>limit)throw new InvalidDataException("Fixup test exceeded its buffer limit.");
            }
            log.Write("PASS: fixup templates, repeats, zero strides, same-offset host writes, inline records, compact/byte/32-bit skips, scalar arrays and exact partition equivalence.");
        }
        internal static byte[] TestPartitionContainer(byte[] bytes,Log log) {
            List<int> sizes=new List<int>();int count=BitConverter.ToInt32(bytes,28);
            for(int i=0;i<count;i++){int at=32+16*i,type=BitConverter.ToUInt16(bytes,at);if(type==1||type==8)sizes.Add(BitConverter.ToInt32(bytes,at+4));}
            return LimitFixupBlocks(bytes,sizes,log,1024);
        }
    }
    internal static partial class SelfTest {
        public static void RunFixupPartitions(string dll,string movie,string output,Log log) {
            Directory.CreateDirectory(output);Container.TestFixupInstructions(log);
            byte[] original=File.ReadAllBytes(movie);string hash=Hash(original);
            byte[] prepared=Container.Prepare(original,log),partitioned=Container.TestPartitionContainer(prepared,log);
            int count=BitConverter.ToInt32(partitioned,28),fixups=0;
            for(int i=0;i<count;i++) {
                int at=32+16*i;if(BitConverter.ToUInt16(partitioned,at)!=0)continue;
                Check(BitConverter.ToInt32(partitioned,at+4)<=1024,"Native regression uses small fixup partitions");fixups++;
            }
            Check(fixups>1,"Regression fixture contains multiple native fixup entries");
            Check(Hash(Container.Prepare(partitioned,log))==Hash(partitioned),"Partitioned container preparation is idempotent");
            string path=Path.Combine(output,"partitioned.iggy");File.WriteAllBytes(path,partitioned);
            List<Bitmap> reference=new List<Bitmap>();int[] frames={4,11,47,49,51,63,66};
            try {
                using(Native native=new Native(dll,log)) {
                    native.Open(movie,640,480);
                    foreach(int frame in frames){native.Seek(frame);Check(native.Frame(false),"Reference native frame "+frame);reference.Add(native.Renderer.Snapshot());}
                }
                using(Native native=new Native(dll,log)) {
                    native.Open(path,640,480);
                    for(int i=0;i<frames.Length;i++) {
                        native.Seek(frames[i]);Check(native.Frame(false),"Partitioned native frame "+frames[i]);
                        using(Bitmap image=native.Renderer.Snapshot())Check(Different(reference[i],image)==0,"Partitioned native frame "+frames[i]+" is pixel-identical");
                    }
                    native.Close();native.Open(path,320,240);Check(native.Frame(false),"Reopen partitioned movie");
                }
            } finally {foreach(Bitmap image in reference)image.Dispose();}
            Check(Hash(original)==hash&&Hash(File.ReadAllBytes(movie))==hash,"Partitioning leaves the source unchanged");
            log.Write("PASS: "+fixups+" native fixup partitions, seven pixel-identical frames, gradients/filters/text, idempotence, reopen and unchanged source.");
        }
    }
}
