"""Isolated serialized-copy experiment; never changes the source movie."""
import struct
import sys
from pathlib import Path
from analyze_fixups import decode

b=bytearray(Path(sys.argv[1]).read_bytes())
abc=Path(sys.argv[2]).read_bytes()
n=struct.unpack_from('<I',b,28)[0]
entry=next(32+16*i for i in range(n) if struct.unpack_from('<H',b,32+16*i)[0]==1)
size,_,start=struct.unpack_from('<III',b,entry+4)
data=bytearray(b[start:start+size]);node=(size+7)&~7
head=struct.unpack_from('<q',data,152)[0]
data.extend(bytes(node-len(data)))
data.extend(struct.pack('<qI',1 if head==1 else 152+head-node,len(abc))+abc)
struct.pack_into('<q',data,152,node-152)
for i in range(n):
    at=32+16*i;kind,target,length,_,pos=struct.unpack_from('<HHIII',b,at)
    if kind!=0 or target!=0:continue
    program=bytes(b[pos:pos+length]);fields,cursor=decode(program)
    assert (152,2) in fields
    assert cursor<=node
    program+=bytes([255])+struct.pack('<I',node-cursor)+bytes([0xd2,0])
    p=len(b);b.extend(program);struct.pack_into('<III',b,at+4,len(program),len(program),p)
p=(len(b)+7)&~7;b.extend(bytes(p-len(b)));b.extend(data)
struct.pack_into('<III',b,entry+4,len(data),len(data),p)
Path(sys.argv[3]).write_bytes(b)
