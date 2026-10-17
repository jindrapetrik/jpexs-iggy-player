package com.jpexs.iggy.runtime {
    import flash.display.DisplayObject;
    import flash.display.LoaderInfo;
    import flash.events.Event;
    public class HostMovieRequest extends LoaderInfo {
        private var info:LoaderInfo;
        public function HostMovieRequest(token:String, owner:LoaderInfo) {
            super(token, owner); info = owner;
        }
        public function retain(movie:DisplayObject):void {
            // An AS3 edge keeps LoaderInfo alive with retained content without
            // rooting the Loader/content cycle from the host's weak map.
            movie.addEventListener("__iggy_loader_info", keepInfo);
        }
        private function keepInfo(event:Event):void { }
    }
}
