package com.jpexs.iggy.runtime {
    import flash.media.Sound;
    import flash.utils.Dictionary;
    public class HostSoundLoading {
        private static var states:Dictionary=new Dictionary(true);
        private static function state(sound:Sound):HostSoundLoad {
            var value:HostSoundLoad=states[sound];
            if(value==null){value=new HostSoundLoad();states[sound]=value;}
            return value;
        }
        public static function construct(sound:Sound,request:HostURLRequest=null,context:Object=null):void {state(sound);if(request!=null)load(sound,request,context);}
        public static function load(sound:Sound,request:HostURLRequest,context:Object=null):void {state(sound).load(sound,request,context);}
        public static function close(sound:Sound):void {state(sound).close();}
        public static function bytesLoaded(sound:Sound):uint {return state(sound).loaded;}
        public static function bytesTotal(sound:Sound):uint {return state(sound).total;}
        public static function url(sound:Sound):String {return state(sound).url;}
        public static function isBuffering(sound:Sound):Boolean {return state(sound).pending;}
        public static function isURLInaccessible(sound:Sound):Boolean {return false;}
    }
}
