package {
    import flash.display.MovieClip;
    import flash.xml.XMLDocument;
    public class AmfOnly extends MovieClip {
        public var passed:Boolean;
        public function AmfOnly() {
            super(); stop();
            var any:Vector.<*> = new Vector.<*>(2); any[0] = undefined; any[1] = "value";
            var xml:XML = new XML("<a>hello</a>");
            passed = any[0] === undefined && any[1] == "value" && xml.toString() == "hello"
                && XML.ignoreComments && XML.prettyPrinting && XML.prettyIndent == 2
                && new XMLDocument("<b/>").toString().indexOf("b") >= 0;
        }
    }
}
