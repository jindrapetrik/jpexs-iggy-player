package com.jpexs.iggy.runtime {
    import flash.utils.getQualifiedClassName;
    import flash.utils.ByteArray;
    // Versioned AMF storage, with read-only compatibility for the old text format.
    public class HostCodec {
        private var source:String;
        private var cursor:int;
        private var values:int;
        private static function root(value:*):Object {
            if (value == null || getQualifiedClassName(value) != "Object") throw new Error("Invalid SharedObject root");
            return value;
        }
        public static function encode(value:*, encoding:uint = 3):String {
            if (encoding != 0 && encoding != 3) throw new ArgumentError("Invalid SharedObject objectEncoding");
            var bytes:ByteArray = new ByteArray(); bytes.objectEncoding = encoding; bytes.writeObject(root(value));
            return new HostSharedObjectRequest("base64-encode", bytes, String(encoding)).result;
        }
        public static function decode(text:String):Object {
            if (text.substr(0, 6) == "IGGY2:") {
                if ((text.charAt(6) != "0" && text.charAt(6) != "3") || text.charAt(7) != ":") throw new Error("Invalid SharedObject encoding header");
                var bytes:ByteArray = new ByteArray(); bytes.objectEncoding = uint(text.charAt(6));
                new HostSharedObjectRequest("base64-decode", bytes, text).result;
                var value:* = bytes.readObject();
                if (bytes.bytesAvailable != 0) throw new Error("Trailing SharedObject data");
                return root(value);
            }
            if (text.substr(0, 6) != "IGGY1:") throw new Error("Invalid SharedObject data version");
            if (text.length > 2 * 1024 * 1024) throw new Error("Legacy SharedObject exceeds the size limit");
            var reader:HostCodec = new HostCodec();
            reader.source = text;
            reader.cursor = 6;
            var result:* = reader.unpack(0);
            if (reader.cursor != text.length || result == null || result is Array || typeof result != "object") throw new Error("Invalid SharedObject root");
            return result;
        }
        private function length():int {
            var end:int = source.indexOf(":", cursor);
            if (end < cursor || end - cursor > 8) throw new Error("Invalid SharedObject length");
            var text:String = source.substring(cursor, end);
            var count:int = int(text);
            if (count < 0 || String(count) != text) throw new Error("Invalid SharedObject length");
            cursor = end + 1;
            return count;
        }
        private function unpack(depth:int):* {
            if (depth > 64 || ++values > 200000 || cursor >= source.length) throw new Error("Invalid SharedObject value");
            var type:String = source.charAt(cursor++);
            if (type == "N") return null;
            if (type == "U") return undefined;
            if (type == "T") return true;
            if (type == "F") return false;
            var count:int = length();
            if (type == "D" || type == "S") {
                if (count > source.length - cursor) throw new Error("Truncated SharedObject string");
                var text:String = source.substr(cursor, count);
                cursor += count;
                return type == "D" ? Number(text) : text;
            }
            if (count > 100000 || (type != "A" && type != "O")) throw new Error("Invalid SharedObject collection");
            var result:* = type == "A" ? [] : {};
            for (var i:int = 0; i < count; i++) {
                if (type == "A") result.push(unpack(depth + 1));
                else {
                    var key:* = unpack(depth + 1);
                    if (typeof key != "string") throw new Error("Invalid SharedObject key");
                    result[key] = unpack(depth + 1);
                }
            }
            return result;
        }
    }
}
