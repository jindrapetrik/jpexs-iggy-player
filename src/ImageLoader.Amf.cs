using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace IggyViewer {
    internal unsafe sealed partial class ImageLoader {
        sealed class AmfRequest {internal IntPtr Request,Value;internal string Operation,Text,Extra;}
        readonly List<AmfRequest> amfRequests=new List<AmfRequest>();
        void StartAmfRequest(IntPtr info,IntPtr arguments) {
            string operation=ReadText(Marshal.ReadIntPtr(arguments,16));
            amfRequests.Add(new AmfRequest {Request=reference(native.Player,info,1),Operation=operation,
                Value=operation=="utf"?reference(native.Player,Marshal.ReadIntPtr(arguments,24),0):IntPtr.Zero,
                Text=operation=="schema"?ReadText(Marshal.ReadIntPtr(arguments,24)):null,Extra=ReadText(Marshal.ReadIntPtr(arguments,32))});
        }
        bool AmfContent(IntPtr context,IntPtr receiver,out IntPtr result) {
            result=IntPtr.Zero;
            for(int i=0;i<amfRequests.Count;i++)if(Marshal.ReadIntPtr(amfRequests[i].Request)==receiver) {
                AmfRequest request=amfRequests[i];amfRequests.RemoveAt(i);
                try {
                    string text;
                    if(request.Operation=="schema")text=native.AmfSchema(request.Text??"");
                    else if(request.Operation=="utf") {
                        int length;if(!int.TryParse(request.Extra,out length)||length<0||length>16*1024*1024)throw new InvalidDataException("AMF string exceeds the size limit.");
                        text=native.ReadAmfText(Marshal.ReadIntPtr(request.Value),(uint)length);
                    } else throw new InvalidDataException("Unknown AMF request.");
                    result=native.ReturnMovieString(context,text);
                } catch(Exception ex) {
                    if(!(ex is EndOfStreamException)&&!(ex is InvalidDataException))throw;
                    result=native.RaiseByteArrayError(Marshal.ReadIntPtr(context),ex is EndOfStreamException?"EOFError":"RangeError",ex.Message);
                } finally {FreeAmfRequest(request);}
                return true;
            }
            return false;
        }
        void FreeAmfRequest(AmfRequest request) {freeReference(native.Player,request.Request);if(request.Value!=IntPtr.Zero)freeReference(native.Player,request.Value);}
        void PruneAmfRequests() {for(int i=amfRequests.Count-1;i>=0;i--)if(Marshal.ReadIntPtr(amfRequests[i].Request).ToInt64()==7){FreeAmfRequest(amfRequests[i]);amfRequests.RemoveAt(i);}}
    }
}
