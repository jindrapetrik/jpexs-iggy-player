import collections
import struct
import sys

WIDTH = {2:8,3:1,4:2,5:4,6:8,8:8,9:8,10:8,11:8,12:1,13:1,14:1,15:1}
KEEP = {2,8,9,10,11,12,13,14,15}

def decode(data):
    p = 0
    def byte():
        nonlocal p
        value=data[p]; p+=1; return value
    def template():
        advance=byte(); count=byte()
        return advance, [(byte(),byte()) for _ in range(count)]
    templates=[template() for _ in range(byte())]
    fields=[]; cursor=0
    def apply(t):
        nonlocal cursor
        advance, entries=t
        for offset,op in entries: fields.append((cursor+offset,op))
        cursor+=advance
    while p<len(data):
        code=byte()
        if code<0xC0:
            repeat=1 if code<128 else code-127
            index=code if code<128 else byte()
            for _ in range(repeat): apply(templates[index])
        elif code<0xD0: cursor+=code*2-382
        elif code in (0xD2,0xD4,0xD5,0xD6):
            op=code&15
            for _ in range(byte()+1): apply((WIDTH[op],[(0,op)]))
        elif code==0xFC: byte()
        elif code==0xFD: apply(template())
        elif code==0xFE: cursor+=byte()+1
        elif code==0xFF: cursor+=struct.unpack_from('<I',data,p)[0]; p+=4
        else: raise ValueError(hex(code))
    return fields,cursor

if __name__=='__main__':
    for path in sys.argv[1:]:
        b=open(path,'rb').read(); n=struct.unpack_from('<I',b,28)[0]
        for i in range(n):
            kind, target, size, _, start=struct.unpack_from('<HHIII',b,32+16*i)
            if kind: continue
            all_fields, end=decode(b[start:start+size])
            fields=sorted(f for f in all_fields if f[1] in KEEP)
            gaps=[fields[i+1][0]-f[0] for i,f in enumerate(fields[:-1])]
            print(path,'block',i,'original',size,'fields',len(all_fields),'retained',len(fields),'end',end)
            print('ops',collections.Counter(f[1] for f in fields))
            print('gaps',collections.Counter(gaps).most_common(20))
            print('unordered',sum(a[0]>b[0] for a,b in zip(all_fields,all_fields[1:])))
            tokens=[(f[1],g) for f,g in zip(fields,gaps)]
            print('tokens',collections.Counter(tokens).most_common(15))
            print('pairs',collections.Counter(zip(tokens,tokens[1:])).most_common(12))
