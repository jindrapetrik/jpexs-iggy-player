package {
    import flash.display.Loader;
    import flash.display.MovieClip;
    import flash.events.Event;
    import flash.events.MouseEvent;
    import flash.events.TimerEvent;
    import flash.net.URLRequest;
    import flash.text.TextField;
    import flash.utils.Timer;
    import flash.utils.setInterval;
    import flash.utils.setTimeout;
    import flash.utils.clearInterval;
    import flash.utils.clearTimeout;
    import flash.media.Sound;
    import flash.media.SoundChannel;
    public class Main extends MovieClip {
        public static var marker:uint = 17;
        public var kind:String = "child";
        public var ownClass:Boolean;
        public var constructorInfo:Boolean;
        public var ticks:uint;
        public var nestedReady:Boolean;
        public var nested:Loader;
        public var timerTicks:uint;
        public var lateTicks:uint;
        public var stageTicks:uint;
        public var mouseTicks:uint;
        public var anonymousTicks:uint;
        public var clock:Timer;
        public var late:Timer;
        public var timeline:MovieClip;
        public var detached:MovieClip;
        public var intervalTicks:uint;
        public var timeoutTicks:uint;
        public var anonymousTimer:Timer;
        public var anonymousTimerTicks:uint;
        public var soundComplete:uint;
        public var tone:Sound;
        public var channel:SoundChannel;
        public var soundTimeline:MovieClip;
        public var cancelledTimers:uint;
        public var argumentsCorrect:Boolean;
        public var stopOnTick:Boolean;
        public var afterStopTicks:uint;
        public var afterStopRunning:Boolean;
        public var pendingDone:uint;
        public var pendingLoader:Loader;
        private var text:TextField;
        public function Main() {
            super();
            ownClass = Main.marker == 17;
            constructorInfo = loaderInfo != null && root === this;
            text = new TextField(); text.text = "Loaded child"; text.x = 5; text.y = 90; addChild(text);
            nested = new Loader(); nested.x = 20; nested.y = 40; addChild(nested);
            nested.contentLoaderInfo.addEventListener(Event.COMPLETE, nestedComplete);
            nested.load(new URLRequest("../loader/colors.png"));
            addEventListener(Event.ENTER_FRAME, enter);
            addEventListener(Event.ADDED_TO_STAGE, added);
            clock = new Timer(10); clock.addEventListener(TimerEvent.TIMER, timer); clock.start();
            timeline = new NestedTimeline(); addChild(timeline);
            detached = new NestedTimeline();
            setInterval(function():void { intervalTicks++; }, 10);
            setTimeout(function():void { timeoutTicks++; }, 1500);
            var cancel:uint = setInterval(function():void { cancelledTimers++; }, 10); clearTimeout(cancel);
            cancel = setTimeout(function():void { cancelledTimers++; }, 100); clearInterval(cancel);
            setTimeout(function(number:uint, text:String):void { argumentsCorrect = number == 7 && text == "ok"; }, 1, 7, "ok");
        }
        private function nestedComplete(e:Event):void { nestedReady = true; }
        private function enter(e:Event):void {
            ticks++;
            if (ticks == 2) {
                late = new Timer(10);
                late.addEventListener(TimerEvent.TIMER, function(e:TimerEvent):void { lateTicks++; });
                late.start();
            }
        }
        private function timer(e:TimerEvent):void {
            timerTicks++;
            if (stopOnTick) {
                stopOnTick = false;
                Loader(parent).unloadAndStop(false);
                var after:Timer = new Timer(10);
                after.addEventListener(TimerEvent.TIMER, function(e:TimerEvent):void { afterStopTicks++; });
                after.start(); afterStopRunning = after.running;
            }
        }
        private function added(e:Event):void {
            stage.addEventListener(Event.ENTER_FRAME, stageEnter);
            stage.addEventListener(MouseEvent.MOUSE_DOWN, mouse);
            stage.addEventListener(MouseEvent.MOUSE_UP, mouse, true);
            stage.addEventListener(Event.ENTER_FRAME, function(e:Event):void {
                anonymousTicks++;
                if (anonymousTicks == 2) {
                    anonymousTimer = new Timer(10);
                    anonymousTimer.addEventListener(TimerEvent.TIMER, function(e:TimerEvent):void { anonymousTimerTicks++; });
                    anonymousTimer.start();
                }
            });
        }
        private function stageEnter(e:Event):void { stageTicks++; }
        private function mouse(e:MouseEvent):void { mouseTicks++; }
        public function startSounds():void {
            tone = new UnloadTone(); channel = tone.play(0, 1000);
            channel.addEventListener(Event.SOUND_COMPLETE, completed);
            soundTimeline = new SoundTimeline(); addChild(soundTimeline); soundTimeline.stop();
        }
        private function completed(e:Event):void { soundComplete++; }
        public function unloadNestedImage():void { nested.unloadAndStop(false); }
        public function startPending():void {
            pendingLoader = new Loader(); addChild(pendingLoader);
            pendingLoader.contentLoaderInfo.addEventListener(Event.COMPLETE, function(e:Event):void { pendingDone++; });
            pendingLoader.load(new URLRequest("../loader/colors.png"));
        }
    }
}
