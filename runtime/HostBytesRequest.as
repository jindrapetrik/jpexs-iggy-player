package com.jpexs.iggy.runtime {
    import flash.display.LoaderInfo;
    import flash.utils.ByteArray;
    public class HostBytesRequest extends LoaderInfo {
        public function HostBytesRequest(bytes:ByteArray, baseUrl:String, domain:uint, allowCode:Boolean) {
            super("__iggy_bytes", bytes, baseUrl, String(domain), allowCode ? "yes" : "no");
        }
        public function get token():String { return String(content); }
    }
}
