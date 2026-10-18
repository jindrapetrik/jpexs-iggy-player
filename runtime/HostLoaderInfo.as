package com.jpexs.iggy.runtime {
    import flash.display.DisplayObject;
    import flash.display.LoaderInfo;
    import flash.system.ApplicationDomain;
    public class HostLoaderInfo extends LoaderInfo {
        public var requestUrl:String;
        public var requestBytes:uint;
        public var requestTotal:uint;
        private var listeners:Array = [];
        override public function addEventListener(type:String, listener:Function, useCapture:Boolean = false, priority:int = 0, useWeakReference:Boolean = false):void {
            super.addEventListener(type,listener,useCapture,priority,useWeakReference);
            if (listener == null || useWeakReference) return;
            for each (var entry:Object in listeners) if (entry.type == type && entry.listener === listener && entry.capture == useCapture) return;
            listeners.push({type:type,listener:listener,capture:useCapture});
        }
        override public function removeEventListener(type:String, listener:Function, useCapture:Boolean = false):void {
            super.removeEventListener(type,listener,useCapture);
            for (var i:int = listeners.length - 1; i >= 0; i--) if (listeners[i].type == type && listeners[i].listener === listener && listeners[i].capture == useCapture) listeners.splice(i,1);
        }
        public var imageWidth:int;
        public var imageHeight:int;
        public var mimeType:String;
        public var imageContent:DisplayObject;
        public var owner:DisplayObject;
        public var requestDomain:ApplicationDomain;
        public var requestParameters:Object;
        public function HostLoaderInfo(owner:DisplayObject = null) { super(owner); this.owner = owner; }
        override public function get bytesLoaded():uint { return requestBytes; }
        override public function get bytesTotal():uint { return requestTotal; }
        override public function get url():String { return requestUrl; }
        override public function get content():DisplayObject { return imageContent; }
        override public function get width():int { return imageWidth; }
        override public function get height():int { return imageHeight; }
        override public function get contentType():String { return mimeType; }
        override public function get applicationDomain():ApplicationDomain { return requestDomain; }
        override public function get parameters():Object { return requestParameters; }
    }
}
