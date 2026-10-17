package {
    import flash.display.MovieClip;
    import flash.system.ApplicationDomain;
    import flash.utils.getDefinitionByName;
    import linklib.ImportedState;
    import linklib.ImportedTile;
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
        }
        public function afterGc():Boolean { return domain.getDefinition("linklib.ImportedState") === ImportedState && ImportedState.current() === domain; }
    }
}
