using System;
using System.IO;
using System.Collections.Generic;

namespace IggyViewer {
    internal static partial class Container {
        public static string[] EmbeddedFonts(byte[] bytes) {
            HashSet<string> fonts=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if(bytes.Length<64||bytes[9]!=64||bytes[8]!=1)return new string[0];
            int n=BitConverter.ToInt32(bytes,28);
            for(int block=0;block<n;block++) {
                int at=32+block*16;if(at+16>bytes.Length)throw new InvalidDataException("Truncated Iggy directory.");
                if(bytes[at]!=1)continue;
                int size=BitConverter.ToInt32(bytes,at+4),main=BitConverter.ToInt32(bytes,at+12);
                if(main<0||size<184||main>bytes.Length-size)throw new InvalidDataException("Invalid Iggy root.");
                int count=BitConverter.ToInt32(bytes,main+64);long dictionary=main+BitConverter.ToInt64(bytes,main);
                if(count<0||count>65536||dictionary<main||dictionary>main+(long)size-count*8L)throw new InvalidDataException("Invalid Iggy dictionary.");
                for(int id=0;id<count;id++) {
                    int field=checked((int)dictionary)+id*8;long reference=BitConverter.ToInt64(bytes,field);if(reference==1)continue;
                    long obj=field+reference;
                    if(obj<main||obj>=main+(long)size)throw new InvalidDataException("Invalid Iggy dictionary reference.");
                    if(bytes[(int)obj]!=22)continue;
                    if(obj>main+(long)size-136)throw new InvalidDataException("Truncated Iggy font.");
                    // A font with no outlines still needs a device font.
                    if(BitConverter.ToUInt16(bytes,(int)obj+32)==0)continue;
                    long rel=BitConverter.ToInt64(bytes,(int)obj+128);if(rel==1)continue;
                    long name=obj+128+rel;
                    if(name<main||name>main+(long)size-2)throw new InvalidDataException("Invalid Iggy font name.");
                    int end=(int)name;while(end+1<main+size&&(bytes[end]!=0||bytes[end+1]!=0))end+=2;
                    if(end+1>=main+size)throw new InvalidDataException("Unterminated Iggy font name.");
                    fonts.Add(System.Text.Encoding.Unicode.GetString(bytes,(int)name,end-(int)name));
                }
            }
            string[] result=new string[fonts.Count];fonts.CopyTo(result);return result;
        }
        public static string[] Imports(byte[] bytes) {
            if(bytes.Length<64||bytes[9]!=64||bytes[8]!=1)return new string[0];
            int n=BitConverter.ToInt32(bytes,28);
            for(int block=0;block<n;block++) {
                int at=32+block*16;if(at+16>bytes.Length)throw new InvalidDataException("Truncated Iggy directory.");
                if(bytes[at]!=1)continue;
                int size=BitConverter.ToInt32(bytes,at+4),main=BitConverter.ToInt32(bytes,at+12);
                if(main<0||size<184||main>bytes.Length-size)throw new InvalidDataException("Invalid Iggy root.");
                long rel=BitConverter.ToInt64(bytes,main+112);int count=BitConverter.ToInt32(bytes,main+128);
                if(count<0||count>32)throw new InvalidDataException("Invalid import library count.");
                if(count==0)return new string[0];
                long table=main+112L+rel;
                if(rel==1||table<main||table>main+(long)size-count*24L)throw new InvalidDataException("Invalid import library table.");
                List<string> names=new List<string>();
                for(int i=0;i<count;i++) {
                    int field=checked((int)table)+i*24;long str=field+BitConverter.ToInt64(bytes,field);
                    if(str<main||str>=main+(long)size)throw new InvalidDataException("Invalid import name.");
                    int end=checked((int)str);
                    while(end+1<main+size&&(bytes[end]!=0||bytes[end+1]!=0))end+=2;
                    if(end+1>=main+size)throw new InvalidDataException("Unterminated import name.");
                    names.Add(System.Text.Encoding.Unicode.GetString(bytes,(int)str,end-(int)str));
                }
                return names.ToArray();
            }
            return new string[0];
        }
        sealed class Template { public int Advance;public int[] Offsets,Operations; }
        struct Field { public long Offset;public int Operation,Order; }
        static int Width(int op) {
            switch(op) {case 2:case 6:case 8:case 9:case 10:case 11:return 8;case 3:case 12:case 13:case 14:case 15:return 1;case 4:return 2;case 5:return 4;default:throw new InvalidDataException("Unknown Iggy fixup field.");}
        }
        static Template ReadTemplate(BinaryReader r) {
            Template t=new Template {Advance=r.ReadByte()};int n=r.ReadByte();t.Offsets=new int[n];t.Operations=new int[n];
            for(int i=0;i<n;i++){t.Offsets[i]=r.ReadByte();t.Operations[i]=r.ReadByte();Width(t.Operations[i]);}return t;
        }
        static void Apply(Template t,List<Field> fields,ref long cursor,int size) {
            for(int i=0;i<t.Offsets.Length;i++) {
                long at=cursor+t.Offsets[i];int op=t.Operations[i];
                if(at<0||at>size-Width(op))throw new InvalidDataException("Iggy fixup outside its data block.");
                fields.Add(new Field {Offset=at,Operation=op,Order=fields.Count});
            }
            cursor+=t.Advance;
        }
        static List<Field> Decode(byte[] bytes,int start,int length,int size) {
            List<Field> fields=new List<Field>();long cursor=0;
            using(BinaryReader r=new BinaryReader(new MemoryStream(bytes,start,length,false))) {
                Template[] templates=new Template[r.ReadByte()];for(int i=0;i<templates.Length;i++)templates[i]=ReadTemplate(r);
                while(r.BaseStream.Position<r.BaseStream.Length) {
                    int code=r.ReadByte();
                    if(code<0xc0) {
                        int repeat=code<128?1:code-127,index=code<128?code:r.ReadByte();
                        if(index>=templates.Length)throw new InvalidDataException("Invalid Iggy fixup template.");
                        for(int i=0;i<repeat;i++)Apply(templates[index],fields,ref cursor,size);
                    } else if(code<0xd0)cursor+=code*2-382;
                    else if(code==0xd2||code==0xd4||code==0xd5||code==0xd6) {
                        int count=r.ReadByte()+1,op=code&15;
                        for(int i=0;i<count;i++)Apply(new Template {Advance=Width(op),Offsets=new int[]{0},Operations=new int[]{op}},fields,ref cursor,size);
                    } else if(code==0xfc)r.ReadByte();
                    else if(code==0xfd)Apply(ReadTemplate(r),fields,ref cursor,size);
                    else if(code==0xfe)cursor+=r.ReadByte()+1;
                    else if(code==0xff)cursor+=r.ReadUInt32();
                    else throw new InvalidDataException("Unsupported Iggy fixup opcode.");
                    if(cursor<0||cursor>size)throw new InvalidDataException("Invalid Iggy fixup cursor.");
                }
            }
            return fields;
        }
        static byte[] Compact(byte[] bytes,int start,int length,int size,List<Field> extra=null) {
            int[] ops={2,8,9,10,11,12,13,14,15};
            List<Field> fields=Decode(bytes,start,length,size);
            if(extra!=null)fields.AddRange(extra);
            // Scalar swaps/bitfield conversions are no-ops on this host.
            // Keep pointer and host-context writes, including their tie order.
            fields.RemoveAll(field=>Array.IndexOf(ops,field.Operation)<0);
            fields.Sort((a,b)=>a.Offset!=b.Offset?a.Offset.CompareTo(b.Offset):a.Order.CompareTo(b.Order));
            int[] keys=new int[fields.Count];Dictionary<int,int> counts=new Dictionary<int,int>();
            for(int i=0;i<fields.Count;i++) {
                long gap=i+1<fields.Count?fields[i+1].Offset-fields[i].Offset:0;
                int advance=gap<=255?(int)gap:0,key=fields[i].Operation*256+advance,count;
                keys[i]=key;counts.TryGetValue(key,out count);counts[key]=count+1;
            }
            // A template's advance incorporates the otherwise separate skip
            // to the next field. Frequent strides get one-byte references;
            // the native repeat opcode then encodes up to 64 such writes.
            List<int> templates=new List<int>();Dictionary<int,int> indices=new Dictionary<int,int>();
            foreach(int op in ops){indices.Add(op*256,templates.Count);templates.Add(op*256);}
            List<int> candidates=new List<int>(counts.Keys);
            candidates.Sort((a,b)=>counts[a]!=counts[b]?counts[b].CompareTo(counts[a]):a.CompareTo(b));
            foreach(int key in candidates)if(templates.Count<128&&!indices.ContainsKey(key)){indices.Add(key,templates.Count);templates.Add(key);}
            using(MemoryStream stream=new MemoryStream())using(BinaryWriter w=new BinaryWriter(stream)) {
                w.Write((byte)templates.Count);
                foreach(int key in templates){w.Write((byte)(key&255));w.Write((byte)1);w.Write((byte)0);w.Write((byte)(key/256));}
                long cursor=0;
                for(int i=0;i<fields.Count;) {
                    Field field=fields[i];
                    long gap=field.Offset-cursor;
                    if(gap<0)throw new InvalidDataException("Out-of-order Iggy fixup.");
                    if(gap>0) {
                        if(gap<=32&&gap%2==0)w.Write((byte)(191+gap/2));
                        else if(gap<=256){w.Write((byte)0xfe);w.Write((byte)(gap-1));}
                        else {w.Write((byte)0xff);w.Write((uint)gap);}
                        cursor=field.Offset;
                    }
                    int key=keys[i],index;if(!indices.TryGetValue(key,out index)){key=field.Operation*256;index=indices[key];}
                    int repeat=1,advance=key&255;
                    while(repeat<64&&i+repeat<fields.Count&&keys[i+repeat]==key&&fields[i+repeat].Offset==cursor+(long)repeat*advance)repeat++;
                    if(repeat>1)w.Write((byte)(127+repeat));
                    w.Write((byte)index);cursor+=(long)repeat*advance;i+=repeat;
                }
                byte[] compact=stream.ToArray();List<Field> decoded=Decode(compact,0,compact.Length,size);
                if(decoded.Count!=fields.Count)throw new InvalidDataException("Compacted Iggy fixup lost fields.");
                for(int i=0;i<fields.Count;i++)if(decoded[i].Offset!=fields[i].Offset||decoded[i].Operation!=fields[i].Operation)throw new InvalidDataException("Compacted Iggy fixup changed a field.");
                return compact;
            }
        }
        static byte[] NormalizeEmptyStyles(byte[] bytes,Log log) {
            if(bytes[8]!=1||bytes[10]!=1)return bytes;
            int blocks=BitConverter.ToInt32(bytes,28),dataIndex=0;
            using(MemoryStream output=new MemoryStream())using(BinaryWriter w=new BinaryWriter(output)) {
                w.Write(bytes);bool changed=false;
                for(int block=0;block<blocks;block++) {
                    int entry=32+block*16,kind=BitConverter.ToUInt16(bytes,entry);
                    if(kind!=1&&kind!=8)continue;
                    int target=dataIndex++;if(kind!=1)continue;
                    int main=BitConverter.ToInt32(bytes,entry+12),size=BitConverter.ToInt32(bytes,entry+4);
                    if(size<184)throw new InvalidDataException("Truncated Iggy main block.");
                    int count=BitConverter.ToInt32(bytes,main+64);long dictionary=main+BitConverter.ToInt64(bytes,main);
                    if(count<0||count>65536||dictionary<main||dictionary>main+(long)size-count*8L)throw new InvalidDataException("Invalid Iggy dictionary.");
                    List<int> empty=new List<int>();
                    for(int id=0;id<count;id++) {
                        int field=checked((int)dictionary)+id*8;long reference=BitConverter.ToInt64(bytes,field);if(reference==1)continue;
                        long shape=field+reference;
                        if(shape<main||shape>=main+(long)size)throw new InvalidDataException("Invalid Iggy dictionary reference.");
                        if(bytes[(int)shape]!=1&&bytes[(int)shape]!=2)continue;
                        if(shape>main+(long)size-80)throw new InvalidDataException("Truncated Iggy shape.");
                        int groups=BitConverter.ToUInt16(bytes,(int)shape+56);if(groups==0)continue;
                        long data=shape+72+BitConverter.ToInt64(bytes,(int)shape+72);
                        if(data<main||data>main+(long)size-groups*64L)throw new InvalidDataException("Invalid Iggy shape groups.");
                        for(int g=0;g<groups;g++)for(int offset=32;offset<=40;offset+=8) {
                            int at=(int)data+g*64+offset;if(BitConverter.ToInt64(bytes,at)==1)empty.Add(at-main);
                        }
                    }
                    if(empty.Count==0)continue;
                    HashSet<long> pointers=new HashSet<long>();
                    for(int fixup=0;fixup<blocks;fixup++) {
                        int at=32+fixup*16;
                        if(BitConverter.ToUInt16(bytes,at)!=0||BitConverter.ToUInt16(bytes,at+2)!=target)continue;
                        foreach(Field field in Decode(bytes,BitConverter.ToInt32(bytes,at+12),BitConverter.ToInt32(bytes,at+4),size))if(field.Operation==2)pointers.Add(field.Offset);
                    }
                    foreach(int field in empty)if(!pointers.Contains(field))throw new InvalidDataException("Empty Iggy style table has no pointer fixup.");
                    // Relative pointers remain valid when the whole main block
                    // moves. Add a shared empty table at its aligned end; its
                    // count is zero, so no additional relocation is required.
                    int table=checked((size+7)&~7),newSize=checked(table+8);
                    byte[] dataBlock=new byte[newSize];Buffer.BlockCopy(bytes,main,dataBlock,0,size);
                    foreach(int field in empty)Buffer.BlockCopy(BitConverter.GetBytes((long)table-field),0,dataBlock,field,8);
                    output.Position=output.Length;while(output.Position%8!=0)w.Write((byte)0);
                    int destination=checked((int)output.Position);w.Write(dataBlock);
                    output.Position=entry+4;w.Write(newSize);w.Write(newSize);w.Write(destination);
                    changed=true;log.Write("Normalized "+empty.Count+" serialized empty style tables before native loading; source file unchanged.");
                }
                return changed?output.ToArray():bytes;
            }
        }
        public static byte[] Prepare(byte[] bytes,Log log) {
            if(bytes.Length<32||BitConverter.ToUInt32(bytes,0)!=0xed0a6749)throw new InvalidDataException("Not an Iggy container.");
            if(bytes[9]!=64)throw new NotSupportedException("This player uses a 64-bit Iggy DLL. 32-bit Iggy files require a matching 32-bit runtime.");
            int count=BitConverter.ToInt32(bytes,28);
            if(count<1||count>256||32L+count*16>bytes.Length)throw new InvalidDataException("Invalid Iggy block directory.");
            List<int> sizes=new List<int>();
            for(int i=0;i<count;i++) {
                int at=32+16*i,len=BitConverter.ToInt32(bytes,at+4),pos=BitConverter.ToInt32(bytes,at+12);
                if(len<0||pos<32+count*16||pos>bytes.Length-len)throw new InvalidDataException("Iggy block outside file.");
                if(bytes[at]==1||bytes[at]==8)sizes.Add(len);
            }
            bytes=(byte[])bytes.Clone();
            NormalizeClassNames(bytes,log);
            bytes=NormalizeEmptyStyles(bytes,log);
            sizes.Clear();
            for(int i=0;i<count;i++){int at=32+16*i;if(bytes[at]==1||bytes[at]==8)sizes.Add(BitConverter.ToInt32(bytes,at+4));}
            for(int block=0;block<count;block++) {
                int at=32+block*16;if(bytes[at]!=1)continue;
                int main=BitConverter.ToInt32(bytes,at+12),size=BitConverter.ToInt32(bytes,at+4);
                if(size<184||bytes[main+168]!=0||bytes[8]!=1)continue;
                int characters=BitConverter.ToInt32(bytes,main+64);long rel=BitConverter.ToInt64(bytes,main);
                long dictionary=main+rel;
                if(characters<0||characters>65536||dictionary<main||dictionary>main+(long)size-characters*8L)throw new InvalidDataException("Invalid Iggy dictionary.");
                for(int id=0;id<characters;id++) {
                    int field=checked((int)dictionary)+id*8;long reference=BitConverter.ToInt64(bytes,field);if(reference==1)continue;
                    long obj=field+reference;
                    if(obj<main||obj>=main+(long)size)throw new InvalidDataException("Invalid Iggy dictionary reference.");
                    if(bytes[(int)obj]==6) {
                        // HTML text name interning in this runtime requires a VM,
                        // even when the movie contains no user ActionScript.
                        bytes[main+168]=1;log.Write("Enabled an empty AS3 VM in the in-memory copy for native editable/HTML text support.");break;
                    }
                }
            }
            MemoryStream result=new MemoryStream();result.Write(bytes,0,bytes.Length);
            for(int i=0;i<count;i++) {
                int at=32+16*i,len=BitConverter.ToInt32(bytes,at+4),pos=BitConverter.ToInt32(bytes,at+12);
                if(bytes[at]!=0||len<=65536)continue;
                if(bytes[8]!=1||bytes[10]!=1||bytes[at+2]>=sizes.Count)throw new InvalidDataException("Oversized fixup block cannot be normalized for this runtime.");
                byte[] compact=Compact(bytes,pos,len,sizes[bytes[at+2]]);
                if(compact.Length>65536)throw new InvalidDataException("Fixup program exceeds the native DLL's 64 KiB temporary buffer even after compaction.");
                int output=checked((int)result.Length);result.Position=result.Length;result.Write(compact,0,compact.Length);
                using(BinaryWriter w=new BinaryWriter(result,System.Text.Encoding.UTF8,true)) {
                    result.Position=at+4;w.Write(compact.Length);w.Write(compact.Length);w.Write(output);
                }
                log.Write("Compacted native no-op fixups and repeated strides in memory: "+len+" -> "+compact.Length+" bytes; source file unchanged.");
            }
            return result.ToArray();
        }
        static void NormalizeClassNames(byte[] bytes,Log log) {
            // The native initializer runs during CreateFromMemory. For resource
            // classes it only links metadata when the definition name is null.
            // A name serialized together with an empty runtime class cache skips
            // that step, leaving both display and embedded resource classes
            // unbound. Sprite constructors and their frame scripts never run.
            int blocks=BitConverter.ToInt32(bytes,28),normalized=0,target=0;
            for(int block=0;block<blocks;block++) {
                int entry=32+block*16,type=BitConverter.ToUInt16(bytes,entry);
                if(type!=1){if(type==8)target++;continue;}
                int size=BitConverter.ToInt32(bytes,entry+4),main=BitConverter.ToInt32(bytes,entry+12);
                if(bytes[8]!=1||size<184){target++;continue;}
                int count=BitConverter.ToInt32(bytes,main+176);
                long symbols=main+160L+BitConverter.ToInt64(bytes,main+160);
                if(count<0||count>65536||symbols<main||symbols>main+(long)size-count*24L)throw new InvalidDataException("Invalid Iggy symbol table.");
                List<int> fields=new List<int>();
                for(int i=0;i<count;i++) {
                    int at=checked((int)symbols)+24*i;
                    long reference=BitConverter.ToInt64(bytes,at);
                    if(BitConverter.ToInt32(bytes,at+16)==0||reference==1)continue;
                    long obj=at+reference;
                    if(obj<main||obj>main+(long)size-32)throw new InvalidDataException("Invalid Iggy symbol object.");
                    int objectType=bytes[(int)obj];
                    if(!((objectType>=1&&objectType<=7)||(objectType>=20&&objectType<=22))||BitConverter.ToInt64(bytes,(int)obj+24)!=0||BitConverter.ToInt64(bytes,(int)obj+16)==1)continue;
                    fields.Add((int)obj+16-main);
                }
                if(fields.Count>0) {
                    HashSet<long> pointers=new HashSet<long>();
                    for(int fixup=0;fixup<blocks;fixup++) {
                        int at=32+16*fixup;
                        if(BitConverter.ToUInt16(bytes,at)!=0||BitConverter.ToUInt16(bytes,at+2)!=target)continue;
                        foreach(Field field in Decode(bytes,BitConverter.ToInt32(bytes,at+12),BitConverter.ToInt32(bytes,at+4),size))if(field.Operation==2)pointers.Add(field.Offset);
                    }
                    foreach(int field in fields) {
                        if(!pointers.Contains(field))throw new InvalidDataException("Iggy class name has no pointer fixup.");
                        Buffer.BlockCopy(BitConverter.GetBytes(1L),0,bytes,main+field,8);normalized++;
                    }
                }
                target++;
            }
            if(normalized>0)log.Write("Reset "+normalized+" serialized symbol names for native AS3 class binding; source file unchanged.");
        }
    }
}
