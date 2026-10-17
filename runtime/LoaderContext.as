package flash.system {
    import flash.display.DisplayObjectContainer;
    public class LoaderContext {
        public var checkPolicyFile:Boolean;
        public var applicationDomain:ApplicationDomain;
        public var securityDomain:SecurityDomain;
        public var allowCodeImport:Boolean = true;
        public var allowLoadBytesCodeExecution:Boolean = true;
        public var parameters:Object;
        public var requestedContentParent:DisplayObjectContainer;
        public var imageDecodingPolicy:String = "onDemand";
        public function LoaderContext(checkPolicyFile:Boolean = false, applicationDomain:ApplicationDomain = null, securityDomain:SecurityDomain = null) {
            this.checkPolicyFile = checkPolicyFile;
            this.applicationDomain = applicationDomain;
            this.securityDomain = securityDomain;
        }
    }
}
