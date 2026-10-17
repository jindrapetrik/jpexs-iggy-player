package {
    import flash.display.Bitmap;
    import flash.display.Loader;
    import flash.display.LoaderInfo;
    import flash.display.MovieClip;
    import flash.events.Event;
    import flash.events.IOErrorEvent;
    import flash.events.ProgressEvent;
    import flash.system.ApplicationDomain;
    import flash.system.LoaderContext;
    import flash.utils.ByteArray;
    public class BytesMain extends MovieClip {
        public var loaders:Array = [];
        public var movies:Array = [];
        public var shared:ApplicationDomain;
        public var completed:uint;
        public var errors:uint;
        public var failures:uint;
        public var failureList:String = "";
        public var cancelledEvents:uint;
        public var initCancelled:uint;
        public var complete:Boolean;
        public var inputOk:Boolean;
        public var controls:Boolean;
        public var stopped:Boolean;
        public var order:String = "";
        private var checks:uint;
        private var retained:Object;
        private var stoppedTicks:uint;
        private var stoppedFrames:uint;
        public function BytesMain() {
            super(); stop(); byteOperations();
            var png:ByteArray = new ByteArray(); png.writeBytes(new PngBytes()); png.position = 7;
            var first:Loader = start(png, null); first.x = 10; first.y = 10;
            inputOk = png.position == 7; png.length = 0;
            var jpeg:ByteArray = new JpegBytes(); jpeg.position = jpeg.length;
            var photo:Loader = start(jpeg, null); photo.x = 60; photo.y = 10;
            inputOk = inputOk && jpeg.position == jpeg.length;
            shared = new ApplicationDomain(ApplicationDomain.currentDomain);
            for (var i:int = 0; i < 4; i++) {
                var domain:ApplicationDomain = i == 0 ? null : i == 3 ? new ApplicationDomain(null) : shared;
                var movie:Loader = start(new MovieBytes(), new LoaderContext(false, domain));
                movie.x = 120 + 90 * i; movie.y = 50; movies.push(movie);
            }
            reject(new BadBytes(), null); reject(new SwfBytes(), null); reject(new LegacyBytes(), null);
            var context:LoaderContext = new LoaderContext(); context.allowCodeImport = false; reject(new MovieBytes(), context);
            context = new LoaderContext(); context.allowLoadBytesCodeExecution = false; reject(new MovieBytes(), context);
            var cancel:Loader = new Loader(); cancel.contentLoaderInfo.addEventListener(Event.OPEN, cancelled);
            cancel.contentLoaderInfo.addEventListener(Event.COMPLETE, cancelled); cancel.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR, cancelled);
            cancel.loadBytes(new PngBytes()); cancel.close();
            var init:Loader = new Loader(); init.contentLoaderInfo.addEventListener(Event.INIT, cancelInit);
            init.contentLoaderInfo.addEventListener(Event.COMPLETE, cancelled); init.loadBytes(new PngBytes());
            try { new Loader().loadBytes(null); } catch (nullError:TypeError) { controls = true; }
            var empty:Boolean; try { new Loader().loadBytes(new ByteArray()); } catch (emptyError:ArgumentError) { empty = true; }
            controls = controls && empty;
            addEventListener(Event.ENTER_FRAME, inspect);
        }
        private function check(value:Boolean):void { checks++; if (!value) { failures++; failureList += checks + ","; } }
        private function byteOperations():void {
            var bytes:ByteArray = new ByteArray(); bytes.writeByte(-1); bytes.writeByte(128); bytes.writeByte(7);
            check(bytes.length == 3 && bytes.position == 3 && bytes.bytesAvailable == 0);
            bytes.position = 0; check(bytes.readByte() == -1 && bytes.readUnsignedByte() == 128 && bytes.bytesAvailable == 1);
            bytes.position = 5; bytes.writeByte(42); check(bytes.length == 6 && bytes.position == 6);
            bytes.position = 3; check(bytes.readUnsignedByte() == 0 && bytes.readUnsignedByte() == 0);
            bytes.length = 2; check(bytes.position == 2); bytes.length = 5; bytes.position = 2; check(bytes.readUnsignedByte() == 0);
            var copy:ByteArray = new ByteArray(); copy.position = 1; bytes.position = 0; bytes.readBytes(copy, 2, 2);
            check(bytes.position == 2 && copy.position == 1 && copy.length == 4); copy.position = 2; check(copy.readUnsignedByte() == 255);
            var embedded:ByteArray = new PngBytes(); embedded.writeByte(0); var fresh:ByteArray = new PngBytes();
            check(fresh.readUnsignedByte() == 137); embedded.position = 0; check(embedded.readUnsignedByte() == 0);
            bytes.position = bytes.length;
            var bounds:Boolean; try { bytes.readUnsignedByte(); } catch (range:Error) { bounds = true; }
            check(bounds && bytes.position == bytes.length);
            bounds = false; try { bytes.length = 0xffffffff; } catch (large:RangeError) { bounds = true; }
            check(bounds && bytes.length == 5);
            copy.position = 0; copy.writeBytes(copy, 2, 2); copy.position = 0;
            check(copy.readUnsignedByte() == 255 && copy.readUnsignedByte() == 128);
        }
        private function start(bytes:ByteArray, context:LoaderContext):Loader {
            var loader:Loader = new Loader(); loaders.push(loader); addChild(loader);
            if (context == null) context = new LoaderContext(); context.parameters = {message:"bytes"};
            loader.contentLoaderInfo.addEventListener(Event.OPEN, opened);
            loader.contentLoaderInfo.addEventListener(ProgressEvent.PROGRESS, progress);
            loader.contentLoaderInfo.addEventListener(Event.INIT, initialized);
            loader.contentLoaderInfo.addEventListener(Event.COMPLETE, loaded);
            loader.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR, failed);
            loader.loadBytes(bytes, context); check(loader.content == null); return loader;
        }
        private function opened(event:Event):void { if (event.target === loaders[0].contentLoaderInfo) order += "open,"; }
        private function progress(event:ProgressEvent):void {
            check(event.bytesLoaded > 0 && event.bytesLoaded == event.bytesTotal);
            if (event.target === loaders[0].contentLoaderInfo) order += "progress,";
        }
        private function initialized(event:Event):void { if (event.target === loaders[0].contentLoaderInfo) order += "init,"; }
        private function loaded(event:Event):void {
            completed++; var info:LoaderInfo = LoaderInfo(event.target);
            check(info.content != null && info.bytesLoaded > 0 && info.bytesLoaded == info.bytesTotal);
            check(info.content.loaderInfo === info && info.loader != null);
            if (event.target === loaders[0].contentLoaderInfo) order += "complete,";
        }
        private function failed(event:IOErrorEvent):void { failures++; failureList += "unexpected:" + event.text; }
        private function reject(bytes:ByteArray, context:LoaderContext):void {
            var loader:Loader = new Loader(); loader.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR, rejected); loader.loadBytes(bytes, context);
        }
        private function rejected(event:IOErrorEvent):void { errors++; }
        private function cancelled(event:Event):void { cancelledEvents++; }
        private function cancelInit(event:Event):void { initCancelled++; LoaderInfo(event.target).loader.unloadAndStop(); }
        private function inspect(event:Event):void {
            if (complete || completed != 6 || errors != 5 || initCancelled != 1) return;
            for each (var loader:Loader in movies) if (!loader.content["nestedReady"]) return;
            complete = true; check(inputOk && controls);
            for each (loader in movies) check(loader.content["constructorInfo"] && loader.content["tile"] != null);
            check(movies[0].content["ordinal"] == 1 && movies[3].content["ordinal"] == 1);
            check(movies[1].content["ordinal"] + movies[2].content["ordinal"] == 3);
            check(movies[1].contentLoaderInfo.applicationDomain === shared && movies[2].contentLoaderInfo.applicationDomain === shared);
            check(movies[0].contentLoaderInfo.applicationDomain !== shared);
            check(shared.getDefinition("BytesChild") !== movies[3].contentLoaderInfo.applicationDomain.getDefinition("BytesChild"));
            check(!ApplicationDomain.currentDomain.hasDefinition("BytesChild"));
        }
        public function stopMovies():void {
            retained = movies[0].content;
            for each (var loader:Loader in loaders) loader.unloadAndStop();
            stoppedTicks = retained["ticks"]; stoppedFrames = retained["frames"];
        }
        public function checkStopped():void { stopped = retained["ticks"] == stoppedTicks && retained["frames"] == stoppedFrames; }
    }
}
