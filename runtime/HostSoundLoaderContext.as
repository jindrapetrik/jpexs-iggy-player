package com.jpexs.iggy.runtime {
    public class HostSoundLoaderContext {
        public var bufferTime:Number;
        public var checkPolicyFile:Boolean;
        public function HostSoundLoaderContext(bufferTime:Number=1000,checkPolicyFile:Boolean=false){this.bufferTime=bufferTime;this.checkPolicyFile=checkPolicyFile;}
    }
}
