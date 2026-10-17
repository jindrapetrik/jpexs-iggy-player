package {
    import flash.display.Loader;
    import flash.display.MovieClip;
    import flash.events.Event;
    import flash.events.IOErrorEvent;
    import flash.net.URLRequest;
    import flash.system.ApplicationDomain;
    import flash.system.LoaderContext;
    public class DomainLinkMain extends MovieClip {
        public var children:Array = [];
        public var shared:ApplicationDomain;
        public var ready:uint;
        public var errors:uint;
        public var failures:uint;
        public var failureList:String = "";
        public var complete:Boolean;
        public var gcLookup:Boolean;
        private var checks:uint;
        public function DomainLinkMain() {
            super(); stop(); shared = new ApplicationDomain(ApplicationDomain.currentDomain);
            start(1, null); start(2, null); start(1, shared); start(1, new ApplicationDomain(null));
            addEventListener(Event.ENTER_FRAME, inspect);
        }
        private function start(variant:uint, domain:ApplicationDomain, file:String = "child.iggy"):void {
            var loader:Loader = new Loader(); children.push(loader); addChild(loader);
            loader.x = children.length * 60;
            loader.contentLoaderInfo.addEventListener(Event.COMPLETE, loaded);
            loader.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR, failed);
            loader.load(new URLRequest("link" + variant + "/" + file), new LoaderContext(false, domain));
        }
        private function loaded(e:Event):void { ready++; if (ready == 4) start(2, shared); if (ready == 5) start(1, shared, "child-again.iggy"); }
        private function failed(e:IOErrorEvent):void { errors++; trace(e.text); }
        private function check(value:Boolean):void { checks++; if (!value) { failures++; failureList += checks + ","; } }
        private function inspect(e:Event):void {
            if (complete || ready != 6) return;
            complete = true;
            var a:Object = children[0].content; var b:Object = children[1].content;
            var first:Object = children[2].content; var isolated:Object = children[3].content; var second:Object = children[4].content;
            check(a.ordinal == 18 && b.ordinal == 30 && isolated.ordinal == 18);
            check(first.ordinal == 18 && second.ordinal == 19);
            check(children[5].content.ordinal == 20 && children[5].content.nested == 44 && children[5].content.domain === shared);
            check(a.nested == 42 && b.nested == 43 && isolated.nested == 42);
            check(first.nested == 42 && second.nested == 43);
            check(a.collision == 101 && b.collision == 101 && first.collision == 101 && second.collision == 101 && isolated.collision == 201);
            check(a.duplicate == 10 && b.duplicate == 20 && first.duplicate == 10 && second.duplicate == 10);
            check(a.domain !== b.domain && first.domain === second.domain && first.domain === shared);
            check(a.domain.getDefinition("linklib.ImportedState") !== b.domain.getDefinition("linklib.ImportedState"));
            check(first.domain.getDefinition("linklib.ImportedState") === second.domain.getDefinition("linklib.ImportedState"));
            check(!ApplicationDomain.currentDomain.hasDefinition("linklib.ImportedState"));
            check(a.domain.getQualifiedDefinitionNames().indexOf("CollisionValue") < 0 && isolated.domain.getQualifiedDefinitionNames().indexOf("CollisionValue") >= 0);
            for each (var loader:Loader in children) {
                check(loader.content["reflection"] && loader.content["methodDomain"] && loader.content["typed"]);
                check(loader.content["globals"]);
                var fn:Function = loader.content["domain"].getDefinition("linklib.globalCurrent") as Function;
                check(fn() === loader.content["domain"] && fn === loader.content["domain"].getDefinition("linklib.globalCurrent"));
                check(loader.content["tile"] != null && loader.content["tile"].width == 40);
            }
            check(a.globalOrdinal == 18 && b.globalOrdinal == 30 && isolated.globalOrdinal == 18);
            check(first.globalOrdinal == 18 && second.globalOrdinal == 19 && children[5].content.globalOrdinal == 20);
            check(a.globalParent == 101 && first.globalParent == 101 && second.globalParent == 101 && isolated.globalParent == 201);
            check(first.domain.getDefinition("linklib.globalLabel") === "blue" && second.domain.getDefinition("linklib.globalLabel") === "blue" && b.domain.getDefinition("linklib.globalLabel") === "red");
            check(first.domain.getDefinition("linklib.globalCounter") == 20 && a.domain.getDefinition("linklib.globalCounter") == 18);
            check(!ApplicationDomain.currentDomain.hasDefinition("linklib.globalCounter"));
            check(shared.getDefinition("linklib.variant1Global") == 51 && shared.getDefinition("linklib.variant2Global") == 52);
            check(shared.getDefinition("linklib.readMixed")() == 60 && b.domain.getDefinition("linklib.readMixed")() == 61);
        }
        public function afterGc():void {
            gcLookup = true;
            for each (var loader:Loader in children) {
                var domain:ApplicationDomain = loader.content["domain"];
                var fn:Function = domain.getDefinition("linklib.globalCurrent") as Function;
                gcLookup = gcLookup && loader.content["afterGc"]() && fn() === domain && domain.hasDefinition("linklib.undefinedGlobal");
            }
            gcLookup = gcLookup && shared.getDefinition("linklib.globalCounter") == 20;
        }
    }
}
