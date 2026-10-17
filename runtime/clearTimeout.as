package flash.utils {
    import com.jpexs.iggy.runtime.HostTimers;
    public function clearTimeout(id:uint):void { HostTimers.clear(id); }
}
