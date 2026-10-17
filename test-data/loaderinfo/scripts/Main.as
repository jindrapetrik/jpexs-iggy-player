package {
    import flash.display.MovieClip;
    import flash.display.Sprite;
    import flash.events.Event;
    import flash.text.TextField;

    public class Main extends MovieClip {
        public var initialLoaded:uint;
        public var initialTotal:uint;
        public var initialSame:Boolean;
        public var childLoaderInfoNull:Boolean;
        public var tickTotal:uint;
        public var reads:uint;

        public function Main() {
            super();
            initialLoaded = stage.loaderInfo.bytesLoaded;
            initialTotal = stage.loaderInfo.bytesTotal;
            initialSame = stage.loaderInfo === loaderInfo;
            var child:Sprite = new Sprite();
            addChild(child);
            childLoaderInfoNull = child.loaderInfo === null;
            var text:TextField = new TextField();
            text.text = "LoaderInfo ready";
            text.x = 100;
            text.y = 50;
            text.width = 300;
            text.height = 40;
            addChild(text);
            trace("LOADERINFO_INIT " + initialLoaded + " " + initialTotal + " " + initialSame);
            addEventListener(Event.ENTER_FRAME, checkTick);
        }

        private function checkTick(event:Event):void {
            tickTotal = stage.loaderInfo.bytesTotal;
            reads++;
        }
    }
}
