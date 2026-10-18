package flash.xml {
    import com.jpexs.iggy.runtime.HostAmfRequest;
    // XMLDocument's serialized document value. DOM traversal is separate from AMF.
    public class XMLDocument {
        private var source:String = "";
        public var ignoreWhite:Boolean = false;
        public var status:int;
        public function XMLDocument(text:String = null) { if (text != null) parseXML(text); }
        public function parseXML(text:String):void { source = String(new HostAmfRequest("xml-document", text).result); status = 0; }
        public function toString():String { return source; }
    }
}
