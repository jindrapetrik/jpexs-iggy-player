package {
    import flash.display.MovieClip;
    import flash.system.ApplicationDomain;
    import linklib.ImportedState;

    public class RootImportCheck extends MovieClip {
        private static var startupDomain:ApplicationDomain = ApplicationDomain.currentDomain;
        public var rootImports:Boolean;
        public var stateCount:uint;
        public var nestedCount:uint;
        public var ownerOk:Boolean;
        public var onceOk:Boolean;
        public var gcLookup:Boolean;

        public function RootImportCheck() {
            super();
            stop();
            var direct:Class = ImportedState;
            var domain:ApplicationDomain = loaderInfo.applicationDomain;
            var state:Class = domain.getDefinition("linklib.ImportedState") as Class;
            var nested:Class = domain.getDefinition("linklib.NestedState") as Class;
            stateCount = state["count"];
            nestedCount = nested["count"];
            ownerOk = startupDomain === domain && state["owner"] === domain && nested["owner"] === domain;
            onceOk = state["initializations"] == 1 && nested["initializations"] == 1;
            rootImports = stateCount == 17 && nestedCount == 41 && direct === state
                && domain.getQualifiedDefinitionNames().indexOf("linklib::ImportedState") >= 0
                && !domain.parentDomain.hasDefinition("linklib.ImportedState");
            state["count"]++;
            nested["count"]++;
        }

        public function afterGc():void {
            var domain:ApplicationDomain = ApplicationDomain.currentDomain;
            var state:Class = domain.getDefinition("linklib.ImportedState") as Class;
            var nested:Class = domain.getDefinition("linklib.NestedState") as Class;
            gcLookup = domain === startupDomain && domain === loaderInfo.applicationDomain
                && state["owner"] === domain && nested["owner"] === domain
                && state["count"] == 18 && nested["count"] == 42
                && state["initializations"] == 1 && nested["initializations"] == 1;
        }
    }
}
