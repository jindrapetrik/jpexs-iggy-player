package {
    import flash.display.MovieClip;
    import flash.display.Loader;
    import flash.system.LoaderContext;
    import flash.media.Sound;
    import flash.media.SoundChannel;
    import flash.media.SoundTransform;
    import flash.media.SoundMixer;
    import flash.media.SoundLoaderContext;
    import flash.net.URLRequest;
    import flash.net.URLRequestHeader;
    import flash.events.Event;
    import flash.events.ProgressEvent;
    import flash.events.IOErrorEvent;
    import flash.events.SecurityErrorEvent;
    public class SoundLoaderMain extends MovieClip {
        public var baseUrl:String;
        public var completed:uint;
        public var errors:uint;
        public var security:uint;
        public var sent:uint;
        public var failures:uint;
        public var details:String="";
        public var cancelledEvents:uint;
        public var soundCompleted:uint;
        public var started:Boolean;
        public var closed:Boolean;
        public var primary:Sound;
        public var primaryUrl:String;
        public var local:Sound;
        public var active:Sound;
        public var embedded:Sound;
        public var channel:SoundChannel;
        public var child:Loader;
        private var sounds:Array=[];
        public function SoundLoaderMain() {
            super();stop();embedded=new EmbeddedTone();check(embedded.length==100,"embedded-length");
            var empty:Sound=new Sound();check(empty.bytesLoaded==0 && empty.bytesTotal==0 && empty.length==0 && empty.play()==null,"empty-sound");
            local=new Sound(new URLRequest("audio/tone-mono.mp3"),new SoundLoaderContext(2,true));watch(local,false);
            check(completed==0,"async-constructor");
        }
        private function check(value:Boolean,label:String):void {if(!value){failures++;details+=label+";";}}
        private function watch(sound:Sound,error:Boolean):void {
            sounds.push(sound);var opened:Boolean;var progress:Boolean;
            sound.addEventListener(Event.OPEN,function(event:Event):void {opened=true;check(event.target===sound,"open-target");});
            sound.addEventListener(ProgressEvent.PROGRESS,function(event:ProgressEvent):void {progress=true;check(opened && (event.bytesTotal==0 || event.bytesLoaded<=event.bytesTotal),"progress");});
            sound.addEventListener(Event.COMPLETE,function(event:Event):void {completed++;check(!error && opened && progress && event.target===sound,"complete-order");check(sound.length>100 && sound.bytesLoaded>0 && sound.bytesLoaded==sound.bytesTotal && !sound.isBuffering && !sound.isURLInaccessible,"metadata");if(sound===primary)primaryUrl=sound.url;});
            sound.addEventListener(IOErrorEvent.IO_ERROR,function(event:IOErrorEvent):void {errors++;details+="io:"+sound.url+":"+event.text+";";check(error && event.text.length>0,"io-error");});
            sound.addEventListener(SecurityErrorEvent.SECURITY_ERROR,function(event:Event):void {security++;check(error,"security-error");});
        }
        private function load(path:String,error:Boolean=false):Sound {var sound:Sound=new Sound();watch(sound,error);sound.load(new URLRequest(baseUrl+path));return sound;}
        public function start():void {primary=load("/redirect.mp3");load("/vbr.mp3");}
        public function bad():void {load("/missing",true);load("/bad.mp3",true);load("/oversize",true);load("/truncated",true);}
        public function play():void {channel=primary.play(0,1,new SoundTransform(0.5));started=channel!=null;if(channel!=null)channel.addEventListener(Event.SOUND_COMPLETE,function(event:Event):void {soundCompleted++;check(event.target===channel,"channel-target");});}
        public function stopSound():void {if(channel!=null)channel.stop();}
        public function stopAll():void {SoundMixer.stopAll();}
        public function pending():void {active=new Sound();sounds.push(active);active.addEventListener(Event.COMPLETE,unexpected);active.addEventListener(IOErrorEvent.IO_ERROR,unexpected);active.load(new URLRequest(baseUrl+"/slowheaders"));}
        private function unexpected(event:Event):void {cancelledEvents++;}
        public function closePending():void {active.close();closed=true;}
        public function cancelOpen():void {active=new Sound();sounds.push(active);active.addEventListener(Event.OPEN,function(event:Event):void {active.close();closed=true;});active.addEventListener(Event.COMPLETE,unexpected);active.load(new URLRequest(baseUrl+"/tone.mp3"));}
        public function constraints():void {
            var rejected:Boolean;
            try{primary.load(new URLRequest(baseUrl+"/tone.mp3"));}catch(error:Error){rejected=true;}check(rejected,"load-once");
            rejected=false;try{embedded.load(new URLRequest(baseUrl+"/tone.mp3"));}catch(error:Error){rejected=true;}check(rejected,"embedded-load-rejected");
            rejected=false;try{new Sound().load(null);}catch(error:Error){rejected=true;}check(rejected,"null-request");
            var source:URLRequest=new URLRequest(baseUrl+"/post.mp3");source.method="POST";source.data="sound=1";source.requestHeaders=[new URLRequestHeader("X-Test","sound")];
            var sound:Sound=new Sound();watch(sound,false);sound.load(source);
        }
        public function tls():void {load("/tone.mp3",true);}
        public function relative():void {child=new Loader();addChild(child);child.load(new URLRequest(baseUrl+"/nested/child.iggy"));}
        public function childPending():void {unloadChild();child=new Loader();addChild(child);var context:LoaderContext=new LoaderContext();context.parameters={slow:true};child.load(new URLRequest(baseUrl+"/nested/child.iggy"),context);}
        public function unloadChild():void {if(child!=null){child.unloadAndStop(true);if(child.parent==this)removeChild(child);child=null;}}
        public function orphan():void {unloadChild();channel=primary.play(0,2,new SoundTransform(0.5));sounds=[];local=null;primary=null;active=null;}
        public function get childDone():Boolean {return child!=null && child.content!=null && child.content["done"];}
        public function get childPassed():Boolean {return childDone && child.content["passed"];}
        public function drop():void {unloadChild();SoundMixer.stopAll();sounds=[];local=null;primary=null;active=null;channel=null;}
    }
}
