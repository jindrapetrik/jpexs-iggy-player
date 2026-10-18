package {
    import flash.display.MovieClip;
    import flash.net.URLVariables;
    public class DataVariablesOnly extends MovieClip {
        public var passed:Boolean;
        public function DataVariablesOnly() {
            super(); stop();
            var data:URLVariables = new URLVariables("word=%C5%BE%F0%9F%99%82&repeat=a&repeat=b");
            var encoded:URLVariables = new URLVariables(); encoded.word = data.word;
            passed = data.word == "ž🙂" && data.repeat is Array && data.repeat[1] == "b" && encoded.toString() == "word=%C5%BE%F0%9F%99%82";
        }
    }
}
