package {
    import flash.display.Loader;
    import flash.display.MovieClip;
    import flash.events.Event;
    import flash.net.URLRequest;
    import flash.text.TextField;
    public class Main extends MovieClip {
        public static var marker:uint = 17;
        public var kind:String = "child";
        public var ownClass:Boolean;
        public var constructorInfo:Boolean;
        public var ticks:uint;
        public var nestedReady:Boolean;
        public var nested:Loader;
        private var text:TextField;
        public function Main() {
            super();
            ownClass = Main.marker == 17;
            constructorInfo = loaderInfo != null && root === this;
            text = new TextField(); text.text = "Loaded child"; text.x = 5; text.y = 90; addChild(text);
            nested = new Loader(); nested.x = 20; nested.y = 40; addChild(nested);
            nested.contentLoaderInfo.addEventListener(Event.COMPLETE, nestedComplete);
            nested.load(new URLRequest("../loader/colors.png"));
            addEventListener(Event.ENTER_FRAME, enter);
        }
        private function nestedComplete(e:Event):void { nestedReady = true; }
        private function enter(e:Event):void { ticks++; }
    }
}
