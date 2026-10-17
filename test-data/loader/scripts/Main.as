package {
    import flash.display.Bitmap;
    import flash.display.Loader;
    import flash.display.MovieClip;
    import flash.events.Event;
    import flash.events.IOErrorEvent;
    import flash.events.ProgressEvent;
    import flash.net.URLRequest;
    public class Main extends MovieClip {
        public var png:Loader;
        public var jpeg:Loader;
        public var unicode:Loader;
        public var failures:uint;
        public var completed:uint;
        public var errors:uint;
        public var cancelledEvents:uint;
        public var order:String = "";
        public var returned:Boolean;
        public var unloads:uint;
        public var initCancelled:uint;
        private var initCancel:Loader;
        private var openCancel:Loader;
        private var retained:Bitmap;
        public function Main() {
            super(); stop();
            png = new Loader(); png.x = 10; png.y = 10; addChild(png);
            png.contentLoaderInfo.addEventListener(Event.OPEN, opened);
            png.contentLoaderInfo.addEventListener(ProgressEvent.PROGRESS, progress);
            png.contentLoaderInfo.addEventListener(Event.INIT, init);
            png.contentLoaderInfo.addEventListener(Event.COMPLETE, complete);
            png.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR, unexpected);
            png.contentLoaderInfo.addEventListener(Event.UNLOAD, unloaded);
            png.load(new URLRequest("loader/replaced.png"));
            png.load(new URLRequest("loader/colors.png"));
            jpeg = new Loader(); jpeg.x = 60; jpeg.y = 10; addChild(jpeg);
            jpeg.contentLoaderInfo.addEventListener(Event.COMPLETE, other);
            jpeg.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR, unexpected);
            jpeg.load(new URLRequest("loader/photo.jpg"));
            unicode = new Loader(); unicode.x = 110; unicode.y = 10; addChild(unicode);
            unicode.contentLoaderInfo.addEventListener(Event.COMPLETE, other);
            unicode.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR, unexpected);
            unicode.load(new URLRequest("loader/%C5%BElu%C5%A5ou%C4%8Dk%C3%BD%20image.png?test=1"));
            var missing:Loader = new Loader();
            missing.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR, expected);
            missing.load(new URLRequest("loader/missing.png"));
            var invalid:Loader = new Loader();
            invalid.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR, expected);
            invalid.load(new URLRequest("loader/invalid.png"));
            var movie:Loader = new Loader();
            movie.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR, expected);
            movie.load(new URLRequest("README.md"));
            var cancelled:Loader = new Loader();
            cancelled.contentLoaderInfo.addEventListener(Event.COMPLETE, cancelEvent);
            cancelled.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR, cancelEvent);
            cancelled.load(new URLRequest("loader/colors.png")); cancelled.close();
            openCancel = new Loader();
            openCancel.contentLoaderInfo.addEventListener(Event.OPEN, closeAtOpen);
            openCancel.contentLoaderInfo.addEventListener(Event.COMPLETE, cancelEvent);
            openCancel.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR, cancelEvent);
            openCancel.load(new URLRequest("loader/colors.png"));
            initCancel = new Loader();
            initCancel.contentLoaderInfo.addEventListener(Event.INIT, unloadAtInit);
            initCancel.contentLoaderInfo.addEventListener(Event.COMPLETE, cancelEvent);
            initCancel.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR, unexpected);
            initCancel.load(new URLRequest("loader/colors.png"));
            returned = true;
        }
        private function check(value:Boolean):void { if (!value) { failures++; trace("LOADER CHECK FAILED"); } }
        private function opened(e:Event):void { check(returned); order += "open,"; }
        private function progress(e:ProgressEvent):void { check(e.bytesLoaded > 0 && e.bytesLoaded == e.bytesTotal); order += "progress,"; }
        private function init(e:Event):void {
            order += "init,";
            check(png.content is Bitmap);
            check(png.content.parent === png);
            check(png.contentLoaderInfo.content === png.content);
            check(png.contentLoaderInfo.loader === png);
            check(png.content.loaderInfo === png.contentLoaderInfo);
            check(png.content.root === png.content);
            check(png.contentLoaderInfo.width == 32 && png.contentLoaderInfo.height == 24);
            check(png.contentLoaderInfo.contentType == "image/png");
            check(png.contentLoaderInfo.bytesLoaded > 0 && png.contentLoaderInfo.bytesTotal == png.contentLoaderInfo.bytesLoaded);
        }
        private function complete(e:Event):void { order += "complete,"; completed++; trace("LOADER PNG COMPLETE " + order); }
        private function other(e:Event):void { completed++; trace("LOADER OTHER COMPLETE"); }
        private function unexpected(e:IOErrorEvent):void { failures++; trace("LOADER UNEXPECTED " + e.text); }
        private function expected(e:IOErrorEvent):void { check(returned && e.text.length > 0); errors++; }
        private function cancelEvent(e:Event):void { cancelledEvents++; }
        private function unloaded(e:Event):void { unloads++; }
        private function closeAtOpen(e:Event):void { openCancel.close(); }
        private function unloadAtInit(e:Event):void { initCancelled++; initCancel.unload(); }
        public function cloneImage():void {
            retained = new Bitmap(Bitmap(png.content).bitmapData.clone()); retained.x = 160; retained.y = 10; addChild(retained);
        }
        public function releaseClone():void { removeChild(retained); retained = null; }
        public function unloadImages():void { png.unload(); jpeg.unload(); unicode.unload(); }
        public function reuse():void { png.load(new URLRequest("loader/colors.png")); }
    }
}
