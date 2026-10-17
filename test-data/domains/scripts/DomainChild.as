package {
    import flash.display.MovieClip;
    import flash.system.ApplicationDomain;
    import flash.utils.getDefinitionByName;
    import flash.utils.getQualifiedClassName;
    import flash.utils.getQualifiedSuperclassName;
    import flash.utils.Timer;
    import flash.events.TimerEvent;
    import sample.DomainValue;
    public class DomainChild extends MovieClip {
        public var domain:ApplicationDomain;
        public var ordinal:uint;
        public var constructorDomain:Boolean;
        public var reflection:Boolean;
        public var directReflection:Boolean;
        public var namedReflection:Boolean;
        public var parametersOk:Boolean;
        public var callbackDomain:Boolean;
        public var publicNames:Boolean;
        public var propertyNames:Boolean;
        public var branches:Boolean;
        private var timer:Timer;
        public function DomainChild() {
            super(); stop();
            domain = ApplicationDomain.currentDomain;
            ordinal = ++DomainValue.count;
            constructorDomain = loaderInfo.applicationDomain === domain;
            namedReflection = getDefinitionByName("sample.DomainValue") === DomainValue;
            directReflection = domain.getDefinition("sample::DomainValue") === DomainValue;
            reflection = namedReflection && directReflection;
            publicNames = getQualifiedClassName(this) == "DomainChild" && getQualifiedClassName(new DomainValue()) == "sample::DomainValue" && getQualifiedSuperclassName(this) == "flash.display::MovieClip";
            var object:Object = {DomainChild:7, DomainValue:9};
            propertyNames = object.DomainChild == 7 && object.DomainValue == 9;
            object.DomainChild = 8;
            propertyNames = propertyNames && object.DomainChild == 8 && object.hasOwnProperty("DomainChild");
            var typed:PropertyContainer = new PropertyContainer();
            propertyNames = propertyNames && typed.DomainChild == 7;
            typed.DomainChild = 9;
            propertyNames = propertyNames && typed.DomainChild == 9 && new MethodContainer().DomainChild() == 11;
            branches = probe(0) && probe(1) && probe(2) && probe(3);
            parametersOk = loaderInfo.parameters != null && loaderInfo.parameters.message == "domain";
            timer = new Timer(10);
            timer.addEventListener(TimerEvent.TIMER, tick); timer.start();
        }
        private function tick(e:TimerEvent):void { callbackDomain = ApplicationDomain.currentDomain === domain; }
        private function probe(value:int):Boolean {
            try {
                switch (value) {
                    case 0: return new PropertyContainer().DomainChild == 7;
                    case 1: return domain.getDefinition("sample.DomainValue") === DomainValue;
                    case 2: var type:Class = DomainChild; return type === getDefinitionByName("DomainChild");
                    default: throw new Error("probe");
                }
            } catch (error:Error) { return value == 3; }
            return false;
        }
        public function lookupAfterGc():Boolean { return domain.getDefinition("sample.DomainValue") === DomainValue; }
    }
}
