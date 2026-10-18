package {
    import flash.display.MovieClip;
    import flash.net.SharedObject;
    import flash.net.registerClassAlias;
    import flash.utils.ByteArray;
    import flash.utils.Dictionary;
    public class StorageMain extends MovieClip {
        public var checks:uint;
        public var failures:uint;
        public var failureList:String = "";
        public var complete:Boolean;
        public var before:uint;
        public var failedFlush:Boolean;
        public var retryPassed:Boolean;
        public var graph:SharedObject;
        public function StorageMain() {
            super(); stop();
            registerClassAlias("save.Record",SavedRecord); registerClassAlias("save.External",SavedExternal);
            try {
                defaults();
                graph = SharedObject.getLocal("graph"); before = graph.data.counter ? graph.data.counter : 0;
                if (before != 0) validate(graph.data); else check(true,"first-session");
                populate(graph.data); graph.data.counter = before + 1;
                check(graph.flush() == "flushed","flush");
                check(SharedObject.getLocal("graph","/") === graph,"same-object-cache");
                var legacy:SharedObject = SharedObject.getLocal("legacy");
                check(legacy.data.word == "žluťoučký🙂" && legacy.data.list[1] === undefined && legacy.data.list[2] === null,"legacy-read");
                legacy.flush();
                var zero:SharedObject = SharedObject.getLocal("amf0"); zero.objectEncoding = 0;
                if (zero.data.counter) check(zero.data.self === zero.data && zero.data.bytes is ByteArray,"amf0-reopen");
                populate(zero.data); zero.data.counter = 1; check(zero.flush() == "flushed","amf0-write");
                var clear:SharedObject = SharedObject.getLocal("clear"); var reference:Object = clear.data;
                clear.data.self = clear.data; clear.data.a = 1; clear.data.b = "two"; clear.flush(); clear.clear();
                check(clear.data === reference && reference.a === undefined && reference.b === undefined && reference.self === undefined,"clear-live-data-reference");
                readErrors(); complete = true;
            } catch (error:Error) { failures++; failureList += "uncaught-at-" + checks; }
        }
        private function check(value:Boolean, label:String):void { checks++; if (!value) { failures++; failureList += label + ","; } }
        private function defaults():void {
            check(SharedObject.defaultObjectEncoding == 3,"shared-default");
            ByteArray.defaultObjectEncoding = 0;
            var first:SharedObject = SharedObject.getLocal("default3"); check(first.objectEncoding == 3,"independent-bytearray-default");
            SharedObject.defaultObjectEncoding = 0; var second:SharedObject = SharedObject.getLocal("default0");
            SharedObject.defaultObjectEncoding = 3; ByteArray.defaultObjectEncoding = 3;
            check(first.objectEncoding == 3 && second.objectEncoding == 0,"default-snapshot");
            var caught:Boolean; try { first.objectEncoding = 2; } catch (bad:ArgumentError) { caught = true; }
            check(caught && first.objectEncoding == 3,"invalid-encoding");
            caught = false; try { SharedObject.defaultObjectEncoding = 1; } catch (badDefault:ArgumentError) { caught = true; }
            check(caught && SharedObject.defaultObjectEncoding == 3,"invalid-default");
        }
        private function populate(data:Object):void {
            var shared:Object = {label:"žluťoučký\u0000🙂"}; data.left = shared; data.right = shared; data.self = data;
            var record:SavedRecord = new SavedRecord(); record.label = "typed"; record.owner = data; record.shared = shared; data.record = record;
            var external:SavedExternal = new SavedExternal(); external.shared = shared; external.self = external; data.external = external;
            var bytes:ByteArray = new ByteArray(); bytes.writeUTFBytes("\u0000žluťoučký🙂"); bytes.position = 3; data.bytes = bytes; data.sameBytes = bytes;
            var date:Date = new Date(123456789); data.date = date; data.sameDate = date;
            var vector:Vector.<int> = new <int>[1,-2,2147483647]; data.vector = vector; data.sameVector = vector;
            var dictionary:Dictionary = new Dictionary(true); dictionary[shared] = dictionary; data.dictionary = dictionary;
            var array:Array = [1,undefined,null]; array["owner"] = data; data.array = array;
        }
        private function validate(data:Object):void {
            check(data.self === data && data.left === data.right && data.left.label == "žluťoučký\u0000🙂","cyclic-shared-unicode");
            check(data.record is SavedRecord && data.record.owner === data && data.record.shared === data.left && data.record.label == "typed","typed-alias");
            check(data.external is SavedExternal && data.external.value == 314 && data.external.self === data.external && data.external.shared === data.left,"externalizable");
            check(data.bytes is ByteArray && data.bytes === data.sameBytes && data.bytes.length == 18,"bytearray");
            check(data.date is Date && data.date === data.sameDate && data.date.getTime() == 123456789,"date-reference");
            check(data.vector is Vector.<int> && data.vector === data.sameVector && data.vector[1] == -2,"vector-reference");
            check(data.dictionary is Dictionary && data.dictionary[data.left] === data.dictionary,"dictionary-object-key");
            check(data.array["owner"] === data && data.array[1] === undefined && data.array[2] === null,"associative-array");
        }
        private function readErrors():void {
            for each (var name:String in ["corrupt","trailing","array-root","unknown-version","unreadable"]) {
                var caught:Boolean = false; try { SharedObject.getLocal(name); } catch (error:Error) { caught = true; }
                check(caught,"read-rejected-" + name);
            }
        }
        public function failFlush():void {
            graph.data.unsupported = function():void {};
            try { graph.flush(); } catch (error:Error) { failedFlush = true; }
            delete graph.data.unsupported;
        }
        public function retryCorrupt():void {
            var object:SharedObject = SharedObject.getLocal("corrupt"); retryPassed = object.data.ok === true;
        }
        public function largeWrite():void {
            var store:SharedObject = SharedObject.getLocal("large"); var bytes:ByteArray = new ByteArray();
            bytes.length = 2097152; bytes.position = bytes.length - 4; bytes.writeUnsignedInt(0x12345678);
            store.data.bytes = bytes; check(store.flush() == "flushed","large-write");
        }
        public function largeRead():void {
            var bytes:ByteArray = SharedObject.getLocal("large").data.bytes;
            bytes.position = bytes.length - 4; check(bytes.length == 2097152 && bytes.readUnsignedInt() == 0x12345678,"large-read");
        }
        public function afterGC():void { validate(graph.data); }
    }
}
