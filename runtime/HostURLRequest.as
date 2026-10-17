package com.jpexs.iggy.runtime {
    public class HostURLRequest {
        public var url:String;
        public var data:Object;
        public var method:String = "GET";
        public var contentType:String = "application/x-www-form-urlencoded";
        public var requestHeaders:Array = [];
        public var digest:String;
        public function HostURLRequest(url:String = null) { this.url = url; }
    }
}
