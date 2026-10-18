package com.jpexs.iggy.runtime {
    import flash.media.Sound;
    import flash.events.Event;
    import flash.utils.Timer;
    public class HostSoundLoad {
        public var loaded:uint;
        public var total:uint;
        public var url:String;
        public var pending:Boolean;
        private var used:Boolean;
        private var baseUrl:String;
        private var owner:String;
        private var token:String;
        private var timer:Timer;
        private var generation:uint;
        private var opened:Boolean;
        private var progress:Number=-1;
        public function HostSoundLoad(){baseUrl=String(new HostAmfRequest("data-base",null).result);}
        public function load(sound:Sound,request:HostURLRequest,context:Object):void {
            if(request==null)throw new TypeError("Sound request is null");
            if(request.url==null||request.url.length==0)throw new ArgumentError("Sound URL is empty");
            if(used||new HostAmfRequest("data-sound-has",sound).result=="yes")throw new Error("Sound.load can only be called once per Sound");
            var packet:Object=request.packet();
            used=true;url=request.url;pending=true;
            owner=String(new HostAmfRequest("data-sound-register",sound).result);
            token=String(new HostAmfRequest("data-sound-start",packet,baseUrl).result);
            timer=new Timer(10);timer.addEventListener("timer",poll);timer.start();
        }
        private function poll(event:Event):void {
            var sound:Sound=new HostAmfRequest("data-sound-owner",owner).result as Sound;
            if(sound==null){cancel();return;}
            var serial:uint=generation;
            var invalid:Boolean=token.indexOf("error|")==0||token.indexOf("security|")==0;
            var status:String=invalid?token:String(new HostAmfRequest("data-poll",token).result);
            if(!opened){opened=true;sound.dispatchEvent(new Event(Event.OPEN));if(serial!=generation)return;}
            // Sound has no HTTP_STATUS event; URLLoader remains available to
            // callers that need to inspect response headers or status codes.
            if(status.indexOf("error|")==0||status.indexOf("security|")==0){
                cancel();
                if(status.indexOf("security|")==0)sound.dispatchEvent(new HostSecurityErrorEvent("securityError",false,false,status.substr(9)));
                else sound.dispatchEvent(new HostIOErrorEvent("ioError",false,false,status.substr(6)));
                return;
            }
            var parts:Array=status.split("|");loaded=uint(parts[1]);total=uint(parts[2]);
            if(Number(loaded)!=progress){progress=loaded;sound.dispatchEvent(new HostProgressEvent("progress",false,false,loaded,total));if(serial!=generation)return;}
            if(parts[0]!="ready")return;
            var result:String=String(new HostAmfRequest("data-sound-install",sound,token).result);
            if(result.indexOf("error|")==0){cancel();sound.dispatchEvent(new HostIOErrorEvent("ioError",false,false,result.substr(6)));return;}
            url=result;cancel();sound.dispatchEvent(new Event(Event.COMPLETE));
        }
        private function cancel():void {
            generation++;pending=false;
            if(timer!=null){timer.stop();timer.removeEventListener("timer",poll);timer=null;}
            if(token!=null)new HostAmfRequest("data-cancel",token).result;token=null;
            if(owner!=null)new HostAmfRequest("data-sound-unregister",owner).result;owner=null;
        }
        public function close():void {if(!pending)throw new Error("Sound has no active load");cancel();}
    }
}
