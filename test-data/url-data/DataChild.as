package {
    import flash.display.MovieClip;
    import flash.net.URLLoader;
    import flash.net.URLRequest;
    import flash.events.Event;
    import flash.events.TimerEvent;
    import flash.utils.Timer;
    public class DataChild extends MovieClip {
        public var done:Boolean;
        public var passed:Boolean;
        public var networkUrl:String;
        private var timer:Timer;
        private var data:URLLoader;
        public function DataChild() {super();stop();timer=new Timer(10,1);timer.addEventListener(TimerEvent.TIMER,later);timer.start();}
        private function later(event:TimerEvent):void {
            data=new URLLoader();data.addEventListener(Event.COMPLETE,function(event:Event):void {passed=data.data=="child-relative";done=true;});data.load(new URLRequest(networkUrl==null?"local.txt":networkUrl));
        }
        public function startPending():void {data.load(new URLRequest("../../url-data/bytes.bin"));}
    }
}
