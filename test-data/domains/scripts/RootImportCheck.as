package {
    import flash.display.MovieClip;
    import flash.system.ApplicationDomain;
    import linklib.ImportedState;
    import linklib.globalCounter;
    import linklib.incrementGlobal;

    public class RootImportCheck extends MovieClip {
        private static var startupDomain:ApplicationDomain = ApplicationDomain.currentDomain;
        public var rootImports:Boolean;
        public var stateCount:uint;
        public var nestedCount:uint;
        public var ownerOk:Boolean;
        public var onceOk:Boolean;
        public var gcLookup:Boolean;
        public var globals:Boolean;
        public var globalChecks:String;
        public var lazyGlobalOk:Boolean;

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
            var increment:Function = domain.getDefinition("linklib.incrementGlobal") as Function;
            globals = domain.hasDefinition("linklib.globalCounter") && domain.getDefinition("linklib.globalCounter") == 17
                && increment != null && increment === domain.getDefinition("linklib.incrementGlobal") && increment() == 18
                && domain.getDefinition("linklib::globalCounter") === globalCounter
                && domain.getDefinition("linklib.nestedGlobal") == 41
                && domain.hasDefinition("linklib.nullGlobal") && domain.getDefinition("linklib.nullGlobal") === null
                && domain.hasDefinition("linklib.undefinedGlobal") && domain.getDefinition("linklib.undefinedGlobal") === null
                && domain.hasDefinition("linklib.zeroGlobal") && domain.getDefinition("linklib.zeroGlobal") === 0
                && domain.hasDefinition("linklib.falseGlobal") && domain.getDefinition("linklib.falseGlobal") === false
                && domain.getDefinition("rootCounter") == 73 && domain.getDefinition("::rootCounter") == 73
                && domain.getQualifiedDefinitionNames().indexOf("rootCounter") >= 0
                && domain.getDefinition("rootCurrent")() === domain
                && String(domain.getDefinition("linklib.globalNamespace")) === "urn:iggy:test"
                && domain.getDefinition("linklib.variant1Global") == 51 && domain.getDefinition("linklib.readMixed")() == 60
                && !domain.hasDefinition("linklib.hiddenGlobal") && domain.getQualifiedDefinitionNames().indexOf("linklib::hiddenGlobal") < 0;
            lazyGlobalOk = state["globalInitializations"] == 0 && domain.hasDefinition("linklib.lazyGlobal") && state["globalInitializations"] == 0;
            var lazy:Object = domain.getDefinition("linklib.lazyGlobal");
            lazyGlobalOk = lazyGlobalOk && lazy.marker == 67 && state["globalInitializations"] == 1 && lazy === domain.getDefinition("linklib.lazyGlobal");
            globalChecks = "counter=" + domain.getDefinition("linklib.globalCounter") + ", lexical=" + globalCounter
                + ", function=" + (increment != null)
                + ", nested=" + domain.getDefinition("linklib.nestedGlobal")
                + ", null=" + (domain.getDefinition("linklib.nullGlobal") === null)
                + ", undefined=" + (domain.getDefinition("linklib.undefinedGlobal") === null)
                + ", zero=" + (domain.getDefinition("linklib.zeroGlobal") === 0)
                + ", false=" + (domain.getDefinition("linklib.falseGlobal") === false);
        }

        public function afterGc():void {
            var domain:ApplicationDomain = ApplicationDomain.currentDomain;
            var state:Class = domain.getDefinition("linklib.ImportedState") as Class;
            var nested:Class = domain.getDefinition("linklib.NestedState") as Class;
            gcLookup = domain === startupDomain && domain === loaderInfo.applicationDomain
                && state["owner"] === domain && nested["owner"] === domain
                && state["count"] == 18 && nested["count"] == 42
                && state["initializations"] == 1 && nested["initializations"] == 1
                && domain.getDefinition("linklib.globalCounter") == 18 && domain.getDefinition("linklib.globalLabel") === "blue"
                && state["globalInitializations"] == 1 && domain.getDefinition("linklib.lazyGlobal").marker == 67;
        }
    }
}
