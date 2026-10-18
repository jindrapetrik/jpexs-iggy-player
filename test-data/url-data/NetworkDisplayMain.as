package {
    import flash.display.MovieClip;
    import flash.display.Loader;
    import flash.display.Bitmap;
    import flash.net.URLRequest;
    import flash.net.URLRequestHeader;
    import flash.system.LoaderContext;
    import flash.events.Event;
    import flash.events.HTTPStatusEvent;
    import flash.events.ProgressEvent;
    import flash.events.IOErrorEvent;
    import flash.events.SecurityErrorEvent;
    public class NetworkDisplayMain extends MovieClip {
        public var baseUrl:String;
        public var completed:uint;
        public var errors:uint;
        public var security:uint;
        public var sent:uint;
        public var failures:uint;
        public var details:String="";
        public var cancelledEvents:uint;
        public var cancelled:uint;
        public var movie:Loader;
        public var active:Loader;
        private var retained:Array=[];
        public function NetworkDisplayMain(){super();stop();}
        private function check(value:Boolean,label:String):void {if(!value){failures++;details+=label+",";}}
        public function start():void {
            watch("png","/remote/pic.png?query=1",10,10);
            watch("jpeg","/remote/photo.jpg",60,10);
            watch("post","/remote/post.png",110,10);
            movie=watch("movie","/display-redirect",160,10);
            watch("missing","/missing",0,0,true);
            watch("raw","/remote/raw.swf",0,0,true);
            watch("bad","/remote/bad.iggy",0,0,true);
            watch("imports","/remote/imports.iggy",0,0,true);
            var denied:LoaderContext=new LoaderContext();denied.allowCodeImport=false;
            watch("denied","/remote/child.iggy",0,0,true,denied);
            check(completed==0 && errors==0,"async-return");
        }
        private function watch(label:String,path:String,xpos:int,ypos:int,error:Boolean=false,context:LoaderContext=null):Loader {
            var loader:Loader=new Loader();retained.push(loader);loader.x=xpos;loader.y=ypos;addChild(loader);
            var opened:Boolean;var progress:Boolean;var status:Boolean;var initialized:Boolean;
            loader.contentLoaderInfo.addEventListener(Event.OPEN,function(event:Event):void {opened=true;check(event.target===loader.contentLoaderInfo,"open-target");});
            loader.contentLoaderInfo.addEventListener(HTTPStatusEvent.HTTP_STATUS,function(event:HTTPStatusEvent):void {status=true;check(opened && event.clone() is HTTPStatusEvent,"status-"+label);});
            loader.contentLoaderInfo.addEventListener(ProgressEvent.PROGRESS,function(event:ProgressEvent):void {progress=true;check(opened && (event.bytesTotal==0 || event.bytesLoaded<=event.bytesTotal),"progress-"+label);});
            loader.contentLoaderInfo.addEventListener(Event.INIT,function(event:Event):void {initialized=true;check(progress && loader.content!=null,"init-"+label);});
            loader.contentLoaderInfo.addEventListener(Event.COMPLETE,function(event:Event):void {
                completed++;check(!error && opened && status && progress && initialized && loader.content!=null && loader.content.parent===loader,"complete-"+label);
                check(loader.contentLoaderInfo.bytesLoaded>0 && loader.contentLoaderInfo.bytesLoaded==loader.contentLoaderInfo.bytesTotal,"length-"+label);
                if(label=="movie")check(loader.contentLoaderInfo.url==baseUrl+"/remote/child.iggy" && loader.content["origin"]==loader.contentLoaderInfo.url,"redirect-url");
                else {var bitmap:Bitmap=loader.content as Bitmap;check(bitmap!=null && bitmap.loaderInfo===loader.contentLoaderInfo,"bitmap-info-"+label);}
            });
            loader.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR,function(event:IOErrorEvent):void {errors++;check(error && loader.content==null && event.text.length>0,"error-"+label+":"+event.text);});
            loader.contentLoaderInfo.addEventListener(SecurityErrorEvent.SECURITY_ERROR,function(event:Event):void {security++;});
            var request:URLRequest=new URLRequest(baseUrl+path);
            if(label=="post"){request.method="POST";request.data="image=1";request.requestHeaders=[new URLRequestHeader("X-Test","display")];}
            loader.load(request,context);return loader;
        }
        public function get childDone():Boolean {return movie!=null && movie.content!=null && movie.content["done"];}
        public function get childPassed():Boolean {return childDone && movie.content["passed"];}
        private function unexpected(event:Event):void {cancelledEvents++;}
        public function pending():void {active=new Loader();retained.push(active);addChild(active);active.contentLoaderInfo.addEventListener(Event.COMPLETE,unexpected);active.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR,unexpected);active.load(new URLRequest(baseUrl+"/slowheaders"));}
        public function closePending():void {active.close();cancelled++;}
        public function cancelStatus():void {
            active=new Loader();retained.push(active);addChild(active);active.contentLoaderInfo.addEventListener(Event.COMPLETE,unexpected);active.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR,unexpected);
            active.contentLoaderInfo.addEventListener(HTTPStatusEvent.HTTP_STATUS,function(event:Event):void {active.close();cancelled++;});active.load(new URLRequest(baseUrl+"/remote/pic.png"));
        }
        public function cancelInit():void {
            active=new Loader();retained.push(active);addChild(active);active.contentLoaderInfo.addEventListener(Event.COMPLETE,unexpected);active.contentLoaderInfo.addEventListener(Event.INIT,function(event:Event):void {active.unloadAndStop(true);cancelled++;});active.load(new URLRequest(baseUrl+"/remote/pic.png"));
        }
        public function tls():void {watch("png","/remote/pic.png",10,10);}
        public function unload():void {for each(var loader:Loader in retained)loader.unloadAndStop(true);retained=[];active=null;movie=null;}
    }
}
