package {
    import flash.events.Event;
    import flash.media.SoundChannel;

    public class __IggyHostSoundEvents {
        public static function complete(channel:SoundChannel):void {
            channel.dispatchEvent(new Event(Event.SOUND_COMPLETE));
        }
    }
}
