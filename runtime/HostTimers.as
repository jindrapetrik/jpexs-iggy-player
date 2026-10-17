package com.jpexs.iggy.runtime {
    import flash.utils.Dictionary;
    import flash.utils.Timer;
    public class HostTimers {
        private static var timers:Dictionary = new Dictionary(true);
        private static var next:uint;
        public static function schedule(callback:Function, delay:Number, once:Boolean, args:Array):uint {
            if (callback == null) throw new TypeError("Timer callback is null");
            // Explicit task fields avoid the older runtime's fragile captured
            // parameter/rest-array storage across collection and repeated loads.
            var task:HostTimerTask = new HostTimerTask(callback, delay, once, args, timers);
            var timer:Timer = task.clock;
            var id:uint = ++next;
            if (id == 0) id = ++next;
            // Weak keys avoid retaining stopped timers and their captured child
            // movie. Running timers are rooted by the native timer scheduler.
            timers[timer] = id;
            timer.start();
            return id;
        }
        public static function clear(id:uint):void {
            for (var key:Object in timers) {
                if (timers[key] == id) {
                    Timer(key).stop(); delete timers[key]; return;
                }
            }
        }
        public static function prune():void {
            for (var key:Object in timers) if (!Timer(key).running) delete timers[key];
        }
    }
}
