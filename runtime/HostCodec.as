package com.jpexs.iggy.runtime {
    import flash.utils.getQualifiedClassName;
    // Versioned, length-prefixed values. No eval and no executable save data.
    public class HostCodec {
        private var source:String;
        private var cursor:int;
        private var values:int;
        public static function encode(value:*):String {
            return "IGGY1:" + pack(value, [], 0);
        }
        private static function pack(value:*, parents:Array, depth:int):String {
            if (depth > 64) throw new Error("SharedObject nesting limit exceeded");
            if (value === undefined) return "U";
            if (value === null) return "N";
            if (typeof value == "boolean") return value ? "T" : "F";
            if (typeof value == "number") {
                var number:String = String(value);
                return "D" + number.length + ":" + number;
            }
            if (typeof value == "string") return "S" + value.length + ":" + value;
            for (var p:int = 0; p < parents.length; p++) {
                if (parents[p] === value) throw new Error("Cyclic SharedObject data is unsupported");
            }
            if (typeof value != "object") throw new Error("Unsupported SharedObject value");
            parents.push(value);
            var body:String = "";
            var key:String;
            var count:int = 0;
            if (value is Array) {
                if (value.length > 100000) throw new Error("SharedObject array limit exceeded");
                for (key in value) {
                    var index:uint = uint(key);
                    if (String(index) != key || index >= value.length) throw new Error("Associative arrays are unsupported in SharedObject data");
                }
                for (var i:int = 0; i < value.length; i++) body += pack(value[i], parents, depth + 1);
                body = "A" + value.length + ":" + body;
            } else {
                if (getQualifiedClassName(value) != "Object") throw new Error("Only plain objects are supported in SharedObject data");
                for (key in value) {
                    if (++count > 100000) throw new Error("SharedObject object limit exceeded");
                    body += pack(key, parents, depth + 1) + pack(value[key], parents, depth + 1);
                }
                body = "O" + count + ":" + body;
            }
            parents.pop();
            if (body.length > 2000000) throw new Error("SharedObject size limit exceeded");
            return body;
        }
        public static function decode(text:String):Object {
            if (text.substr(0, 6) != "IGGY1:") throw new Error("Invalid SharedObject data version");
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
