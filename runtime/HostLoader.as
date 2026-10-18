package com.jpexs.iggy.runtime {
    import flash.display.BitmapData;
    import flash.display.DisplayObject;
    import flash.display.DisplayObjectContainer;
    import flash.display.Sprite;
    import flash.display.MovieClip;
    import flash.display.LoaderInfo;
    import flash.events.Event;
    import flash.events.TimerEvent;
    import flash.external.ExternalInterface;
    import flash.system.LoaderContext;
    import flash.system.ApplicationDomain;
    import flash.utils.Timer;
    import flash.utils.ByteArray;
    public class HostLoader extends Sprite {
        private var info:HostLoaderInfo;
        private var pending:Timer;
        private var token:String;
        private var generation:uint;
        private var opened:Boolean;
        private var loaded:DisplayObject;
        private var allowCode:Boolean;
        private var progress:Number;
        public function HostLoader() {
            super(); info = new HostLoaderInfo(this);
        }
        public function load(request:HostURLRequest, context:LoaderContext = null):void {
            if (request == null) throw new TypeError("Loader request is null");
            if (request.url == null || request.url.length == 0) throw new ArgumentError("Loader URL is empty");
            begin(request.url, null, context, request.packet());
        }
        public function loadBytes(bytes:ByteArray, context:LoaderContext = null):void {
            if (bytes == null) throw new TypeError("Loader bytes are null");
            if (bytes.length == 0) throw new ArgumentError("Loader bytes are empty");
            begin(null, bytes, context);
        }
        private function begin(url:String, bytes:ByteArray, context:LoaderContext, packet:ByteArray = null):void {
            if (context != null && context.securityDomain != null) throw new SecurityError("SecurityDomain is unavailable for local loading");
            if (context != null && context.requestedContentParent != null) throw new ArgumentError("requestedContentParent is unavailable for local loading");
            var serial:uint = generation + 1;
            unload();
            if (serial != generation) return;
            info.requestUrl = url;
            info.requestBytes = 0;
            info.requestTotal = 0; progress = -1;
            info.imageWidth = 0;
            info.imageHeight = 0;
            info.mimeType = null;
            info.requestDomain = context != null && context.applicationDomain != null ? context.applicationDomain : new ApplicationDomain(ApplicationDomain.currentDomain);
            info.requestParameters = context == null || context.parameters == null ? {} : context.parameters;
            allowCode = context == null || context.allowCodeImport;
            opened = false;
            if (bytes == null) {
                packet.position = packet.length; packet.writeUnsignedInt(info.requestDomain.__iggyId); packet.writeBoolean(allowCode);
                token = String(new HostAmfRequest("data-image-start", packet, info.loaderURL).result);
            }
            else token = new HostBytesRequest(bytes, info.loaderURL, info.requestDomain.__iggyId, allowCode && (context == null || context.allowLoadBytesCodeExecution)).token;
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
                ExternalInterface.call("__iggy_image_opened", token);
            }
            var invalid:Boolean = token.indexOf("error|") == 0 || token.indexOf("security|") == 0;
            if (!invalid) {
                var http:String = String(ExternalInterface.call("__iggy_image_transfer", token));
                while (http.length != 0) {
                    var fields:Array = http.split("|");
                    info.dispatchEvent(new HostHTTPStatusEvent("httpStatus", false, false, int(fields[0]), fields[1] == "1"));
                    if (serial != generation) return;
                    http = String(ExternalInterface.call("__iggy_image_transfer", token));
                }
                var counts:String = String(ExternalInterface.call("__iggy_image_progress", token));
                if (counts.length != 0) {
                    var values:Array = counts.split("|"); info.requestBytes = uint(values[0]); info.requestTotal = uint(values[1]);
                    if (Number(info.requestBytes) != progress) {
                        progress = info.requestBytes; info.dispatchEvent(new HostProgressEvent("progress", false, false, info.requestBytes, info.requestTotal));
                        if (serial != generation) return;
                    }
                }
            }
            var status:String = invalid ? token : String(ExternalInterface.call("__iggy_image_poll", token));
            if (status == "pending") return;
            var parts:Array = status.split("|");
            if (parts[0] != "ready" && parts[0] != "movie") {
                close();
                if (status.indexOf("security|") == 0) info.dispatchEvent(new HostSecurityErrorEvent("securityError", false, false, status.substr(9)));
                else info.dispatchEvent(new HostIOErrorEvent("ioError", false, false, status));
                return;
            }
            info.imageWidth = int(parts[1]);
            info.imageHeight = int(parts[2]);
            info.requestBytes = uint(parts[3]);
            info.requestTotal = info.requestBytes;
            info.mimeType = String(parts[4]);
            info.requestUrl = parts.slice(5).join("|");
            if (Number(info.requestBytes) != progress) {
                progress = info.requestBytes; info.dispatchEvent(new HostProgressEvent("progress", false, false, info.requestBytes, info.requestBytes));
                if (serial != generation) return;
            }
            var candidate:DisplayObject;
            try {
                if (parts[0] == "movie") {
                    var movieRequest:HostMovieRequest = new HostMovieRequest(token, info);
                    candidate = movieRequest.content;
                    if (candidate != null) movieRequest.retain(candidate);
                } else {
                    var bitmapClass:Class = BitmapData;
                    var pixels:BitmapData = new bitmapClass(info.imageWidth, info.imageHeight, true, 0, token);
                    candidate = new HostLoadedBitmap(pixels, info);
                }
            } catch (error:Error) {
                if (serial != generation) return;
                close();
                info.dispatchEvent(new HostIOErrorEvent("ioError", false, false, "Cannot instantiate loaded content"));
                return;
            }
            if (serial != generation) return;
            var result:String = String(ExternalInterface.call("__iggy_image_finish", token));
            if (result != "ok" || candidate == null) {
                close();
                info.dispatchEvent(new HostIOErrorEvent("ioError", false, false, result));
                return;
            }
            token = null;
            stopTimer();
            loaded = candidate;
            info.imageContent = loaded;
            addChild(loaded);
            if (serial != generation) return;
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
                if (loaded is MovieClip) MovieClip(loaded).stop();
                if (loaded.parent == this) removeChild(loaded);
                loaded = null;
                info.imageContent = null;
                info.dispatchEvent(new Event(Event.UNLOAD));
            }
        }
        public function unloadAndStop(gc:Boolean = true):void {
            var serial:uint = generation;
            var previous:DisplayObject = loaded;
            if (loaded != null) new HostMovieRequest("__iggy_unload_and_stop", info, loaded, gc);
            if (loaded != null) stopDescendants(loaded, gc);
            HostTimers.prune();
            if (serial != generation || loaded !== previous) return;
            unload();
        }
        private static function stopDescendants(object:DisplayObject, gc:Boolean):void {
            if (object is MovieClip) MovieClip(object).stop();
            if (!(object is DisplayObjectContainer)) return;
            var container:DisplayObjectContainer = DisplayObjectContainer(object);
            for (var i:int = 0; i < container.numChildren; i++) {
                var child:DisplayObject = container.getChildAt(i);
                if (child is HostLoader) HostLoader(child).unloadAndStop(gc);
                else stopDescendants(child, gc);
            }
        }
        public function get content():DisplayObject { return loaded; }
        public function get contentLoaderInfo():LoaderInfo { return info; }
    }
}
