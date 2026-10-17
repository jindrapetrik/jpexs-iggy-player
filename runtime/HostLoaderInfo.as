package com.jpexs.iggy.runtime {
    import flash.display.DisplayObject;
    import flash.display.LoaderInfo;
    import flash.system.ApplicationDomain;
    public class HostLoaderInfo extends LoaderInfo {
        public var requestUrl:String;
        public var requestBytes:uint;
        public var imageWidth:int;
        public var imageHeight:int;
        public var mimeType:String;
        public var imageContent:DisplayObject;
        public var owner:DisplayObject;
        public var requestDomain:ApplicationDomain;
        public var requestParameters:Object;
        public function HostLoaderInfo(owner:DisplayObject = null) { super(owner); this.owner = owner; }
        override public function get bytesLoaded():uint { return requestBytes; }
        override public function get bytesTotal():uint { return requestBytes; }
        override public function get url():String { return requestUrl; }
        override public function get content():DisplayObject { return imageContent; }
        override public function get width():int { return imageWidth; }
        override public function get height():int { return imageHeight; }
        override public function get contentType():String { return mimeType; }
        override public function get applicationDomain():ApplicationDomain { return requestDomain; }
        override public function get parameters():Object { return requestParameters; }
    }
}
