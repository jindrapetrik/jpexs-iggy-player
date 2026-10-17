package com.jpexs.iggy.runtime {
    import flash.events.Event;
    import flash.events.ProgressEvent;
    public class HostProgressEvent extends ProgressEvent {
        private var loaded:Number;
        private var total:Number;
        public function HostProgressEvent(type:String, bubbles:Boolean = false, cancelable:Boolean = false, bytesLoaded:Number = 0, bytesTotal:Number = 0) {
            super(type, bubbles, cancelable, bytesLoaded, bytesTotal);
            loaded = bytesLoaded; total = bytesTotal;
        }
        override public function get bytesLoaded():Number { return loaded; }
        override public function set bytesLoaded(value:Number):void { loaded = value; }
        override public function get bytesTotal():Number { return total; }
        override public function set bytesTotal(value:Number):void { total = value; }
        override public function clone():Event { return new HostProgressEvent(type, bubbles, cancelable, loaded, total); }
    }
}
