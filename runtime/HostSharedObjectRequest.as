package com.jpexs.iggy.runtime {
    import flash.display.LoaderInfo;
    public class HostSharedObjectRequest extends LoaderInfo {
        public function HostSharedObjectRequest(operation:String, value:*, text:String = null) { super("__iggy_amf", operation, value, text); }
        public function get result():* { return content; }
    }
}
