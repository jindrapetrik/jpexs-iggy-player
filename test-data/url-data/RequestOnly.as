package {
    import flash.display.MovieClip;
    import flash.net.URLRequestMethod;
    import flash.net.URLRequestHeader;
    public class RequestOnly extends MovieClip {
        public var passed:Boolean;
        public function RequestOnly() {
            super();stop();var header:URLRequestHeader=new URLRequestHeader("X-Test","value");
            passed=URLRequestMethod.GET=="GET" && URLRequestMethod.POST=="POST" && header.name=="X-Test" && header.value=="value";
        }
    }
}
