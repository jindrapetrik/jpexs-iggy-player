using System;
using System.Collections.Generic;
using System.Linq;

namespace IggyViewer {
    internal sealed class AmfClassSchema {
        internal string Name,Parent;
        internal bool Dynamic,External;
        internal readonly HashSet<string> Variables=new HashSet<string>(),Getters=new HashSet<string>(),Setters=new HashSet<string>(),Transient=new HashSet<string>();
    }
    internal unsafe sealed partial class Native {
        readonly Dictionary<string,AmfClassSchema> amfSchemas=new Dictionary<string,AmfClassSchema>(StringComparer.Ordinal);
        internal string AmfSchema(string name) {
            AmfClassSchema schema;if(!amfSchemas.TryGetValue(name,out schema)&&!amfSchemas.TryGetValue("::"+name,out schema))return "?";
            List<AmfClassSchema> chain=new List<AmfClassSchema>();AmfClassSchema current=schema;
            for(int depth=0;current!=null&&depth<256;depth++){chain.Add(current);amfSchemas.TryGetValue(current.Parent??"",out current);}
            HashSet<string> fields=new HashSet<string>();bool external=false;
            for(int i=chain.Count-1;i>=0;i--) {
                current=chain[i];fields.UnionWith(current.Variables);
                foreach(string getter in current.Getters)if(chain.Exists(item=>item.Setters.Contains(getter)))fields.Add(getter);
                fields.ExceptWith(current.Transient);external|=current.External;
            }
            return (schema.Dynamic?"1":"0")+"\n"+(external?"1":"0")+"\n"+string.Join("\n",fields.OrderBy(field=>field,StringComparer.Ordinal));
        }
    }
}
