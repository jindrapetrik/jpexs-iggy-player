package flash.utils {
    import com.jpexs.iggy.runtime.HostTimers;
    public function setTimeout(callback:Function, delay:Number, ...args):uint {
        return HostTimers.schedule(callback, delay, true, args);
    }
}
