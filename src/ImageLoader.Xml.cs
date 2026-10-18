using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml;
using System.Text;

namespace IggyViewer {
    internal unsafe sealed partial class ImageLoader {
        sealed class XmlValue {internal IntPtr Reference;internal XmlDocumentFragment Node;}
        readonly List<XmlValue> xmlValues=new List<XmlValue>();
        readonly List<Builtin> xmlCallbacks=new List<Builtin>();
        bool xmlPretty=true,xmlIgnoreComments=true,xmlIgnoreInstructions=true,xmlIgnoreWhitespace=true;
        int xmlIndent=2;
        IntPtr xmlMetadata;
        void XmlSettings() {
            xmlPretty=native.Activity.True(native.AmfClassOption(xmlMetadata,"prettyPrinting"));
            xmlIgnoreComments=native.Activity.True(native.AmfClassOption(xmlMetadata,"ignoreComments"));
            xmlIgnoreInstructions=native.Activity.True(native.AmfClassOption(xmlMetadata,"ignoreProcessingInstructions"));
            xmlIgnoreWhitespace=native.Activity.True(native.AmfClassOption(xmlMetadata,"ignoreWhitespace"));
            int indent;if(int.TryParse(native.AmfString(native.AmfClassOption(xmlMetadata,"prettyIndent")),out indent))xmlIndent=Math.Max(0,Math.Min(32,indent));
        }
        static XmlDocumentFragment ParseXml(string source,bool document=false) {
            if(source.Length>16*1024*1024)throw new InvalidDataException("XML exceeds 16 MiB.");
            XmlDocument owner=new XmlDocument {PreserveWhitespace=true,XmlResolver=null};
            XmlDocumentFragment fragment=owner.CreateDocumentFragment();
            XmlReaderSettings settings=new XmlReaderSettings {ConformanceLevel=document?ConformanceLevel.Auto:ConformanceLevel.Fragment,DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=16*1024*1024};
            // Check depth before DOM construction and recursive formatting.
            using(XmlReader check=XmlReader.Create(new StringReader(source),settings))while(check.Read())if(check.Depth>=128)throw new XmlException("XML nesting limit exceeded.");
            using(XmlReader reader=XmlReader.Create(new StringReader(source),settings)) {
                while(!reader.EOF) {if(reader.ReadState==ReadState.Initial){reader.Read();continue;}XmlNode node=owner.ReadNode(reader);if(node!=null&&node.NodeType!=XmlNodeType.XmlDeclaration)fragment.AppendChild(node);}
            }
            if(!document&&fragment.ChildNodes.Count>1) {
                int significant=0;foreach(XmlNode child in fragment.ChildNodes)if(child.NodeType!=XmlNodeType.Whitespace&&child.NodeType!=XmlNodeType.SignificantWhitespace)significant++;
                if(significant>1)throw new XmlException("XML requires one root node.");
            }
            return fragment;
        }
        XmlValue XmlObject(IntPtr value) {foreach(XmlValue item in xmlValues)if(Marshal.ReadIntPtr(item.Reference)==value)return item;return null;}
        void FilterXml(XmlNode node) {
            for(int i=node.ChildNodes.Count-1;i>=0;i--) {
                XmlNode child=node.ChildNodes[i];
                if(xmlIgnoreComments&&child.NodeType==XmlNodeType.Comment||xmlIgnoreInstructions&&child.NodeType==XmlNodeType.ProcessingInstruction||xmlIgnoreWhitespace&&(child.NodeType==XmlNodeType.Whitespace||child.NodeType==XmlNodeType.SignificantWhitespace))node.RemoveChild(child);
                else FilterXml(child);
            }
        }
        string XmlMarkup(XmlDocumentFragment node) {
            XmlSettings();
            if(!xmlPretty||node.FirstChild==null)return CompactEmptyXmlTags(node.InnerXml);
            using(StringWriter text=new StringWriter())using(XmlWriter writer=XmlWriter.Create(text,new XmlWriterSettings {OmitXmlDeclaration=true,ConformanceLevel=ConformanceLevel.Fragment,Indent=true,IndentChars=new string(' ',xmlIndent),NewLineChars="\n",NewLineHandling=NewLineHandling.None})) {
                node.WriteContentTo(writer);writer.Flush();return CompactEmptyXmlTags(text.ToString());
            }
        }
        static string CompactEmptyXmlTags(string source) {
            StringBuilder output=new StringBuilder(source.Length);bool tag=false;char quote='\0';
            for(int i=0;i<source.Length;i++) {
                char next=source[i];
                if(next=='<'&&i+1<source.Length&&(source[i+1]=='!'||source[i+1]=='?')) {
                    string end=source.IndexOf("<![CDATA[",i,StringComparison.Ordinal)==i?"]]>":source.IndexOf("<!--",i,StringComparison.Ordinal)==i?"-->":"?>";
                    int close=source.IndexOf(end,i+2,StringComparison.Ordinal);if(close>=0){output.Append(source,i,close+end.Length-i);i=close+end.Length-1;continue;}
                }
                if(quote!='\0'){if(next==quote)quote='\0';}
                else if(tag&&(next=='\''||next=='"'))quote=next;
                else if(next=='<')tag=true;
                else if(next=='>')tag=false;
                if(tag&&quote=='\0'&&next==' '&&i+2<source.Length&&source[i+1]=='/'&&source[i+2]=='>')continue;
                output.Append(next);
            }
            return output.ToString();
        }
        IntPtr XmlOperation(IntPtr context,string operation) {
            try {
                IntPtr arguments=Marshal.ReadIntPtr(context,24),receiver=Marshal.ReadIntPtr(arguments);
                if(operation=="constructor") {
                    XmlSettings();
                    string source="";if(Marshal.ReadInt32(context,16)>0) {IntPtr input=Marshal.ReadIntPtr(arguments,8);XmlValue original=XmlObject(input);source=original==null?native.AmfString(input):original.Node.InnerXml;}
                    XmlDocumentFragment node=ParseXml(source);FilterXml(node);
                    xmlValues.Add(new XmlValue {Reference=reference(native.Player,Marshal.ReadIntPtr(arguments),1),Node=node});return IntPtr.Zero;
                }
                XmlValue item=XmlObject(receiver);if(item==null)throw new InvalidDataException("XML has no document value.");
                string result=XmlMarkup(item.Node);
                if(operation=="toString") {bool complex=false;foreach(XmlNode child in item.Node.ChildNodes)foreach(XmlNode member in child.ChildNodes)if(member.NodeType==XmlNodeType.Element)complex=true;if(!complex)result=item.Node.InnerText;}
                return native.ReturnMovieString(context,result);
            } catch(Exception ex) {
                if(!(ex is XmlException)&&!(ex is InvalidDataException))throw;
                return native.RaiseByteArrayError(Marshal.ReadIntPtr(context),"TypeError",ex.Message);
            }
        }
        void InstallAmfXml(IntPtr metadata) {
            xmlMetadata=metadata;
            Builtin constructor=context=>XmlOperation(context,"constructor");xmlCallbacks.Add(constructor);Marshal.WriteIntPtr(metadata,56,Marshal.GetFunctionPointerForDelegate(constructor));
            foreach(int offset in new int[]{72}) {
                IntPtr traits=Marshal.ReadIntPtr(metadata,offset);
                foreach(int table in new int[]{64,72,80}) {
                    int count=(ushort)Marshal.ReadInt16(traits,table==64?22:table==72?26:24);IntPtr entries=Marshal.ReadIntPtr(traits,table);
                    for(int i=0;i<count;i++) {
                        IntPtr function=Marshal.ReadIntPtr(entries,8*i);if(table==64)function=IntPtr.Subtract(function,1);
                        if(function.ToInt64()<65536||Marshal.ReadByte(function)!=16)continue;
                        IntPtr text=Marshal.ReadIntPtr(function,8);string name=Marshal.PtrToStringUni(IntPtr.Add(text,52),Marshal.ReadInt32(text,48));
                        if(name!="toString"&&name!="toXMLString")continue;
                        string operation=(table==80?"set_":"")+name;Builtin callback=context=>XmlOperation(context,operation);xmlCallbacks.Add(callback);Marshal.WriteIntPtr(function,16,Marshal.GetFunctionPointerForDelegate(callback));
                    }
                }
            }

        }
        void PruneXml() {for(int i=xmlValues.Count-1;i>=0;i--)if(Marshal.ReadIntPtr(xmlValues[i].Reference).ToInt64()==7){freeReference(native.Player,xmlValues[i].Reference);xmlValues.RemoveAt(i);}}
    }
}
