using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace IggyViewer {
    internal unsafe sealed partial class ImageLoader {
        sealed class AmfRequest {internal IntPtr Request,Value,Payload;internal string Operation,Text,Extra;}
        internal SharedObjectStorage Storage;
        readonly List<AmfRequest> amfRequests=new List<AmfRequest>();
        readonly List<IntPtr> weakDictionaries=new List<IntPtr>();
        Builtin dictionaryConstructor,originalDictionaryConstructor;
        void InstallAmfDictionary(IntPtr metadata) {
            originalDictionaryConstructor=(Builtin)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(metadata,56),typeof(Builtin));
            dictionaryConstructor=context=> {
                IntPtr result=originalDictionaryConstructor(context),arguments=Marshal.ReadIntPtr(context,24);
                if(Marshal.ReadInt32(context,16)>0&&native.Activity.True(Marshal.ReadIntPtr(arguments,8)))weakDictionaries.Add(reference(native.Player,Marshal.ReadIntPtr(arguments),1));
                return result;
            };
            Marshal.WriteIntPtr(metadata,56,Marshal.GetFunctionPointerForDelegate(dictionaryConstructor));
        }
        string DictionaryWeak(IntPtr value) {foreach(IntPtr item in weakDictionaries)if(Marshal.ReadIntPtr(item)==value)return "1";return "0";}
        void StartAmfRequest(IntPtr info,IntPtr arguments) {
            string operation=ReadText(Marshal.ReadIntPtr(arguments,16))??"";
            amfRequests.Add(new AmfRequest {Request=reference(native.Player,info,1),Operation=operation,
                Value=operation=="utf"||operation=="weak"||operation=="vector-type"||operation=="vector-class"||operation=="xml-document"||operation=="base64-encode"||operation=="base64-decode"||operation.StartsWith("store-",StringComparison.Ordinal)?reference(native.Player,Marshal.ReadIntPtr(arguments,24),0):IntPtr.Zero,
                Payload=operation=="base64-decode"||operation=="store-write"||operation=="store-track"?reference(native.Player,Marshal.ReadIntPtr(arguments,32),0):IntPtr.Zero,
                Text=operation=="schema"?ReadText(Marshal.ReadIntPtr(arguments,24)):null,Extra=ReadText(Marshal.ReadIntPtr(arguments,32))});
        }
        bool AmfContent(IntPtr context,IntPtr receiver,out IntPtr result) {
            result=IntPtr.Zero;
            for(int i=0;i<amfRequests.Count;i++)if(Marshal.ReadIntPtr(amfRequests[i].Request)==receiver) {
                AmfRequest request=amfRequests[i];amfRequests.RemoveAt(i);
                try {
                    if(request.Operation=="vector-type"||request.Operation=="vector-class") {
                        IntPtr value=Marshal.ReadIntPtr(request.Value),answer=request.Operation=="vector-type"?native.AmfVectorType(value):native.AmfVectorClass(value);
                        Marshal.WriteIntPtr(Marshal.ReadIntPtr(context,8),answer==IntPtr.Zero?nullValue:answer);return true;
                    }
                    if(request.Operation.StartsWith("store-",StringComparison.Ordinal)) {
                        if(Storage==null)throw new InvalidDataException("SharedObject storage is unavailable.");
                        if(request.Operation=="store-track") {
                            Storage.Track(Marshal.ReadIntPtr(request.Value),native.AmfString(Marshal.ReadIntPtr(request.Payload),2048));
                            Marshal.WriteIntPtr(Marshal.ReadIntPtr(context,8),native.AmfBoolean(true));return true;
                        }
                        object answer=Storage.Execute(request.Operation.Substring(6),native.AmfString(Marshal.ReadIntPtr(request.Value),2048),request.Payload==IntPtr.Zero?null:native.AmfString(Marshal.ReadIntPtr(request.Payload),24*1024*1024));
                        if(answer is string)result=native.ReturnMovieString(context,(string)answer);
                        else Marshal.WriteIntPtr(Marshal.ReadIntPtr(context,8),answer==null?nullValue:native.AmfBoolean((bool)answer));
                        return true;
                    }
                    string text;
                    if(request.Operation=="schema")text=native.AmfSchema(request.Text??"");
                    else if(request.Operation=="base64-encode") {
                        if(request.Extra!="0"&&request.Extra!="3")throw new InvalidDataException("Invalid SharedObject encoding.");
                        text="IGGY2:"+request.Extra+":"+Convert.ToBase64String(native.CopyByteArray(Marshal.ReadIntPtr(request.Value),16*1024*1024));
                    }
                    else if(request.Operation=="base64-decode") {
                        string encoded=native.AmfString(Marshal.ReadIntPtr(request.Payload),24*1024*1024);
                        if(encoded.Length<8||(!encoded.StartsWith("IGGY2:0:",StringComparison.Ordinal)&&!encoded.StartsWith("IGGY2:3:",StringComparison.Ordinal)))throw new InvalidDataException("Invalid SharedObject encoding header.");
                        if(encoded.Length>22369632)throw new InvalidDataException("SharedObject AMF data exceeds 16 MiB.");
                        byte[] bytes=Convert.FromBase64String(encoded.Substring(8));native.SetSharedObjectBytes(Marshal.ReadIntPtr(request.Value),bytes);text="";
                    }
                    else if(request.Operation=="xml-document")text=ParseXml(native.AmfString(Marshal.ReadIntPtr(request.Value)),true).InnerXml;
                    else if(request.Operation=="weak")text=DictionaryWeak(Marshal.ReadIntPtr(request.Value));
                    else if(request.Operation=="utf") {
                        int length;if(!int.TryParse(request.Extra,out length)||length<0||length>16*1024*1024)throw new InvalidDataException("AMF string exceeds the size limit.");
                        text=native.ReadAmfText(Marshal.ReadIntPtr(request.Value),(uint)length);
                    } else throw new InvalidDataException("Unknown AMF request.");
                    result=native.ReturnMovieString(context,text);
                } catch(Exception ex) {
                    if(!(ex is EndOfStreamException)&&!(ex is InvalidDataException)&&!(ex is System.Xml.XmlException)&&!(ex is FormatException))throw;
                    result=native.RaiseByteArrayError(Marshal.ReadIntPtr(context),ex is EndOfStreamException?"EOFError":ex is System.Xml.XmlException||ex is FormatException?"TypeError":"RangeError",ex.Message);
                } finally {FreeAmfRequest(request);}
                return true;
            }
            return false;
        }
        void FreeAmfRequest(AmfRequest request) {freeReference(native.Player,request.Request);if(request.Value!=IntPtr.Zero)freeReference(native.Player,request.Value);if(request.Payload!=IntPtr.Zero)freeReference(native.Player,request.Payload);}
        void PruneAmfRequests() {
            for(int i=amfRequests.Count-1;i>=0;i--)if(Marshal.ReadIntPtr(amfRequests[i].Request).ToInt64()==7){FreeAmfRequest(amfRequests[i]);amfRequests.RemoveAt(i);}
            for(int i=weakDictionaries.Count-1;i>=0;i--)if(Marshal.ReadIntPtr(weakDictionaries[i]).ToInt64()==7){freeReference(native.Player,weakDictionaries[i]);weakDictionaries.RemoveAt(i);}
        }
    }
}
