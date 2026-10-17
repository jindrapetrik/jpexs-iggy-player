using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IggyViewer {
    internal sealed partial class RuntimeAbc {
        // Iggy associates lazy script initializers only with class traits.
        // A unique unused class trait gives each script the same lifetime and
        // initialization path, even if its user class reuses a parent definition.
        // Its constructor is never instantiated.
        internal byte[] BindGlobals() {
            List<Script> pending=scripts.FindAll(script=>script.Globals.Count!=0&&(script.Anchor==null||!script.Anchor.StartsWith("com.jpexs.iggy.globals.",StringComparison.Ordinal)));
            if(pending.Count==0)return bytes;
            string package="com.jpexs.iggy.globals.g"+Guid.NewGuid().ToString("N");
            List<string> texts=new List<string>(strings);texts.Add(package);int packageText=texts.Count-1;
            texts.Add("Object");int objectText=texts.Count-1;
            for(int i=0;i<pending.Count;i++)texts.Add("Script"+i);
            int objectName=names.Count,firstName=objectName+1;
            using(MemoryStream output=new MemoryStream())using(BinaryWriter w=new BinaryWriter(output)) {
                w.Write(bytes,0,stringsStart);WriteVar(w,texts.Count);
                for(int i=1;i<texts.Count;i++){byte[] text=Encoding.UTF8.GetBytes(texts[i]);WriteVar(w,text.Length);w.Write(text);}
                WriteVar(w,namespaces.Count+2);
                for(int i=1;i<namespaces.Count;i++){w.Write(namespaceKinds[i]);WriteVar(w,namespaces[i]);}
                w.Write((byte)0x16);WriteVar(w,packageText);w.Write((byte)0x16);WriteVar(w,0);
                w.Write(bytes,setsStart,setsEnd-setsStart);WriteVar(w,names.Count+1+pending.Count);
                for(int i=1;i<names.Count;i++)w.Write(names[i].Raw);
                w.Write((byte)7);WriteVar(w,namespaces.Count+1);WriteVar(w,objectText);
                for(int i=0;i<pending.Count;i++){w.Write((byte)7);WriteVar(w,namespaces.Count);WriteVar(w,objectText+1+i);}
                WriteVar(w,methodCount+2*pending.Count);cursor=tail;Count();w.Write(bytes,cursor,methodsEnd-cursor);
                for(int i=0;i<2*pending.Count;i++){WriteVar(w,0);WriteVar(w,0);WriteVar(w,0);w.Write((byte)0);}
                w.Write(bytes,methodsEnd,classCountAt-methodsEnd);WriteVar(w,classCount+pending.Count);
                w.Write(bytes,instancesStart,classInfoStart-instancesStart);
                for(int i=0;i<pending.Count;i++) {
                    WriteVar(w,firstName+i);WriteVar(w,objectName);w.Write((byte)0);WriteVar(w,0);
                    WriteVar(w,methodCount+2*i);WriteVar(w,0);
                }
                w.Write(bytes,classInfoStart,scriptsStart-classInfoStart);
                for(int i=0;i<pending.Count;i++){WriteVar(w,methodCount+2*i+1);WriteVar(w,0);}
                WriteVar(w,scripts.Count);
                foreach(Script script in scripts) {
                    WriteVar(w,script.Initializer);cursor=script.TraitsStart;int count=Count(),index=pending.IndexOf(script);
                    WriteVar(w,count+(index>=0?1:0));w.Write(bytes,cursor,script.End-cursor);
                    if(index>=0){WriteVar(w,firstName+index);w.Write((byte)4);WriteVar(w,0);WriteVar(w,classCount+index);}
                }
                cursor=bodiesStart;int bodies=Count();WriteVar(w,bodies+2*pending.Count);w.Write(bytes,cursor,bytes.Length-cursor);
                for(int i=0;i<pending.Count;i++) {
                    WriteGlobalAnchorBody(w,methodCount+2*i,new byte[]{0xd0,0x30,0xd0,0x49,0,0x47});
                    WriteGlobalAnchorBody(w,methodCount+2*i+1,new byte[]{0xd0,0x30,0x47});
                }
                return output.ToArray();
            }
        }
        static void WriteGlobalAnchorBody(BinaryWriter w,int method,byte[] code) {
            WriteVar(w,method);WriteVar(w,1);WriteVar(w,1);WriteVar(w,0);WriteVar(w,1);WriteVar(w,code.Length);
            w.Write(code);WriteVar(w,0);WriteVar(w,0);
        }
    }
}
