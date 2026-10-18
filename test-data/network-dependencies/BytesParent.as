package {
    import flash.display.MovieClip;
    import flash.display.Loader;
    import flash.events.Event;
    import flash.net.URLLoader;
    import flash.net.URLLoaderDataFormat;
    import flash.net.URLRequest;
    public class RemoteBytesParent extends MovieClip {
        private var data:URLLoader;
        public var child:Loader;
        public function RemoteBytesParent() {
            super();stop();data=new URLLoader();data.dataFormat=URLLoaderDataFormat.BINARY;
            data.addEventListener(Event.COMPLETE,function(event:Event):void {child=new Loader();addChild(child);child.loadBytes(data.data);});
            data.load(new URLRequest("bytes-root.iggy"));
        }
        public function get done():Boolean {return child!=null && child.content!=null && child.content["done"];}
        public function get passed():Boolean {return done && child.content["passed"];}
    }
}
