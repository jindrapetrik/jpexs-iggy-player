using System;
using System.Collections.Generic;
using System.IO;

namespace IggyViewer {
    internal sealed partial class RuntimeAbc {
        void RestoreMemberOperands(List<RuntimeInstruction> code,Dictionary<int,int> aliases,bool script,int method,List<int> handlers) {
            if(code.Count==0)return;
            Dictionary<int,int> indices=new Dictionary<int,int>();for(int i=0;i<code.Count;i++)indices[code[i].Old]=i;
            Dictionary<int,List<int>> states=new Dictionary<int,List<int>>();Queue<int> pending=new Queue<int>();
            MergeNameStack(0,new List<int>(),states,pending);
            foreach(int handler in handlers)MergeNameStack(handler,new List<int>{0},states,pending);
            HashSet<int> members;methodMembers.TryGetValue(method,out members);
            while(pending.Count>0) {
                int address=pending.Dequeue(),index;if(!indices.TryGetValue(address,out index))continue;
                RuntimeInstruction instruction=code[index];List<int> stack=new List<int>(states[address]);int[] effect=stackEffects[instruction.Opcode];
                int pop=effect[0]+(effect[2]<0?0:instruction.Operands[effect[2]]*effect[3]),push=effect[1];
                int nameOperand=instruction.Format.IndexOf('n');
                if(nameOperand>=0) {
                    int kind=names[instruction.Operands[nameOperand]].Kind;
                    pop+=kind==17||kind==18?2:kind==15||kind==16||kind==27||kind==28?1:0;
                }
                if(pop<0||pop>stack.Count)throw new InvalidDataException("Invalid AVM2 stack while isolating class names.");
                int marker=0;
                if((instruction.Opcode==0x5d||instruction.Opcode==0x5e)&&aliases.ContainsKey(instruction.Operands[0])&&(members==null||!members.Contains(instruction.Operands[0])))marker=instruction.Operands[0]+1;
                if(instruction.Opcode==0x2a){int top=stack[stack.Count-1];stack.Add(top);}
                else if(instruction.Opcode==0x2b){int top=stack[stack.Count-1];stack[stack.Count-1]=stack[stack.Count-2];stack[stack.Count-2]=top;}
                else {stack.RemoveRange(stack.Count-pop,pop);for(int i=0;i<push;i++)stack.Add(i==0?marker:0);}
                if(instruction.Opcode==3||instruction.Opcode==0x47||instruction.Opcode==0x48)continue;
                foreach(int target in instruction.Targets)MergeNameStack(target,stack,states,pending);
                if(instruction.Opcode!=0x10&&instruction.Opcode!=0x1b&&index+1<code.Count)MergeNameStack(code[index+1].Old,stack,states,pending);
            }
            foreach(RuntimeInstruction instruction in code) {
                int original=instruction.Operands.Count==0?0:instruction.Operands[0],replacement;
                if(!aliases.TryGetValue(original,out replacement))continue;
                bool restore=false;
                switch(instruction.Opcode) {
                    case 0x5d:case 0x5e:case 0x5f:case 0x60:restore=members!=null&&members.Contains(original);break;
                    case 0x04:case 0x05:case 0x61:case 0x59:case 0x6a:restore=true;break;
                    case 0x68:restore=!script;break;
                    case 0x45:case 0x46:case 0x4a:case 0x4c:case 0x4e:case 0x4f:case 0x66:
                        List<int> stack;int arguments=instruction.Opcode==0x66?0:instruction.Operands[1];
                        restore=!states.TryGetValue(instruction.Old,out stack)||stack.Count<=arguments||stack[stack.Count-arguments-1]!=original+1;break;
                }
                if(restore)instruction.Operands[0]=replacement;
            }
        }
        static void MergeNameStack(int target,List<int> stack,Dictionary<int,List<int>> states,Queue<int> pending) {
            List<int> old;
            if(!states.TryGetValue(target,out old)){states[target]=new List<int>(stack);pending.Enqueue(target);return;}
            if(old.Count!=stack.Count)throw new InvalidDataException("AVM2 stack depths disagree at a branch target.");
            bool changed=false;for(int i=0;i<old.Count;i++)if(old[i]!=0&&old[i]!=stack[i]){old[i]=0;changed=true;}
            if(changed)pending.Enqueue(target);
        }
    }
}
