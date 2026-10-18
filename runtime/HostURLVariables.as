package com.jpexs.iggy.runtime {
    import flash.utils.ByteArray;
    public dynamic class HostURLVariables {
        public function HostURLVariables(source:String = null) {
            if (source != null && source.length != 0) decode(source);
        }
        public function decode(source:String):void {
            if (source == null) throw new TypeError("URLVariables source is null");
            var pairs:Array = source.split("&");
            for each (var pair:String in pairs) {
                var separator:int = pair.indexOf("=");
                if (separator < 0) throw new Error("URLVariables.decode requires name/value pairs", 2101);
                var name:String = unescapeValue(pair.substr(0, separator));
                var value:String = unescapeValue(pair.substr(separator + 1));
                if (this[name] == null) this[name] = value;
                else if (this[name] is Array) this[name].push(value);
                else this[name] = [this[name], value];
            }
        }
        public function toString():String {
            var result:Array = [];
            for (var name:String in this) {
                var prefix:String = escapeValue(name) + "=";
                var value:* = this[name];
                if (value is Array) {
                    for each (var item:* in value) result.push(prefix + escapeValue(String(item)));
                } else result.push(prefix + escapeValue(String(value)));
            }
            return result.join("&");
        }
        // Flash's form encoding escapes every non-alphanumeric UTF-8 byte.
        private static function escapeValue(value:String):String {
            var bytes:ByteArray = new ByteArray(); bytes.writeUTFBytes(value); bytes.position = 0;
            var result:Array = []; var hex:String = "0123456789ABCDEF";
            while (bytes.bytesAvailable != 0) {
                var code:uint = bytes.readUnsignedByte();
                if (code == 0) break;
                if ((code >= 48 && code <= 57) || (code >= 65 && code <= 90) || (code >= 97 && code <= 122)) result.push(String.fromCharCode(code));
                else result.push("%" + hex.charAt(code >> 4) + hex.charAt(code & 15));
            }
            return result.join("");
        }
        private static function unescapeValue(value:String):String {
            var result:Array = [];
            for (var i:int = 0; i < value.length; i++) {
                var code:uint = value.charCodeAt(i);
                if (code == 0) break;
                if (code == 43) { result.push(" "); continue; }
                if (code != 37) { result.push(value.charAt(i)); continue; }
                var bytes:ByteArray = new ByteArray();
                do {
                    var high:int = digit(value.charCodeAt(++i));
                    var low:int = high < 0 ? -1 : digit(value.charCodeAt(++i));
                    if (high < 0 || low < 0) break;
                    bytes.writeByte((high << 4) | low);
                    if (value.charAt(i + 1) != "%") break;
                    i++;
                } while (i < value.length);
                bytes.position = 0; result.push(bytes.readUTFBytes(bytes.length));
            }
            return result.join("");
        }
        private static function digit(code:Number):int {
            if (code >= 48 && code <= 57) return int(code - 48);
            if (code >= 65 && code <= 70) return int(code - 55);
            if (code >= 97 && code <= 102) return int(code - 87);
            return -1;
        }
    }
}
