"""Read-only PE string/RIP-reference audit for the supplied x64 Iggy DLLs."""
import bisect
import struct
import sys
from pathlib import Path

class PE:
    def __init__(self, path):
        self.data = Path(path).read_bytes()
        header = struct.unpack_from('<I', self.data, 60)[0]
        count = struct.unpack_from('<H', self.data, header + 6)[0]
        optional = struct.unpack_from('<H', self.data, header + 20)[0]
        self.base = struct.unpack_from('<Q', self.data, header + 48)[0]
        self.sections = [struct.unpack_from('<8sIIII', self.data,
                         header + 24 + optional + 40 * i) for i in range(count)]
        pdata = next(s for s in self.sections if s[0].rstrip(b'\0') == b'.pdata')
        self.functions = [struct.unpack_from('<III', self.data, p)[:2]
                          for p in range(pdata[4], pdata[4] + pdata[1], 12)]
        self.starts = [f[0] for f in self.functions]
    def va(self, offset):
        for name, virtual_size, rva, raw_size, raw in self.sections:
            if raw <= offset < raw + raw_size:
                return self.base + rva + offset - raw
        raise ValueError(offset)
    def function(self, va):
        i = bisect.bisect_right(self.starts, va - self.base) - 1
        if i >= 0 and va - self.base < self.functions[i][1]:
            return self.base + self.functions[i][0]
        return None

if __name__ == '__main__':
    for path in sys.argv[1:]:
        pe = PE(path)
        targets = {}
        for name in ['loaderInfo', 'LoaderInfo', 'bytesLoaded', 'bytesTotal',
                     'contentLoaderInfo', 'DisplayObject', 'ProgressEvent']:
            needle = name.encode() + b'\0'
            offset = pe.data.find(needle)
            while offset >= 0:
                targets[pe.va(offset)] = name
                offset = pe.data.find(needle, offset + 1)
        print(path, [(name, hex(va)) for va, name in targets.items()])
        text = next(s for s in pe.sections if s[0].rstrip(b'\0') == b'.text')
        for p in range(text[4], text[4] + text[1] - 7):
            # LEA/MOV register,[RIP+disp32], with optional REX.W/R prefix.
            rex = pe.data[p] in (0x48, 0x4c)
            if not rex and pe.data[p - 1] in (0x48, 0x4c):
                continue
            opcode = p + int(rex)
            if pe.data[opcode] not in (0x8d, 0x8b) or pe.data[opcode + 1] & 0xc7 != 5:
                continue
            end = opcode + 6
            target = pe.va(end) + struct.unpack_from('<i', pe.data, opcode + 2)[0]
            if target in targets:
                print(targets[target], 'xref', hex(pe.va(p)),
                      'function', hex(pe.function(pe.va(p)) or 0))
