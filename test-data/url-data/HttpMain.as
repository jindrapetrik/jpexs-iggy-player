package {
    import flash.display.MovieClip;
    import flash.display.Loader;
    import flash.net.URLLoader;
    import flash.net.URLRequest;
    import flash.net.URLRequestMethod;
    import flash.net.URLRequestHeader;
    import flash.net.URLVariables;
    import flash.events.Event;
    import flash.events.HTTPStatusEvent;
    import flash.events.ProgressEvent;
    import flash.events.IOErrorEvent;
    import flash.events.SecurityErrorEvent;
    import flash.utils.ByteArray;
    import flash.utils.getDefinitionByName;
    import flash.utils.getQualifiedClassName;
    public class HttpMain extends MovieClip {
        public var baseUrl:String;
        public var completed:uint;
        public var errors:uint;
        public var security:uint;
        public var failures:uint;
        public var checks:uint;
        public var details:String = "";
        public var cancelled:uint;
        public var cancelledEvents:uint;
        public var replaced:uint;
        public var sent:uint;
        public var active:URLLoader;
        public var child:Loader;
        private var retained:Array = [];
        public function HttpMain() { super(); stop(); }
        private function check(value:Boolean, label:String):void { checks++; if(!value){failures++;details+=label+",";} }
        public function start():void {
            check(getQualifiedClassName(HTTPStatusEvent)=="flash.events::HTTPStatusEvent" && getDefinitionByName("flash.events.HTTPStatusEvent")===HTTPStatusEvent,"status-reflection");
            watch("text","/text");
            watch("query","/query?word=%C5%BE%F0%9F%99%82&plus=%2B#ignored");
            watch("binary","/binary","binary");
            watch("variables","/variables","variables");
            watch("empty","/empty");
            watch("gzip","/gzip");
            watch("chunked","/chunked");
            watch("redirect","/redirect");
            watch("missing","/missing","text",404);
            watch("failed","/failed","text",500);
            watch("oversize","/oversize","text",200);
            watch("inflated","/gzip-limit","text",200);
            watch("truncated","/truncated","text",200);
            watch("loop","/loop","text",302);
            watch("unsafe","/unsafe","text",302,true);
            check(completed==0 && errors==0 && security==0,"async-return");
        }
        private function watch(label:String,path:String,format:String="text",failure:int=0,denied:Boolean=false):void {
            var loader:URLLoader=new URLLoader();retained.push(loader);loader.dataFormat=format;
            var opened:Boolean;var statuses:Array=[];var progress:Boolean;var unknown:Boolean;
            loader.addEventListener(Event.OPEN,function(event:Event):void {check(!opened && event.target===loader,"open-"+label);opened=true;});
            loader.addEventListener(HTTPStatusEvent.HTTP_STATUS,function(event:HTTPStatusEvent):void {
                var copy:HTTPStatusEvent=event.clone() as HTTPStatusEvent;
                check(opened && copy!=null && copy.status==event.status && copy.redirected==event.redirected && event.target===loader && event.toString().indexOf("HTTPStatusEvent")>=0,"status-"+label);
                if(label=="redirect" && event.status==200)check(event.redirected,"redirected");
                statuses.push(event.status);
            });
            loader.addEventListener(ProgressEvent.PROGRESS,function(event:ProgressEvent):void {progress=true;if(event.bytesLoaded>0 && event.bytesTotal==0)unknown=true;check(opened && (event.bytesTotal==0 || event.bytesLoaded<=event.bytesTotal),"progress-"+label);});
            loader.addEventListener(Event.COMPLETE,function(event:Event):void {
                completed++;check(failure==0 && progress && statuses.length>0 && statuses[statuses.length-1]==(label=="empty"?204:200),"complete-"+label);
                check(loader.bytesLoaded==loader.bytesTotal,"length-"+label);
                if(label=="binary"){var bytes:ByteArray=loader.data as ByteArray;check(bytes!=null && bytes.length==256 && bytes.position==0 && bytes.readUnsignedInt()==0x00010203,"binary");}
                else if(label=="variables"){var variables:URLVariables=loader.data as URLVariables;check(variables!=null && variables.name is Array && variables.name[1]=="two" && variables.word=="ž🙂","variables");}
                else check(loader.data==(label=="empty"?"":"ž🙂\u0000HTTP"),"text-"+label);
                if(label=="redirect")check(statuses.length==2 && statuses[0]==302,"redirect-chain");
                if(label=="chunked")check(unknown,"unknown-length-progress");
            });
            loader.addEventListener(IOErrorEvent.IO_ERROR,function(event:IOErrorEvent):void {errors++;if(failure==0 || denied)details+="io-"+label+":"+event.text+",";check(failure!=0 && !denied && statuses.length>0 && statuses[statuses.length-1]==(failure==-1?0:failure) && loader.data==null && event.text.length>0,"error-"+label);});
            loader.addEventListener(SecurityErrorEvent.SECURITY_ERROR,function(event:SecurityErrorEvent):void {security++;check(denied && statuses.length>0 && loader.data==null,"security-"+label);});
            loader.load(new URLRequest(baseUrl+path));
        }
        private function unexpected(event:Event):void {cancelledEvents++;}
        private function send(request:URLRequest, expected:String, binary:Boolean = false):void {
            var loader:URLLoader=new URLLoader();retained.push(loader);if(binary)loader.dataFormat="binary";
            loader.addEventListener(Event.COMPLETE,function(event:Event):void {
                sent++;
                if(binary){var data:ByteArray=loader.data as ByteArray;check(data!=null && data.length==256 && data.position==0,"post-binary-size");if(data!=null)for(var i:uint=0;i<data.length;i++)check(data.readUnsignedByte()==i,"post-binary-byte");}
                else check(loader.data===expected,"request-payload-"+sent+":"+loader.data);
            });
            loader.addEventListener(IOErrorEvent.IO_ERROR,function(event:IOErrorEvent):void {failures++;details+="request-io:"+event.text+",";});
            loader.addEventListener(SecurityErrorEvent.SECURITY_ERROR,unexpected);
            loader.load(request);
        }
        public function requests():void {
            check(new URLRequestHeader().name=="" && new URLRequestHeader().value=="" && URLRequestMethod.GET=="GET" && URLRequestMethod.POST=="POST","request-api-defaults");
            check(getDefinitionByName("flash.net.URLRequestHeader")===URLRequestHeader && getQualifiedClassName(URLRequestMethod)=="flash.net::URLRequestMethod" && getQualifiedClassName(new URLRequest(baseUrl))=="flash.net::URLRequest","request-reflection");
            var request:URLRequest=new URLRequest(baseUrl+"/inspect");request.method=URLRequestMethod.POST;request.contentType="text/plain; charset=utf-8";request.data="ž🙂\u0000body";
            request.requestHeaders=[new URLRequestHeader("X-Test","first"),new URLRequestHeader("Accept","application/json")];
            send(request,"POST\n/inspect\ntext/plain; charset=utf-8\nfirst\napplication/json\nž🙂\u0000body");
            request.url=baseUrl+"/missing";request.method="GET";request.data="changed";request.requestHeaders[0].value="changed";
            var form:URLVariables=new URLVariables();form.word=["ž🙂","a +"];
            request=new URLRequest(baseUrl+"/inspect");request.method="POST";request.data=form;
            send(request,"POST\n/inspect\napplication/x-www-form-urlencoded\n\n\nword=%C5%BE%F0%9F%99%82&word=a%20%2B");form.word[0]="changed";
            request=new URLRequest(baseUrl+"/inspect-query?base=1#fragment");request.data="x=one&y=a%2Bb";
            send(request,"GET\n/inspect-query?base=1&x=one&y=a%2Bb\n\n\n\n");check(request.url.indexOf("#fragment")>=0,"get-keeps-source-url");
            request=new URLRequest(baseUrl+"/inspect-query?#fragment");request.data=new URLVariables("word=%C5%BE%F0%9F%99%82");
            send(request,"GET\n/inspect-query?word=%C5%BE%F0%9F%99%82\n\n\n\n");
            var bytes:ByteArray=new ByteArray();for(var i:uint=0;i<256;i++)bytes.writeByte(i);bytes.position=7;
            request=new URLRequest(baseUrl+"/echo");request.method="POST";request.contentType="application/octet-stream";request.data=bytes;send(request,"",true);
            check(bytes.position==7,"post-preserves-byte-position");bytes.length=0;
            request=new URLRequest(baseUrl+"/inspect");request.method="POST";request.data="plain";request.requestHeaders=[new URLRequestHeader("content-type","text/plain"),new URLRequestHeader("X-Test","one"),new URLRequestHeader("x-test","two")];
            send(request,"POST\n/inspect\ntext/plain\none,two\n\nplain");
            request=new URLRequest(baseUrl+"/inspect");request.method="POST";send(request,"GET\n/inspect\n\n\n\n");
            for each(var code:uint in [301,302,303,307,308]) {
                request=new URLRequest(baseUrl+"/post-redirect/"+code);request.method="POST";request.contentType="text/plain";request.data="redirect-body";request.requestHeaders=[new URLRequestHeader("X-Test","same-origin")];
                send(request,code==307||code==308?"POST\n/inspect\ntext/plain\nsame-origin\n\nredirect-body":"GET\n/inspect\n\nsame-origin\n\n");
            }
            request=new URLRequest(baseUrl+"/cross-origin");request.method="POST";request.contentType="text/plain";request.data="cross-body";request.requestHeaders=[new URLRequestHeader("X-Test","private")];
            send(request,"POST\n/inspect\ntext/plain\n\n\ncross-body");
            request=new URLRequest(baseUrl+"/echo");request.method="POST";request.data=42;send(request,"42");
            var preserved:URLLoader=new URLLoader();retained.push(preserved);preserved.addEventListener(Event.COMPLETE,function(event:Event):void {sent++;check(preserved.data=="ž🙂\u0000HTTP","invalid-load-preserves-pending");});preserved.addEventListener(IOErrorEvent.IO_ERROR,unexpected);preserved.load(new URLRequest(baseUrl+"/text"));
            request=new URLRequest(baseUrl+"/missing");request.requestHeaders=[new URLRequestHeader("Host","invalid")];var caught:Boolean;try{preserved.load(request);}catch(invalid:ArgumentError){caught=true;}check(caught,"invalid-request-rejected-before-cancel");
            requestValidation();
        }
        private function requestValidation():void {
            var loader:URLLoader=new URLLoader();var request:URLRequest=new URLRequest(baseUrl+"/echo");request.data=new ByteArray();var caught:Boolean;
            try{loader.load(request);}catch(binaryGet:ArgumentError){caught=true;}check(caught,"binary-get-rejected");
            request.method="POST";request.data="body";
            for each(var name:String in ["Host","Content_Length","accept-encoding","Authorization","X-Bad\r\nInjected"]) {
                request.requestHeaders=[new URLRequestHeader(name,"value")];caught=false;try{loader.load(request);}catch(blocked:ArgumentError){caught=true;}check(caught,"restricted-header");
            }
            request.requestHeaders=[new URLRequestHeader("X-Test","bad\r\nInjected: yes")];caught=false;try{loader.load(request);}catch(injection:ArgumentError){caught=true;}check(caught,"header-injection");
            request.requestHeaders=[{}];caught=false;try{loader.load(request);}catch(wrongType:ArgumentError){caught=true;}check(caught,"header-type");
            request.requestHeaders=[new URLRequestHeader("X-Test",new Array(8194).join("a"))];caught=false;try{loader.load(request);}catch(headerSize:ArgumentError){caught=true;}check(caught,"header-size");
            request.requestHeaders=[];for(var i:uint=0;i<65;i++)request.requestHeaders.push(new URLRequestHeader("X-Test","value"));caught=false;try{loader.load(request);}catch(headerCount:ArgumentError){caught=true;}check(caught,"header-count");
            request.requestHeaders=[];request.contentType="text/plain\r\nInjected: yes";caught=false;try{loader.load(request);}catch(typeInjection:ArgumentError){caught=true;}check(caught,"content-type-injection");request.contentType="text/plain";
            request.requestHeaders=[];var large:ByteArray=new ByteArray();large.length=33554433;request.data=large;caught=false;try{loader.load(request);}catch(oversize:RangeError){caught=true;}check(caught,"upload-limit");large.length=0;
        }
        public function cancelProgress():void {
            active=new URLLoader();retained.push(active);active.addEventListener(Event.COMPLETE,unexpected);active.addEventListener(IOErrorEvent.IO_ERROR,unexpected);
            active.addEventListener(ProgressEvent.PROGRESS,function(event:ProgressEvent):void {if(event.bytesLoaded>0){active.close();cancelled++;}});
            active.load(new URLRequest(baseUrl+"/slowbody"));
        }
        public function cancelStatus():void {
            active=new URLLoader();retained.push(active);active.addEventListener(Event.COMPLETE,unexpected);active.addEventListener(IOErrorEvent.IO_ERROR,unexpected);
            active.addEventListener(HTTPStatusEvent.HTTP_STATUS,function(event:HTTPStatusEvent):void {active.close();cancelled++;});
            active.load(new URLRequest(baseUrl+"/redirect"));
        }
        public function replaceStatus():void {
            active=new URLLoader();retained.push(active);var switched:Boolean;
            active.addEventListener(HTTPStatusEvent.HTTP_STATUS,function(event:HTTPStatusEvent):void {if(!switched){switched=true;active.load(new URLRequest(baseUrl+"/text"));}});
            active.addEventListener(Event.COMPLETE,function(event:Event):void {replaced++;check(active.data=="ž🙂\u0000HTTP","status-replacement");});
            active.addEventListener(IOErrorEvent.IO_ERROR,unexpected);active.load(new URLRequest(baseUrl+"/redirect"));
        }
        public function pending():void {active=new URLLoader();retained.push(active);active.addEventListener(Event.COMPLETE,unexpected);active.addEventListener(IOErrorEvent.IO_ERROR,unexpected);active.load(new URLRequest(baseUrl+"/slowheaders"));}
        public function closePending():void {active.close();cancelled++;}
        public function pendingUpload():void {
            active=new URLLoader();retained.push(active);active.addEventListener(Event.COMPLETE,unexpected);active.addEventListener(IOErrorEvent.IO_ERROR,unexpected);
            var request:URLRequest=new URLRequest(baseUrl+"/slowupload");request.method="POST";request.contentType="application/octet-stream";var bytes:ByteArray=new ByteArray();bytes.length=16777216;request.data=bytes;active.load(request);
        }
        public function closeUpload():void {active.close();}
        public function tlsPost():void {var request:URLRequest=new URLRequest(baseUrl+"/inspect");request.method="POST";request.contentType="text/plain";request.data="ž🙂";request.requestHeaders=null;send(request,"POST\n/inspect\ntext/plain\n\n\nž🙂");}
        public function replacePending():void {active.addEventListener(Event.COMPLETE,function(event:Event):void {replaced++;});active.removeEventListener(Event.COMPLETE,unexpected);active.load(new URLRequest(baseUrl+"/text"));}
        public function tls():void {watch("tls","/text");}
        public function rejectTls():void {
            active=new URLLoader();retained.push(active);var status:Boolean;
            active.addEventListener(HTTPStatusEvent.HTTP_STATUS,function(event:HTTPStatusEvent):void {status=event.status==0;});
            active.addEventListener(SecurityErrorEvent.SECURITY_ERROR,function(event:Event):void {security++;check(status,"tls-status-zero");});
            active.addEventListener(Event.COMPLETE,unexpected);active.addEventListener(IOErrorEvent.IO_ERROR,unexpected);active.load(new URLRequest(baseUrl+"/text"));
        }
        public function refused():void {watch("refused","/text","text",-1);}
        public function childNetwork():void {
            child=new Loader();addChild(child);
            child.contentLoaderInfo.addEventListener(Event.COMPLETE,function(event:Event):void {child.content["networkUrl"]=baseUrl+"/slowheaders";});
            child.load(new URLRequest("url-data/sub/child.swf"));
        }
        public function stopChild():void {child.unloadAndStop(true);}
        public function release():void {active=null;retained=[];}
    }
}
