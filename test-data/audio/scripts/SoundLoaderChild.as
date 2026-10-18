package {
    import flash.display.MovieClip;
    import flash.media.Sound;
    import flash.events.Event;
    import flash.net.URLRequest;
    import flash.utils.Timer;
    public class SoundLoaderChild extends MovieClip {
        public var sound:Sound;
        public var done:Boolean;
        public var passed:Boolean;
        private var timer:Timer;
        public function SoundLoaderChild(){super();stop();sound=new Sound();timer=new Timer(20,1);timer.addEventListener("timer",fetch);timer.start();}
        private function fetch(event:Event):void {
            sound.addEventListener(Event.COMPLETE,function(event:Event):void {done=true;passed=sound.length>100 && sound.bytesLoaded>0 && sound.url.indexOf("/nested/tone.mp3")>=0;});
            sound.load(new URLRequest(loaderInfo.parameters.slow?"slowheaders":"tone.mp3"));
        }
    }
}
