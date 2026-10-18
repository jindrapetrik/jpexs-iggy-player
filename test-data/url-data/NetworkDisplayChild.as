package {
    import flash.display.MovieClip;
    import flash.display.Loader;
    import flash.net.URLLoader;
    import flash.net.URLRequest;
    import flash.events.Event;
    import flash.events.TimerEvent;
    import flash.utils.Timer;
    public class NetworkDisplayChild extends MovieClip {
        public var image:Loader;
        public var text:URLLoader;
        public var done:Boolean;
        public var passed:Boolean;
        public var imageDone:Boolean;
        public var dataDone:Boolean;
        public var origin:String;
        private var later:Timer;
        public function NetworkDisplayChild() {
            super();stop();origin=loaderInfo.url;later=new Timer(20,1);later.addEventListener(TimerEvent.TIMER,loadRelative);later.start();
        }
        private function loadRelative(event:TimerEvent):void {
            image=new Loader();addChild(image);image.contentLoaderInfo.addEventListener(Event.COMPLETE,function(event:Event):void {imageDone=true;finish();});image.load(new URLRequest("pic.png"));
            text=new URLLoader();text.addEventListener(Event.COMPLETE,function(event:Event):void {dataDone=text.data=="remote-relative";finish();});text.load(new URLRequest("data.txt"));
        }
        private function finish():void {done=imageDone && dataDone;passed=done && origin.indexOf("/remote/child.iggy")>=0 && image.contentLoaderInfo.url.indexOf("/remote/pic.png")>=0 && image.contentLoaderInfo.loaderURL.indexOf("/remote/child.iggy")>=0;}
    }
}
