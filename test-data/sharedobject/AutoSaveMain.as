package {
    import flash.display.MovieClip;
    import flash.display.Loader;
    import flash.events.Event;
    import flash.net.SharedObject;
    import flash.net.registerClassAlias;
    import flash.net.URLRequest;
    import flash.system.ApplicationDomain;
    import flash.system.LoaderContext;
    import flash.utils.ByteArray;
    public class AutoSaveMain extends MovieClip {
        public var before:uint;
        public var passed:Boolean;
        public var loaded:Boolean;
        public var childBefore:uint;
        private var store:SharedObject;
        private var child:Loader;
        public function AutoSaveMain() {
            super(); stop(); registerClassAlias("auto.Record",SavedRecord);
            registerClassAlias("auto.External",AutoSavedExternal);
            store = SharedObject.getLocal("auto"); before = store.data.counter ? store.data.counter : 0;
            passed = before == 0 || valid(store.data);
            SharedObject.getLocal("empty");
            var legacy:SharedObject = SharedObject.getLocal("legacy");
            passed = passed && legacy.data.word == "žluťoučký🙂";
            SharedObject.getLocal("unchanged");
            if (before != 0) passed = passed && SharedObject.getLocal("auto0").data.counter == before
                && SharedObject.getLocal("clear-edit").data.value == before
                && SharedObject.getLocal("bad").data.value == "original"
                && SharedObject.getLocal("bad").data.unsupported === undefined
                && SharedObject.getLocal("after-bad").data.value == "survives";
        }
        private function valid(data:Object):Boolean {
            if (!(data.bytes is ByteArray)) return false;
            data.bytes.position = 0;
            var binary:Boolean = data.bytes.length == 1048576 && data.bytes.readUnsignedInt() == before;
            return data.self === data && data.left === data.right && data.left.value == before
                && data.record is SavedRecord && data.record.owner === data && data.record.shared === data.left
                && data.external is SavedExternal && data.external.value == 314 && data.external.self === data.external
                && binary;
        }
        public function edit():void {
            if (before == 0) {
                var shared:Object = {value:0}; store.data.self = store.data; store.data.left = shared; store.data.right = shared;
                var record:SavedRecord = new SavedRecord(); record.owner = store.data; record.shared = shared; store.data.record = record;
                var external:SavedExternal = new AutoSavedExternal(); external.self = external; external.shared = shared; store.data.external = external;
                var bytes:ByteArray = new ByteArray(); bytes.length = 1048576; store.data.bytes = bytes;
            }
            store.data.left.value = before + 1; store.data.counter = before + 1;
            store.data.bytes.position = 0; store.data.bytes.writeUnsignedInt(before + 1);
            var zero:SharedObject = SharedObject.getLocal("auto0"); zero.objectEncoding = 0; zero.setProperty("counter",before + 1);
            var clear:SharedObject = SharedObject.getLocal("clear"); clear.data.value = true; clear.flush(); clear.clear();
            var again:SharedObject = SharedObject.getLocal("clear-edit"); again.data.old = true; again.flush();
            var held:Object = again.data; again.clear(); held.value = before + 1;
            var transient:SharedObject = SharedObject.getLocal("transient"); transient.data.value = true; delete transient.data.value;
        }
        public function breakSave():void {
            var bad:SharedObject = SharedObject.getLocal("bad"); bad.data.value = "original"; bad.flush();
            bad.data.value = "changed"; bad.data.unsupported = function():void {};
            var good:SharedObject = SharedObject.getLocal("after-bad"); good.data.value = "survives";
        }
        public function editBlocked():void { store.data.changed = true; }
        public function loadChild():void {
            child = new Loader(); child.contentLoaderInfo.addEventListener(Event.COMPLETE,onLoaded);
            addChild(child); child.load(new URLRequest("sharedobject/auto-child.swf"),new LoaderContext(false,new ApplicationDomain(null)));
        }
        private function onLoaded(event:Event):void { childBefore = child.content["before"]; passed = passed && child.content["passed"]; loaded = true; }
        public function stopChild():void { child.unloadAndStop(true); }
    }
}
