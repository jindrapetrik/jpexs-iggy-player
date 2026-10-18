package com.jpexs.iggy.runtime {
    import flash.events.EventDispatcher;
    import flash.events.Event;
    import flash.events.TimerEvent;
    import flash.utils.Timer;
    import flash.utils.ByteArray;
    public class HostURLLoader extends EventDispatcher {
        public var bytesLoaded:uint;
        public var bytesTotal:uint;
        public var data:*;
        public var dataFormat:String = "text";
        private var baseUrl:String;
        private var token:String;
        private var timer:Timer;
        private var generation:uint;
        private var opened:Boolean;
        private var progress:Number;
        // The DLL does not trace strong listeners on this scripted dispatcher
        // subclass reliably. Keep them in ordinary AS3 slots, reachable with it.
        private var listeners:Array = [];
        override public function addEventListener(type:String, listener:Function, useCapture:Boolean = false, priority:int = 0, useWeakReference:Boolean = false):void {
            super.addEventListener(type,listener,useCapture,priority,useWeakReference);
            if (listener == null || useWeakReference) return;
            for each (var entry:Object in listeners) if (entry.type == type && entry.listener === listener && entry.capture == useCapture) return;
            listeners.push({type:type,listener:listener,capture:useCapture});
        }
        override public function removeEventListener(type:String, listener:Function, useCapture:Boolean = false):void {
            super.removeEventListener(type,listener,useCapture);
            for (var i:int = listeners.length - 1; i >= 0; i--) if (listeners[i].type == type && listeners[i].listener === listener && listeners[i].capture == useCapture) listeners.splice(i,1);
        }
        public function HostURLLoader(request:HostURLRequest = null) {
            super(); baseUrl = String(new HostAmfRequest("data-base", null).result);
            if (request != null) load(request);
        }
        public function load(request:HostURLRequest):void {
            if (request == null) throw new TypeError("URLLoader request is null");
            if (request.url == null || request.url.length == 0) throw new ArgumentError("URLLoader URL is empty");
            if (dataFormat != "text" && dataFormat != "binary" && dataFormat != "variables") throw new ArgumentError("Unsupported URLLoader data format");
            if (request.method != "GET" || request.data != null || (request.requestHeaders != null && request.requestHeaders.length != 0)) throw new ArgumentError("Local URLLoader supports GET without a body or headers");
            cancel(); data = null; bytesLoaded = 0; bytesTotal = 0; opened = false; progress = -1;
            token = String(new HostAmfRequest("data-start", request.url, baseUrl).result);
            timer = new Timer(10); timer.addEventListener(TimerEvent.TIMER, poll); timer.start();
        }
        private function poll(event:TimerEvent):void {
            var serial:uint = generation;
            var active:String = token;
            var status:String = token.indexOf("error|") == 0 || token.indexOf("security|") == 0 ? token : String(new HostAmfRequest("data-poll", token).result);
            if (status.indexOf("error|") == 0 || status.indexOf("security|") == 0) {
                cancel();
                if (status.indexOf("security|") == 0) dispatchEvent(new HostSecurityErrorEvent("securityError", false, false, status.substr(9)));
                else dispatchEvent(new HostIOErrorEvent("ioError", false, false, status.substr(6)));
                return;
            }
            if (!opened) {
                opened = true; dispatchEvent(new Event(Event.OPEN));
                if (serial != generation || token != active) return;
            }
            var parts:Array = status.split("|");
            bytesLoaded = uint(parts[1]); bytesTotal = uint(parts[2]);
            if (Number(bytesLoaded) != progress) {
                progress = bytesLoaded;
                dispatchEvent(new HostProgressEvent("progress", false, false, bytesLoaded, bytesTotal));
                if (serial != generation || token != active) return;
            }
            if (parts[0] != "ready") return;
            var candidate:*;
            if (dataFormat == "binary") {
                candidate = new ByteArray(); new HostAmfRequest("data-bytes", candidate, token).result;
            } else if (dataFormat == "text") candidate = new HostAmfRequest("data-text", token).result;
            else if (dataFormat == "variables") {
                try { candidate = new HostURLVariables(String(new HostAmfRequest("data-text", token).result)); }
                catch (error:Error) { cancel(); dispatchEvent(new HostIOErrorEvent("ioError", false, false, "Invalid URL-encoded variables")); return; }
            }
            else { cancel(); dispatchEvent(new HostIOErrorEvent("ioError", false, false, "Unsupported URLLoader data format")); return; }
            new HostAmfRequest("data-cancel", token).result;
            token = null; stopTimer(); data = candidate;
            dispatchEvent(new Event(Event.COMPLETE));
        }
        private function stopTimer():void {
            if (timer != null) { timer.stop(); timer.removeEventListener(TimerEvent.TIMER, poll); timer = null; }
        }
        private function cancel():void {
            generation++; stopTimer();
            if (token != null) new HostAmfRequest("data-cancel", token).result;
            token = null;
        }
        public function close():void {
            if (token == null) throw new Error("URLLoader has no active load");
            cancel();
        }
    }
}
