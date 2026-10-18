package com.jpexs.iggy.runtime {
    import flash.events.Event;
    import flash.events.ErrorEvent;
    public class HostSecurityErrorEvent extends ErrorEvent {
        public static const SECURITY_ERROR:String = "securityError";
        public function HostSecurityErrorEvent(type:String, bubbles:Boolean = false, cancelable:Boolean = false, text:String = "") { super(type, bubbles, cancelable, text); }
        override public function clone():Event { return new HostSecurityErrorEvent(type, bubbles, cancelable, text); }
    }
}
