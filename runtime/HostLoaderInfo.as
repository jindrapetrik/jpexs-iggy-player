package com.jpexs.iggy.runtime {
    import flash.display.LoaderInfo;
    public class HostLoaderInfo extends LoaderInfo {
        public var requestUrl:String;
        public function HostLoaderInfo() { super(); }
        override public function get bytesLoaded():uint { return 0; }
        override public function get bytesTotal():uint { return 0; }
        override public function get url():String { return requestUrl; }
    }
}
