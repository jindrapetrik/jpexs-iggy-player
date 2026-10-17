using System;
using System.Collections.Generic;
using System.IO;

namespace IggyViewer {
    internal sealed partial class RuntimeAbc {
        sealed class NameState {
            internal List<int> Stack;
            internal Dictionary<int,int> Locals;
        }
        void RestoreMemberOperands(List<RuntimeInstruction> code,Dictionary<int,int> aliases,bool script,int method,List<int> handlers) {
            if(code.Count==0)return;
            Dictionary<int,int> indices=new Dictionary<int,int>();for(int i=0;i<code.Count;i++)indices[code[i].Old]=i;
            Dictionary<int,NameState> states=new Dictionary<int,NameState>();Queue<int> pending=new Queue<int>();
            MergeNameState(0,new List<int>(),new Dictionary<int,int>(),states,pending);
            foreach(int handler in handlers)MergeNameState(handler,new List<int>{0},new Dictionary<int,int>(),states,pending);
            HashSet<int> members;methodMembers.TryGetValue(method,out members);
            while(pending.Count>0) {
                int address=pending.Dequeue(),index;if(!indices.TryGetValue(address,out index))continue;
                RuntimeInstruction instruction=code[index];List<int> stack=new List<int>(states[address].Stack);
                Dictionary<int,int> locals=new Dictionary<int,int>(states[address].Locals);int[] effect=stackEffects[instruction.Opcode];
                int pop=effect[0]+(effect[2]<0?0:instruction.Operands[effect[2]]*effect[3]),push=effect[1];
                int nameOperand=instruction.Format.IndexOf('n');
                if(nameOperand>=0) {
                    int kind=names[instruction.Operands[nameOperand]].Kind;
                    pop+=kind==17||kind==18?2:kind==15||kind==16||kind==27||kind==28?1:0;
                }
                if(pop<0||pop>stack.Count)throw new InvalidDataException("Invalid AVM2 stack while isolating class names.");
                int marker=0;
                if((instruction.Opcode==0x5d||instruction.Opcode==0x5e||instruction.Opcode==0x5f)&&aliases.ContainsKey(instruction.Operands[0])&&(members==null||!members.Contains(instruction.Operands[0])))marker=instruction.Operands[0]+1;
                int local=instruction.Opcode==0x62||instruction.Opcode==0x63||instruction.Opcode==8?instruction.Operands[0]:
                    instruction.Opcode>=0xd0&&instruction.Opcode<=0xd7?(instruction.Opcode&3):-1;
                if(instruction.Opcode==0x62||(instruction.Opcode>=0xd0&&instruction.Opcode<=0xd3))locals.TryGetValue(local,out marker);
                if(instruction.Opcode==0x63||(instruction.Opcode>=0xd4&&instruction.Opcode<=0xd7)) {
                    int value=stack[stack.Count-1];if(value==0)locals.Remove(local);else locals[local]=value;
                }
                if(instruction.Opcode==8)locals.Remove(local);
                if(instruction.Opcode==0x2a){int top=stack[stack.Count-1];stack.Add(top);}
                else if(instruction.Opcode==0x2b){int top=stack[stack.Count-1];stack[stack.Count-1]=stack[stack.Count-2];stack[stack.Count-2]=top;}
                else {stack.RemoveRange(stack.Count-pop,pop);for(int i=0;i<push;i++)stack.Add(i==0?marker:0);}
                if(instruction.Opcode==3||instruction.Opcode==0x47||instruction.Opcode==0x48)continue;
                foreach(int target in instruction.Targets)MergeNameState(target,stack,locals,states,pending);
                if(instruction.Opcode!=0x10&&instruction.Opcode!=0x1b&&index+1<code.Count)MergeNameState(code[index+1].Old,stack,locals,states,pending);
            }
            foreach(RuntimeInstruction instruction in code) {
                int original=instruction.Operands.Count==0?0:instruction.Operands[0],replacement;
                if(!aliases.TryGetValue(original,out replacement))continue;
                bool restore=false;
                switch(instruction.Opcode) {
                    case 0x5d:case 0x5e:case 0x5f:case 0x60:restore=members!=null&&members.Contains(original);break;
                    case 0x04:case 0x05:case 0x59:case 0x6a:restore=true;break;
                    case 0x68:restore=!script;break;
                    case 0x45:case 0x46:case 0x4a:case 0x4c:case 0x4e:case 0x4f:case 0x66:case 0x61:
                        NameState state;int arguments=instruction.Opcode==0x61?1:instruction.Opcode==0x66?0:instruction.Operands[1];
                        restore=!states.TryGetValue(instruction.Old,out state)||state.Stack.Count<=arguments||state.Stack[state.Stack.Count-arguments-1]!=original+1;break;
                }
                if(restore)instruction.Operands[0]=replacement;
            }
        }
        static void MergeNameState(int target,List<int> stack,Dictionary<int,int> locals,Dictionary<int,NameState> states,Queue<int> pending) {
            NameState old;
            if(!states.TryGetValue(target,out old)){states[target]=new NameState {Stack=new List<int>(stack),Locals=new Dictionary<int,int>(locals)};pending.Enqueue(target);return;}
            if(old.Stack.Count!=stack.Count)throw new InvalidDataException("AVM2 stack depths disagree at a branch target.");
            bool changed=false;for(int i=0;i<old.Stack.Count;i++)if(old.Stack[i]!=0&&old.Stack[i]!=stack[i]){old.Stack[i]=0;changed=true;}
            foreach(int local in new List<int>(old.Locals.Keys)) {
                int value;if(!locals.TryGetValue(local,out value)||value!=old.Locals[local]){old.Locals.Remove(local);changed=true;}
            }
            if(changed)pending.Enqueue(target);
        }
    }
}
