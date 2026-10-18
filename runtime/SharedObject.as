package flash.net {
    import flash.events.EventDispatcher;
    import com.jpexs.iggy.runtime.HostSharedObjectRequest;
    import com.jpexs.iggy.runtime.HostCodec;
    public class SharedObject extends EventDispatcher {
        private static var objects:Object = {};
        private static var defaultEncoding:uint = 3;
        private var encoding:uint;
        private var values:Object = {};
        private var key:String;
        public function SharedObject() { super(); encoding = defaultEncoding; }
        public function get data():Object { return values; }
        public static function get defaultObjectEncoding():uint { return defaultEncoding; }
        public static function set defaultObjectEncoding(value:uint):void { if (value != 0 && value != 3) throw new ArgumentError("Invalid SharedObject encoding"); defaultEncoding = value; }
        public function get objectEncoding():uint { return encoding; }
        public function set objectEncoding(value:uint):void { if (value != 0 && value != 3) throw new ArgumentError("Invalid SharedObject encoding"); encoding = value; }
        public static function getLocal(name:String, localPath:String = null, secure:Boolean = false):SharedObject {
            if (name == null || name.length == 0) throw new Error("SharedObject name is empty");
            var key:String = "$" + (localPath == null ? "/" : localPath) + "|" + name;
            if (objects[key] == null) {
                var object:SharedObject = new SharedObject();
                object.key = key;
                var saved:* = new HostSharedObjectRequest("store-read", key).result;
                if (saved === false) throw new Error("SharedObject read failed");
                if (saved != null) object.values = HostCodec.decode(String(saved));
                objects[key] = object;
            }
            return objects[key];
        }
        public function flush(minDiskSpace:int = 0):String {
            if (!new HostSharedObjectRequest("store-write", key, HostCodec.encode(values, encoding)).result) throw new Error("SharedObject write failed");
            return "flushed";
        }
        public function clear():void {
            if (!new HostSharedObjectRequest("store-clear", key).result) throw new Error("SharedObject clear failed");
            var names:Array = []; var name:String;
            for (name in values) names.push(name);
            for each (name in names) delete values[name];
        }
        public function close():void {}
        public function setProperty(name:String, value:* = null):void { data[name] = value; }
    }
}
