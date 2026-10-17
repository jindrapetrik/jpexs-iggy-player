package com.jpexs.iggy.runtime {
    import flash.display.BitmapData;
    import flash.display.DisplayObject;
    import flash.display.Sprite;
    import flash.display.LoaderInfo;
    import flash.events.Event;
    import flash.events.TimerEvent;
    import flash.external.ExternalInterface;
    import flash.system.LoaderContext;
    import flash.utils.Timer;
    public class HostLoader extends Sprite {
        private var info:HostLoaderInfo;
        private var pending:Timer;
        private var token:String;
        private var generation:uint;
        private var opened:Boolean;
        private var loaded:DisplayObject;
        public function HostLoader() {
            super(); info = new HostLoaderInfo(this);
        }
        public function load(request:HostURLRequest, context:LoaderContext = null):void {
            if (request == null) throw new TypeError("Loader request is null");
            var serial:uint = generation + 1;
            unload();
            if (serial != generation) return;
            info.requestUrl = request.url;
            info.requestBytes = 0;
            info.imageWidth = 0;
            info.imageHeight = 0;
            info.mimeType = null;
            opened = false;
            token = String(ExternalInterface.call("__iggy_image_start", request.url));
            pending = new Timer(10);
            pending.addEventListener(TimerEvent.TIMER, poll);
            pending.start();
        }
        private function poll(event:TimerEvent):void {
            var serial:uint = generation;
            if (!opened) {
                opened = true;
                info.dispatchEvent(new Event(Event.OPEN));
                if (serial != generation) return;
            }
            var status:String = token.indexOf("error|") == 0 ? token : String(ExternalInterface.call("__iggy_image_poll", token));
            if (status == "pending") return;
            var parts:Array = status.split("|");
            if (parts[0] != "ready") {
                close();
                info.dispatchEvent(new HostIOErrorEvent("ioError", false, false, status));
                return;
            }
            info.imageWidth = int(parts[1]);
            info.imageHeight = int(parts[2]);
            info.requestBytes = uint(parts[3]);
            info.mimeType = String(parts[4]);
            info.requestUrl = parts.slice(5).join("|");
            info.dispatchEvent(new HostProgressEvent("progress", false, false, info.requestBytes, info.requestBytes));
            if (serial != generation) return;
            var bitmapClass:Class = BitmapData;
            var pixels:BitmapData = new bitmapClass(info.imageWidth, info.imageHeight, true, 0, token);
            var result:String = String(ExternalInterface.call("__iggy_image_finish", token));
            if (result != "ok") {
                close();
                info.dispatchEvent(new HostIOErrorEvent("ioError", false, false, result));
                return;
            }
            token = null;
            stopTimer();
            loaded = new HostLoadedBitmap(pixels, info);
            info.imageContent = loaded;
            addChild(loaded);
            info.dispatchEvent(new Event(Event.INIT));
            if (serial != generation) return;
            info.dispatchEvent(new Event(Event.COMPLETE));
        }
        private function stopTimer():void {
            if (pending != null) {
                pending.stop();
                pending.removeEventListener(TimerEvent.TIMER, poll);
                pending = null;
            }
        }
        public function close():void {
            generation++;
            stopTimer();
            if (token != null) ExternalInterface.call("__iggy_image_cancel", token);
            token = null;
        }
        public function unload():void {
            close();
            if (loaded != null) {
                if (loaded.parent == this) removeChild(loaded);
                loaded = null;
                info.imageContent = null;
                info.dispatchEvent(new Event(Event.UNLOAD));
            }
        }
        public function unloadAndStop(gc:Boolean = true):void { unload(); }
        public function get content():DisplayObject { return loaded; }
        public function get contentLoaderInfo():LoaderInfo { return info; }
    }
}
