package flash.net {
    import flash.events.EventDispatcher;
    import flash.external.ExternalInterface;
    import com.jpexs.iggy.runtime.HostCodec;
    public class SharedObject extends EventDispatcher {
        private static var objects:Object = {};
        public var data:Object = {};
        private var key:String;
        public function SharedObject() { super(); }
        public static function getLocal(name:String, localPath:String = null, secure:Boolean = false):SharedObject {
            if (name == null || name.length == 0) throw new Error("SharedObject name is empty");
            var key:String = "$" + (localPath == null ? "/" : localPath) + "|" + name;
            if (objects[key] == null) {
                var object:SharedObject = new SharedObject();
                object.key = key;
                var saved:* = ExternalInterface.call("__iggy_shared_read", key);
                if (saved != null) object.data = HostCodec.decode(String(saved));
                objects[key] = object;
            }
            return objects[key];
        }
        public function flush(minDiskSpace:int = 0):String {
            if (!ExternalInterface.call("__iggy_shared_write", key, HostCodec.encode(data))) throw new Error("SharedObject write failed");
            return "flushed";
        }
        public function clear():void {
            if (!ExternalInterface.call("__iggy_shared_clear", key)) throw new Error("SharedObject clear failed");
            data = {};
        }
        public function close():void {}
        public function setProperty(name:String, value:* = null):void { data[name] = value; }
    }
}
