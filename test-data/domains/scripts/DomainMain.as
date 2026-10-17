package {
    import flash.display.Loader;
    import flash.display.MovieClip;
    import flash.events.Event;
    import flash.events.IOErrorEvent;
    import flash.net.URLRequest;
    import flash.system.ApplicationDomain;
    import flash.system.LoaderContext;
    import flash.utils.getDefinitionByName;
    import flash.utils.getQualifiedClassName;
    public class DomainMain extends MovieClip {
        public var mainDomain:ApplicationDomain;
        public var shared:ApplicationDomain;
        public var isolated:ApplicationDomain;
        public var first:Loader;
        public var second:Loader;
        public var sharedFirst:Loader;
        public var sharedSecond:Loader;
        public var into:Loader;
        public var separate:Loader;
        public var image:Loader;
        public var blocked:Loader;
        public var ready:uint;
        public var errors:uint;
        public var failures:uint;
        public var failureList:String = "";
        private var checks:uint;
        public var initialDomain:Boolean;
        public var initialLoaderInfo:Boolean;
        public var initialDefinition:Boolean;
        public var blockedOk:Boolean;
        public var missingThrows:Boolean;
        public var nullThrows:Boolean;
        public var unsupportedContextThrows:Boolean;
        public var gcLookup:Boolean;
        public var complete:Boolean;
        public function DomainMain() {
            super(); stop();
            mainDomain = ApplicationDomain.currentDomain;
            initialLoaderInfo = loaderInfo.applicationDomain === mainDomain;
            initialDefinition = mainDomain.hasDefinition("ParentOnly") && mainDomain.getDefinition("ParentOnly") === ParentOnly;
            initialDomain = initialLoaderInfo && initialDefinition;
            try { mainDomain.getDefinition("MissingDefinition"); } catch (error:ReferenceError) { missingThrows = true; }
            try { mainDomain.getDefinition(null); } catch (nullError:TypeError) { nullThrows = true; }
            var invalid:LoaderContext = new LoaderContext(); invalid.requestedContentParent = this;
            try { new Loader().load(new URLRequest("domains/child.iggy"), invalid); } catch (contextError:ArgumentError) { unsupportedContextThrows = true; }
            shared = new ApplicationDomain(mainDomain);
            isolated = new ApplicationDomain(null);
            first = start("domains/child.iggy", null);
            second = start("domains/child.iggy", null);
            sharedFirst = start("domains/child.iggy", shared);
            sharedSecond = start("domains/child.iggy", shared);
            into = start("domains/into.iggy", mainDomain);
            separate = start("domains/child.iggy", isolated);
            image = start("loader/colors.png", shared, false);
            blocked = new Loader();
            blocked.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR, rejected);
            var context:LoaderContext = new LoaderContext(false, new ApplicationDomain(null));
            context.allowCodeImport = false;
            blocked.load(new URLRequest("domains/child.iggy"), context);
            addEventListener(Event.ENTER_FRAME, inspect);
        }
        private function start(url:String, domain:ApplicationDomain, code:Boolean = true):Loader {
            var loader:Loader = new Loader(); addChild(loader);
            loader.contentLoaderInfo.addEventListener(Event.COMPLETE, loaded);
            loader.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR, failed);
            var context:LoaderContext = new LoaderContext(false, domain);
            context.parameters = {message:"domain"}; context.allowCodeImport = code;
            loader.load(new URLRequest(url), context); return loader;
        }
        private function loaded(e:Event):void { ready++; }
        private function failed(e:IOErrorEvent):void { errors++; }
        private function rejected(e:IOErrorEvent):void { blockedOk = blocked.content == null && e.text.indexOf("allowCodeImport") >= 0; }
        private function check(value:Boolean):void { checks++; if (!value) { failures++; failureList += checks + ","; } }
        private function inspect(e:Event):void {
            if (complete || ready != 7 || !blockedOk) return;
            if (!first.content["callbackDomain"] || !second.content["callbackDomain"] || !separate.content["callbackDomain"]) return;
            complete = true;
            check(initialDomain && missingThrows && nullThrows && unsupportedContextThrows && ApplicationDomain.currentDomain === mainDomain);
            check(first.contentLoaderInfo.applicationDomain !== second.contentLoaderInfo.applicationDomain);
            check(first.content["ordinal"] == 1 && second.content["ordinal"] == 1);
            check(sharedFirst.content["ordinal"] + sharedSecond.content["ordinal"] == 3 && sharedFirst.content["ordinal"] != sharedSecond.content["ordinal"]);
            check(sharedFirst.contentLoaderInfo.applicationDomain === shared && sharedSecond.contentLoaderInfo.applicationDomain === shared);
            check(first.contentLoaderInfo.applicationDomain.parentDomain === mainDomain && shared.parentDomain === mainDomain);
            check(first.contentLoaderInfo.applicationDomain.hasDefinition("ParentOnly") && !isolated.hasDefinition("ParentOnly"));
            check(isolated.hasDefinition("flash.display.Sprite") && isolated.parentDomain.parentDomain == null);
            check(into.contentLoaderInfo.applicationDomain === mainDomain && into.content["domain"] === mainDomain && mainDomain.hasDefinition("IntoDomainValue"));
            check(first.content["constructorDomain"] && first.content["reflection"] && first.content["parametersOk"]);
            check(second.content["constructorDomain"] && second.content["reflection"] && second.content["parametersOk"]);
            check(sharedFirst.content["constructorDomain"] && sharedSecond.content["constructorDomain"] && separate.content["reflection"]);
            check(first.contentLoaderInfo.applicationDomain.getDefinition("sample.DomainValue") !== second.contentLoaderInfo.applicationDomain.getDefinition("sample.DomainValue"));
            check(sharedFirst.contentLoaderInfo.applicationDomain.getDefinition("sample.DomainValue") === sharedSecond.contentLoaderInfo.applicationDomain.getDefinition("sample.DomainValue"));
            check(getDefinitionByName("ParentOnly") === ParentOnly);
            check(first.contentLoaderInfo.applicationDomain.getQualifiedDefinitionNames().indexOf("sample::DomainValue") >= 0);
            check(mainDomain.getQualifiedDefinitionNames().indexOf("sample::DomainValue") < 0);
            check(image.content != null && image.contentLoaderInfo.applicationDomain === shared);
            check(first.content["publicNames"] && second.content["publicNames"] && separate.content["publicNames"]);
            check(first.content["propertyNames"] && second.content["propertyNames"]);
            check(first.content["branches"] && second.content["branches"]);
            check(getQualifiedClassName(mainDomain) == "flash.system::ApplicationDomain" && getQualifiedClassName(new LoaderContext()) == "flash.system::LoaderContext");
        }
        public function afterGc():void {
            gcLookup = first.content["lookupAfterGc"]() && second.content["lookupAfterGc"]() && separate.content["lookupAfterGc"]();
        }
    }
}
