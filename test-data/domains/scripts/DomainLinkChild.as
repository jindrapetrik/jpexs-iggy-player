package {
    import flash.display.MovieClip;
    import flash.system.ApplicationDomain;
    import flash.utils.getDefinitionByName;
    import linklib.ImportedState;
    import linklib.ImportedTile;
    import linklib.globalCounter;
    import linklib.incrementGlobal;
    import linklib.readParentGlobal;
    import linklib.parentCurrent;
    public class DomainLinkChild extends MovieClip {
        public var domain:ApplicationDomain;
        public var ordinal:uint;
        public var nested:uint;
        public var collision:uint;
        public var duplicate:uint;
        public var reflection:Boolean;
        public var methodDomain:Boolean;
        public var typed:Boolean;
        public var tile:ImportedTile;
        public var globals:Boolean;
        public var globalOrdinal:uint;
        public var globalParent:uint;
        public function DomainLinkChild() {
            super(); stop();
            domain = ApplicationDomain.currentDomain;
            ordinal = ++ImportedState.count;
            var state:Class = getDefinitionByName("linklib.NestedState") as Class;
            nested = ++state["count"];
            collision = CollisionValue.marker; duplicate = DuplicateValue.marker;
            var inherited:CollisionValue = new DerivedCollision();
            typed = inherited is CollisionValue && inherited.label() == collision;
            reflection = getDefinitionByName("linklib.ImportedState") === ImportedState && getDefinitionByName("CollisionValue") === CollisionValue && getDefinitionByName("DuplicateValue") === DuplicateValue;
            methodDomain = ImportedState.current() === domain && ImportedState.owner === domain;
            globalOrdinal = incrementGlobal();
            globalParent = readParentGlobal();
            globals = domain.getDefinition("linklib::globalCounter") === globalCounter
                && domain.getDefinition("linklib.globalCounter") == globalOrdinal
                && domain.getDefinition("linklib.incrementGlobal") is Function
                && domain.hasDefinition("linklib.nullGlobal") && domain.getDefinition("linklib.nullGlobal") === null
                && domain.hasDefinition("linklib.undefinedGlobal") && domain.getDefinition("linklib.undefinedGlobal") === null
                && domain.hasDefinition("linklib.zeroGlobal") && domain.getDefinition("linklib.zeroGlobal") === 0
                && domain.hasDefinition("linklib.falseGlobal") && domain.getDefinition("linklib.falseGlobal") === false
                && domain.getQualifiedDefinitionNames().indexOf("linklib::globalCounter") >= 0
                && domain.getQualifiedDefinitionNames().indexOf("linklib::incrementGlobal") >= 0
                && domain.hasDefinition("linklib.nestedGlobal")
                && !domain.hasDefinition("linklib.hiddenGlobal")
                && domain.getQualifiedDefinitionNames().indexOf("linklib::hiddenGlobal") < 0
                && parentCurrent() === (domain.parentDomain.hasDefinition("linklib.parentCurrent") ? domain.parentDomain : domain);
        }
        public function afterGc():Boolean { return domain.getDefinition("linklib.ImportedState") === ImportedState && ImportedState.current() === domain; }
    }
}
