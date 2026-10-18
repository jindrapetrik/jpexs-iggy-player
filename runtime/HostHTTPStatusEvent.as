package com.jpexs.iggy.runtime {
    import flash.events.Event;
    public class HostHTTPStatusEvent extends Event {
        public static const HTTP_STATUS:String = "httpStatus";
        public static const HTTP_RESPONSE_STATUS:String = "httpResponseStatus";
        private var code:int;
        public var redirected:Boolean;
        public var responseURL:String;
        public var responseHeaders:Array;
        public function HostHTTPStatusEvent(type:String, bubbles:Boolean = false, cancelable:Boolean = false, status:int = 0, redirected:Boolean = false) {
            super(type, bubbles, cancelable); code = status; this.redirected = redirected;
        }
        public function get status():int { return code; }
        override public function clone():Event {
            var result:HostHTTPStatusEvent = new HostHTTPStatusEvent(type, bubbles, cancelable, code, redirected);
            result.responseURL = responseURL; result.responseHeaders = responseHeaders; return result;
        }
        override public function toString():String { return formatToString("HTTPStatusEvent", "type", "bubbles", "cancelable", "eventPhase", "status", "redirected"); }
    }
}
