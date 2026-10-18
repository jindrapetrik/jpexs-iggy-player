package {
    import flash.display.MovieClip;
    import flash.display.Loader;
    import flash.net.URLLoader;
    import flash.net.URLRequest;
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
