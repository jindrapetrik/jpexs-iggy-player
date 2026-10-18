package {
    import flash.display.MovieClip;
    import flash.display.Loader;
    import flash.events.Event;
    import flash.events.HTTPStatusEvent;
    import flash.events.IOErrorEvent;
    import flash.events.SecurityErrorEvent;
    import flash.net.URLRequest;
    import flash.system.LoaderContext;
    public class NetworkDependenciesMain extends MovieClip {
        public var baseUrl:String;
        public var completed:uint;
        public var errors:uint;
        public var security:uint;
        public var sent:uint;
        public var failures:uint;
        public var details:String="";
        public var cancelledEvents:uint;
        public var statuses:uint;
        public var active:Loader;
        private var expected:Boolean;
        public function NetworkDependenciesMain(){super();stop();}
        private function begin(path:String,error:Boolean=false,context:LoaderContext=null):void {
            if(active!=null){active.unloadAndStop(true);if(active.parent==this)removeChild(active);}
            active=new Loader();addChild(active);expected=error;
            active.contentLoaderInfo.addEventListener(HTTPStatusEvent.HTTP_STATUS,function(event:Event):void {statuses++;});
            active.contentLoaderInfo.addEventListener(Event.COMPLETE,function(event:Event):void {completed++;if(expected){failures++;details+="unexpected-complete;";}});
            active.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR,function(event:IOErrorEvent):void {errors++;details+=event.text+";";if(!expected)failures++;});
            active.contentLoaderInfo.addEventListener(SecurityErrorEvent.SECURITY_ERROR,function(event:SecurityErrorEvent):void {security++;details+=event.text+";";if(!expected)failures++;});
            active.load(new URLRequest(baseUrl+path),context);
        }
        public function start():void {begin("/entry.iggy?asset=1");}
        public function bytes():void {begin("/bundle/bytes-parent.iggy");}
        public function missing():void {begin("/missing-import/root.iggy",true);}
        public function textures():void {begin("/missing-texture/root.iggy",true);}
        public function corrupt():void {begin("/bad-texture/root.iggy",true);}
        public function cycle():void {begin("/cycle/root.iggy",true);}
        public function unsafe():void {begin("/unsafe/root.iggy",true);}
        public function denied():void {var context:LoaderContext=new LoaderContext();context.allowCodeImport=false;begin("/denied/root.iggy",true,context);}
        public function pendingImport():void {begin("/slow-import/root.iggy");}
        public function pendingTexture():void {begin("/slow-texture/root.iggy");}
        public function closePending():void {active.close();}
        public function tls():void {begin("/entry.iggy?asset=1",true);}
        public function get childDone():Boolean {return active!=null && active.content!=null && active.content["done"];}
        public function get childPassed():Boolean {return childDone && active.content["passed"];}
        public function unload():void {if(active!=null)active.unloadAndStop(true);active=null;}
    }
}
