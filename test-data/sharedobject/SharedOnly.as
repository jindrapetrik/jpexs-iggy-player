package {
    import flash.display.MovieClip;
    import flash.net.SharedObject;
    public class SharedOnly extends MovieClip {
        public var passed:Boolean;
        public var before:uint;
        public function SharedOnly() {
            super(); stop();
            var saved:SharedObject = SharedObject.getLocal("only"); before = saved.data.counter ? saved.data.counter : 0;
            passed = before == 0 || saved.data.self === saved.data;
            saved.data.counter = before + 1; saved.data.self = saved.data;
            passed = passed && saved.flush() == "flushed";
        }
    }
}
