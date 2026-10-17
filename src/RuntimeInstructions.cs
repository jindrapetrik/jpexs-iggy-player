using System;
using System.Collections.Generic;
using System.IO;

namespace IggyViewer {
    internal sealed partial class RuntimeAbc {
        sealed class RuntimeInstruction {
            internal int Old,Offset,Opcode;
            internal string Format;
            internal readonly List<int> Operands=new List<int>();
            internal readonly List<int> Targets=new List<int>();
        }
        static int VarSize(int value) {uint n=(uint)value;int size=1;while(n>=128){size++;n>>=7;}return size;}
        int ReadS24() {int value=Byte()|Byte()<<8|Byte()<<16;return value<<8>>8;}
        static void WriteS24(BinaryWriter w,int value) {
            if(value< -8388608||value>8388607)throw new InvalidDataException("AVM2 branch exceeds signed 24-bit range.");
            w.Write((byte)value);w.Write((byte)(value>>8));w.Write((byte)(value>>16));
        }
        static int MapOffset(Dictionary<int,int> offsets,int old) {
            int result;if(!offsets.TryGetValue(old,out result))throw new InvalidDataException("AVM2 branch/exception is not on an instruction boundary.");return result;
        }
        byte[] RewritePropertyNames(int start,int length,Dictionary<int,int> writes,bool script,int method, out Dictionary<int,int> offsets) {
            List<RuntimeInstruction> instructions=new List<RuntimeInstruction>();cursor=start;
            while(cursor<start+length) {
                RuntimeInstruction instruction=new RuntimeInstruction {Old=cursor-start,Opcode=Byte()};
                instruction.Format=opcodeFormats[instruction.Opcode];
                if(instruction.Format=="?")throw new InvalidDataException("Unknown AVM2 opcode while isolating movie classes.");
                if(instruction.Opcode==0x1b) {
                    instruction.Targets.Add(instruction.Old+ReadS24());int count=Count();instruction.Operands.Add(count);
                    for(int i=0;i<=count;i++)instruction.Targets.Add(instruction.Old+ReadS24());
                } else foreach(char encoding in instruction.Format) {
                    if(encoding=='u'||encoding=='n')instruction.Operands.Add(unchecked((int)Var()));
                    else if(encoding=='b')instruction.Operands.Add(Byte());
                    else if(encoding=='j')instruction.Targets.Add(cursor-start+3+ReadS24());
                    else throw new InvalidDataException("Unexpected AVM2 operand layout.");
                }
                instructions.Add(instruction);
            }
            if(cursor!=start+length)throw new InvalidDataException("Truncated AVM2 instruction.");
            int bodyEnd=cursor,exceptions=Count();List<int> handlers=new List<int>();
            for(int i=0;i<exceptions;i++){Var();Var();handlers.Add(Count());Var();Var();}cursor=bodyEnd;
            RestoreMemberOperands(instructions,writes,script,method,handlers);
            offsets=new Dictionary<int,int>();int at=0;
            foreach(RuntimeInstruction instruction in instructions) {
                instruction.Offset=at;offsets.Add(instruction.Old,at);at++;
                if(instruction.Opcode==0x1b)at+=3+VarSize(instruction.Operands[0])+3*(instruction.Operands[0]+1);
                else {
                    int operand=0;foreach(char encoding in instruction.Format) {
                        if(encoding=='j')at+=3;
                        else {int value=instruction.Operands[operand++];at+=encoding=='b'?1:VarSize(value);}
                    }
                }
            }
            offsets.Add(length,at);
            using(MemoryStream output=new MemoryStream())using(BinaryWriter w=new BinaryWriter(output)) {
                foreach(RuntimeInstruction instruction in instructions) {
                    w.Write((byte)instruction.Opcode);
                    if(instruction.Opcode==0x1b) {
                        WriteS24(w,MapOffset(offsets,instruction.Targets[0])-instruction.Offset);WriteVar(w,instruction.Operands[0]);
                        for(int i=1;i<instruction.Targets.Count;i++)WriteS24(w,MapOffset(offsets,instruction.Targets[i])-instruction.Offset);
                    } else {
                        int operand=0,target=0;
                        foreach(char encoding in instruction.Format) {
                            if(encoding=='j')WriteS24(w,MapOffset(offsets,instruction.Targets[target++])-instruction.Offset-4);
                            else if(encoding=='b')w.Write((byte)instruction.Operands[operand++]);
                            else WriteVar(w,instruction.Operands[operand++]);
                        }
                    }
                }
                return output.ToArray();
            }
        }
        void WriteTraitSpan(BinaryWriter w,int start,int end,Dictionary<int,int> aliases) {
            int previous=start;
            foreach(Tuple<int,int,int> trait in traitNames) {
                int replacement;if(trait.Item1<start||trait.Item1>=end||trait.Item3==4||!aliases.TryGetValue(trait.Item2,out replacement))continue;
                w.Write(bytes,previous,trait.Item1-previous);WriteVar(w,replacement);cursor=trait.Item1;Var();previous=cursor;
            }
            w.Write(bytes,previous,end-previous);
        }
        void RewriteClassBodies(BinaryWriter w,Dictionary<int,int> writes) {
            WriteTraitSpan(w,tail,bodiesStart,writes);cursor=bodiesStart;int count=Count();WriteVar(w,count);
            for(int i=0;i<count;i++) {
                int method=Count(),stack=Count(),locals=Count(),initial=Count(),scope=Count(),length=Count(),start=cursor;
                Dictionary<int,int> offsets;byte[] code=RewritePropertyNames(start,length,writes,scriptInitializers.Contains(method),method,out offsets);
                WriteVar(w,method);WriteVar(w,stack);WriteVar(w,locals);WriteVar(w,initial);WriteVar(w,scope);WriteVar(w,code.Length);w.Write(code);
                int exceptions=Count();WriteVar(w,exceptions);
                for(int n=0;n<exceptions;n++)for(int field=0;field<5;field++){int value=Count();WriteVar(w,field<3?MapOffset(offsets,value):value);}
                int traits=cursor;Traits();int end=cursor;WriteTraitSpan(w,traits,end,writes);cursor=end;
            }
            if(cursor!=bytes.Length)throw new InvalidDataException("Unexpected ABC body trailer.");
        }
    }
}
