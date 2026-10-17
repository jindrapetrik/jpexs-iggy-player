package {
    import flash.display.Loader;
    import flash.display.MovieClip;
    import flash.events.Event;
    import flash.events.TimerEvent;
    import flash.net.URLRequest;
    import flash.system.ApplicationDomain;
    import flash.utils.Timer;
    public class BytesChild extends MovieClip {
        public static var instances:uint;
        public var ordinal:uint;
        public var domain:ApplicationDomain;
        public var constructorInfo:Boolean;
        public var nestedReady:Boolean;
        public var nested:Loader;
        public var tile:Object;
        public var ticks:uint;
        public var frames:uint;
        private var clock:Timer;
        public function BytesChild() {
            super(); stop(); ordinal = ++instances; domain = ApplicationDomain.currentDomain;
            constructorInfo = root === this && loaderInfo.applicationDomain === domain
                && loaderInfo.parameters.message == "bytes" && loaderInfo.loader != null;
            nested = new Loader(); nested.x = 50; addChild(nested);
            nested.contentLoaderInfo.addEventListener(Event.COMPLETE, nestedComplete);
            nested.load(new URLRequest("loader/colors.png"));
            clock = new Timer(10); clock.addEventListener(TimerEvent.TIMER, tick); clock.start();
            addEventListener(Event.ENTER_FRAME, enter);
        }
        private function nestedComplete(event:Event):void { nestedReady = true; }
        private function tick(event:TimerEvent):void { ticks++; }
        private function enter(event:Event):void { frames++; }
    }
}
