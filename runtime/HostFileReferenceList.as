package com.jpexs.iggy.runtime {
    import flash.events.Event;
    import flash.events.EventDispatcher;
    import flash.events.TimerEvent;
    import flash.utils.Timer;

    // Keep inheritance and cancellation usable when no host file picker exists.
    public class HostFileReferenceList extends EventDispatcher {
        private var pending:Timer;

        public function HostFileReferenceList() {
            super();
        }

        public function get fileList():Array {
            return [];
        }

        public function browse(typeFilter:Array = null):Boolean {
            if (pending != null) {
                return false;
            }
            trace("Iggy player: file selection is unavailable; dispatching cancel.");
            pending = new Timer(1, 1);
            pending.addEventListener(TimerEvent.TIMER_COMPLETE, cancelled);
            pending.start();
            return true;
        }

        private function cancelled(event:TimerEvent):void {
            pending.stop();
            pending.removeEventListener(TimerEvent.TIMER_COMPLETE, cancelled);
            pending = null;
            dispatchEvent(new Event(Event.CANCEL));
        }
    }
}
