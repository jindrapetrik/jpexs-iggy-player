package com.jpexs.iggy.runtime {
    import flash.events.EventDispatcher;
    import flash.events.TimerEvent;
    import flash.utils.Dictionary;
    import flash.utils.Timer;
    public class HostTimerTask extends EventDispatcher {
        public var clock:Timer;
        private var callback:Function;
        private var parameters:Array;
        private var once:Boolean;
        private var registry:Dictionary;
        public function HostTimerTask(callback:Function, delay:Number, once:Boolean, args:Array, registry:Dictionary) {
            super();
            this.callback = callback; this.parameters = args;
            this.once = once; this.registry = registry;
            clock = new Timer(Math.max(1, delay), once ? 1 : 0);
            clock.addEventListener(TimerEvent.TIMER, tick);
        }
        private function tick(event:TimerEvent):void {
            if (once) {
                delete registry[clock];
                clock.removeEventListener(TimerEvent.TIMER, tick);
            }
            callback.apply(null, parameters);
        }
    }
}
