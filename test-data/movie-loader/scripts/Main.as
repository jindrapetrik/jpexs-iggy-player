package {
    import flash.display.Loader;
    import flash.display.MovieClip;
    import flash.events.Event;
    import flash.events.IOErrorEvent;
    import flash.events.MouseEvent;
    import flash.events.TimerEvent;
    import flash.net.URLRequest;
    import flash.utils.Timer;
    import flash.media.Sound;
    import flash.media.SoundChannel;
    import flash.system.ApplicationDomain;
    import flash.system.LoaderContext;
    public class Main extends MovieClip {
        public static var marker:uint = 99;
        public var first:Loader;
        public var second:Loader;
        public var ready:uint;
        public var errors:uint;
        public var cancelled:uint;
        public var failures:uint;
        public var returned:Boolean;
        public var order:String = "";
        public var saved:MovieClip;
        public var plain:Loader;
        public var plainReady:Boolean;
        public var parentTicks:uint;
        public var parentTimerTicks:uint;
        private var parentTimer:Timer;
        public var parentTone:Sound;
        public var parentChannel:SoundChannel;
        public function Main() {
            super(); stop();
            addEventListener(Event.ENTER_FRAME, parentEnter);
            parentTimer = new Timer(10); parentTimer.addEventListener(TimerEvent.TIMER, parentTimerEnter); parentTimer.start();
            first = new Loader(); first.x = 10; first.y = 50; addChild(first);
            first.contentLoaderInfo.addEventListener(Event.OPEN, opened);
            first.contentLoaderInfo.addEventListener(Event.INIT, init);
            first.contentLoaderInfo.addEventListener(Event.COMPLETE, complete);
            first.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR, unexpected);
            first.load(new URLRequest("movie-loader/child.swf"), new LoaderContext(false, new ApplicationDomain(null)));
            second = new Loader(); second.x = 260; second.y = 50; addChild(second);
            second.contentLoaderInfo.addEventListener(Event.COMPLETE, other);
            second.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR, unexpected);
            second.load(new URLRequest("movie-loader/child.iggy"), new LoaderContext(false, new ApplicationDomain(null)));
            var cancel:Loader = new Loader();
            cancel.contentLoaderInfo.addEventListener(Event.COMPLETE, cancelledEvent);
            cancel.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR, cancelledEvent);
            cancel.load(new URLRequest("movie-loader/child.iggy"), new LoaderContext(false, new ApplicationDomain(null))); cancel.close();
            var missing:Loader = new Loader();
            missing.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR, failed);
            missing.load(new URLRequest("movie-loader/missing.iggy"));
            var x86:Loader = new Loader(); x86.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR, failed);
            x86.load(new URLRequest("movie-loader/unsupported32.iggy"));
            var invalid:Loader = new Loader(); invalid.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR, failed);
            invalid.load(new URLRequest("movie-loader/invalid.iggy"));
            plain = new Loader(); plain.x = 10; plain.y = 220; addChild(plain);
            plain.contentLoaderInfo.addEventListener(Event.COMPLETE, plainComplete);
            plain.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR, unexpected);
            plain.load(new URLRequest("movie-loader/plain.iggy"));
            returned = true;
        }
        private function check(value:Boolean):void { if (!value) { failures++; trace("MOVIE LOADER CHECK FAILED"); } }
        private function opened(e:Event):void { check(returned); order += "open,"; }
        private function init(e:Event):void {
            order += "init,";
            check(first.content is MovieClip);
            check(first.content.parent === first && first.content.stage === stage);
            check(first.content.root === first.content);
            check(first.content.loaderInfo === first.contentLoaderInfo);
            check(first.contentLoaderInfo.loader === first);
            check(first.contentLoaderInfo.width == 200 && first.contentLoaderInfo.height == 120);
            check(first.contentLoaderInfo.bytesLoaded > 0 && first.contentLoaderInfo.bytesLoaded == first.contentLoaderInfo.bytesTotal);
            check(first.contentLoaderInfo.content === first.content);
            check(first.content["kind"] == "child" && first.content["ownClass"]);
            check(first.content["constructorInfo"]);
            check(MovieClip(first.content).getChildByName("importedSquare")["importedMark"] == 23);
            check(Main.marker == 99);
        }
        private function complete(e:Event):void { order += "complete,"; ready++; trace("MOVIE READY " + order); }
        private function other(e:Event):void { ready++; check(second.content["kind"] == "child" && second.content["ownClass"]); }
        private function failed(e:IOErrorEvent):void { errors++; }
        private function unexpected(e:IOErrorEvent):void { failures++; trace("MOVIE UNEXPECTED " + e.text); }
        private function cancelledEvent(e:Event):void { cancelled++; }
        private function plainComplete(e:Event):void { plainReady = plain.content is MovieClip && MovieClip(plain.content).totalFrames == 3; }
        public function unloadMovies():void { saved = MovieClip(first.content); first.unload(); second.unload(); plain.unload(); }
        public function releaseSaved():void { saved = null; }
        public function reuse():void { first.load(new URLRequest("movie-loader/child.iggy"), new LoaderContext(false, new ApplicationDomain(null))); }
        public function stopFirst(gc:Boolean):void { saved = MovieClip(first.content); first.unloadAndStop(gc); }
        public function mouseEvent():void { stage.dispatchEvent(new MouseEvent(MouseEvent.MOUSE_DOWN)); }
        public function stopSecond():void { second.unloadAndStop(); }
        private function parentEnter(e:Event):void { parentTicks++; }
        private function parentTimerEnter(e:TimerEvent):void { parentTimerTicks++; }
        public function startParentSound():void { parentTone = new UnloadTone(); parentChannel = parentTone.play(0, 1000); }
        public function armReentrantStop():void { saved = MovieClip(first.content); saved["stopOnTick"] = true; }
        public function get hasCapture():Boolean { return stage.hasEventListener(MouseEvent.MOUSE_UP); }
    }
}
