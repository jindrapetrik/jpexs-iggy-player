package {
    import flash.display.MovieClip;
    import flash.net.URLLoader;
    import flash.net.URLRequest;
    import flash.events.Event;
    import flash.events.TimerEvent;
    import flash.utils.Timer;
    public class RemoteChild extends MovieClip {
        public var tile:RemoteTile;
        public var origin:String;
        public var done:Boolean;
        public var passed:Boolean;
        private var timer:Timer;
        private var data:URLLoader;
        public function RemoteChild(){super();stop();origin=loaderInfo.url;timer=new Timer(20,1);timer.addEventListener(TimerEvent.TIMER,fetch);timer.start();}
        private function fetch(event:Event):void {
            data=new URLLoader();data.addEventListener(Event.COMPLETE,function(event:Event):void {done=true;passed=tile!=null && tile.marker==37 && data.data=="bundle-relative" && origin.indexOf("/bundle/")>=0;});
            data.load(new URLRequest("data.txt"));
        }
    }
}
