package flash.utils {
    import com.jpexs.iggy.runtime.HostTimers;
    public function setInterval(callback:Function, delay:Number, ...args):uint {
        return HostTimers.schedule(callback, delay, false, args);
    }
}
