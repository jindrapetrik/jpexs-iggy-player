package com.jpexs.iggy.runtime {
    import flash.display.LoaderInfo;
    public class HostAmfRequest extends LoaderInfo {
        public function HostAmfRequest(operation:String, value:*, extra:String = null) { super("__iggy_amf", operation, value, extra); }
        public function get result():* { return content; }
    }
}
