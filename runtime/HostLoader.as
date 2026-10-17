package com.jpexs.iggy.runtime {
    import flash.display.DisplayObject;
    import flash.display.LoaderInfo;
    import flash.display.Sprite;
    import flash.events.TimerEvent;
    import flash.system.LoaderContext;
    import flash.utils.Timer;
    public class HostLoader extends Sprite {
        private var info:HostLoaderInfo = new HostLoaderInfo();
        private var pending:Timer;
        public function HostLoader() { super(); }
        public function load(request:HostURLRequest, context:LoaderContext = null):void {
            close();
            if (request == null) throw new TypeError("Loader request is null");
            info.requestUrl = request.url;
            trace("Iggy player: external Loader resources are unavailable; dispatching ioError.");
            pending = new Timer(1, 1);
            pending.addEventListener(TimerEvent.TIMER_COMPLETE, failed);
            pending.start();
        }
        private function failed(event:TimerEvent):void {
            close();
            info.dispatchEvent(new HostIOErrorEvent(HostIOErrorEvent.IO_ERROR, false, false, "External resource loading is unavailable in this Iggy player"));
        }
        public function close():void {
            if (pending != null) {
                pending.stop();
                pending.removeEventListener(TimerEvent.TIMER_COMPLETE, failed);
                pending = null;
            }
        }
        public function unload():void { close(); }
        public function get content():DisplayObject { return null; }
        public function get contentLoaderInfo():LoaderInfo { return info; }
    }
}
