package {
    import flash.display.MovieClip;
    import flash.net.URLLoader;
    import flash.net.URLRequest;
    import flash.events.Event;
    public class DataOnly extends MovieClip {
        public var passed:Boolean;
        private var loader:URLLoader;
        public function DataOnly() {
            super();stop();loader=new URLLoader(new URLRequest("empty"));
            loader.addEventListener(Event.COMPLETE,function(event:Event):void {passed=loader.data=="" && loader.bytesLoaded==0 && loader.bytesTotal==0;});
        }
    }
}
