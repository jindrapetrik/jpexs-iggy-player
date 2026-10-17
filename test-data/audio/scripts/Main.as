package {
    import flash.display.MovieClip;
    import flash.events.Event;
    import flash.events.KeyboardEvent;
    import flash.media.Sound;
    import flash.media.SoundChannel;
    import flash.media.SoundMixer;
    import flash.text.TextField;

    public class Main extends MovieClip {
        public var channel:SoundChannel;
        public var second:SoundChannel;
        public var started:Boolean;
        public var plays:uint;
        public var completed:uint;
        public var selected:uint;
        public var position:uint;

        public function Main() {
            super();
            stop();
            var label:TextField = new TextField();
            label.width = 500;
            label.height = 180;
            label.text = "Audio fixture\n1: PCM16, 2: PCM8, 3: ADPCM\n4: MP3 stereo, 5: MP3 mono, 6: native PCM\nL: loop MP3, O: overlap MP3, P: stop, S: stop all";
            addChild(label);
            stage.addEventListener(KeyboardEvent.KEY_DOWN, keyDown);
        }

        private function finished(event:Event):void { completed++; }

        private function keyDown(event:KeyboardEvent):void {
            var code:uint = event.keyCode;
            if (code == 84) { position = channel != null ? channel.position : 0; return; }
            if (code == 80) {
                if (channel != null) channel.stop();
                if (second != null) second.stop();
                return;
            }
            if (code == 83) { SoundMixer.stopAll(); return; }
            if (code < 49 || code > 54) {
                if (code != 76 && code != 79) return;
            }
            if (channel != null) channel.stop();
            if (second != null) second.stop();
            var sound:Sound;
            selected = code;
            if (code == 49) sound = new Pcm16Sound();
            else if (code == 50) sound = new Pcm8Sound();
            else if (code == 51) sound = new AdpcmSound();
            else if (code == 53) sound = new MonoMp3Sound();
            else if (code == 54) sound = new NativePcmSound();
            else sound = new StereoMp3Sound();
            // Iggy interprets this parameter as total iterations, unlike Flash.
            channel = sound.play(0, code == 76 ? 2 : 0);
            started = channel != null;
            if (started) { plays++; channel.addEventListener(Event.SOUND_COMPLETE, finished); }
            if (code == 79) second = new MonoMp3Sound().play();
        }
    }
}
