package com.jpexs.iggy.runtime {
    import flash.events.Event;
    import flash.events.IOErrorEvent;
    public class HostIOErrorEvent extends IOErrorEvent {
        public static const IO_ERROR:String = "ioError";
        public function HostIOErrorEvent(type:String, bubbles:Boolean = false, cancelable:Boolean = false, text:String = "") {
            super(type, bubbles, cancelable, text);
        }
        override public function clone():Event {
            return new HostIOErrorEvent(type, bubbles, cancelable, text);
        }
    }
}
