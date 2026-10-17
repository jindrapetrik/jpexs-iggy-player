package com.jpexs.iggy.runtime {
    import flash.utils.ByteArray;
    import flash.utils.getQualifiedClassName;
    import flash.errors.EOFError;
    // Each public read/write has its own reference tables and network byte order.
    public class HostAmf {
        private static const MAX_BYTES:uint = 16777216;
        private static const MAX_ITEMS:uint = 100000;
        private var data:ByteArray;
        private var objects0:Array = [];
        private var objects3:Array = [];
        private var strings:Array = [];
        private var stringIndex:Object = {};
        private var traits:Array = [];
        private var values:uint;
        public static function read(bytes:ByteArray):* {
            var start:uint = bytes.position;
            if (start >= bytes.length) throw new EOFError("End of AMF data");
            var reader:HostAmf = new HostAmf(); reader.data = new ByteArray();
            reader.data.writeBytes(bytes, start, Math.min(bytes.bytesAvailable, MAX_BYTES)); reader.data.position = 0;
            try { return bytes.objectEncoding == 0 ? reader.read0(0) : reader.read3(0); }
            finally { bytes.position = start + reader.data.position; }
        }
        public static function write(bytes:ByteArray, value:*):void {
            var writer:HostAmf = new HostAmf(); writer.data = new ByteArray();
            if (bytes.objectEncoding == 0) writer.write0(value, 0); else writer.write3(value, 0);
            writer.size(); bytes.writeBytes(writer.data);
        }
        private function guard(depth:uint):void { if (depth > 32 || ++values > 200000) throw new RangeError("AMF graph limit exceeded"); size(); }
        private function size():void { if (data.length > MAX_BYTES) throw new RangeError("AMF data exceeds 16 MiB"); }
        private function count(value:uint):uint { if (value > MAX_ITEMS) throw new RangeError("AMF collection limit exceeded"); return value; }
        private function reference(table:Array, index:uint):* { if (index >= table.length) throw new Error("Invalid AMF reference"); return table[index]; }
        private function add(table:Array, value:*):void { if (table.length >= 200000) throw new RangeError("AMF reference limit exceeded"); table.push(value); }
        private function text(length:uint):String {
            if (length > MAX_BYTES || data.position + length > MAX_BYTES) throw new RangeError("AMF string exceeds 16 MiB");
            return String(new HostAmfRequest("utf", data, String(length)).result);
        }
        private function utf(value:String):ByteArray {
            var bytes:ByteArray = new ByteArray(); bytes.writeUTFBytes(value);
            if (bytes.length > MAX_BYTES) throw new RangeError("AMF string exceeds 16 MiB"); return bytes;
        }
        private function read29():uint {
            var value:uint = 0;
            for (var i:int = 0; i < 3; i++) { var next:uint = data.readUnsignedByte(); if (next < 128) return (value << 7) | next; value = (value << 7) | (next & 127); }
            return (value << 8) | data.readUnsignedByte();
        }
        private function write29(value:uint):void {
            if (value > 0x1fffffff) throw new RangeError("AMF U29 overflow");
            if (value < 128) data.writeByte(value);
            else if (value < 16384) { data.writeByte((value >> 7) | 128); data.writeByte(value & 127); }
            else if (value < 2097152) { data.writeByte((value >> 14) | 128); data.writeByte(((value >> 7) & 127) | 128); data.writeByte(value & 127); }
            else { data.writeByte((value >> 22) | 128); data.writeByte(((value >> 15) & 127) | 128); data.writeByte(((value >> 8) & 127) | 128); data.writeByte(value & 255); }
        }
        private function string3():String {
            var header:uint = read29(); if ((header & 1) == 0) return String(reference(strings, header >> 1));
            var value:String = text(header >> 1); if (value.length != 0) add(strings, value); return value;
        }
        private function putString3(value:String):void {
            if (value.length == 0) { write29(1); return; }
            var found:* = stringIndex["$" + value];
            if (found !== undefined) { write29(uint(found) << 1); return; }
            var bytes:ByteArray = utf(value); stringIndex["$" + value] = strings.length; add(strings, value);
            write29((bytes.length << 1) | 1); data.writeBytes(bytes); size();
        }
        private function putString0(value:String, longString:Boolean = false):void {
            var bytes:ByteArray = utf(value);
            if (!longString && bytes.length > 65535) throw new RangeError("AMF0 name exceeds 65535 bytes");
            if (longString) data.writeUnsignedInt(bytes.length); else data.writeShort(bytes.length);
            data.writeBytes(bytes); size();
        }
        private function schema(value:*):Object {
            var name:String = getQualifiedClassName(value);
            if (name == "Object" || name == "::Object") return {name:"", dynamic:true, fields:[]};
            var description:String = String(new HostAmfRequest("schema", name).result);
            if (description == "?") throw new TypeError("AMF cannot serialize class " + name);
            var parts:Array = description.split("\n");
            if (parts[1] == "1") throw new Error("IExternalizable AMF objects are not supported yet");
            var fields:Array = parts.length < 3 || parts[2] == "" ? [] : parts.slice(2);
            return {name:HostAmfAliases.name(value), dynamic:parts[0] == "1", fields:fields};
        }
        private function instance(name:String):Object { var type:Class = name.length == 0 ? null : HostAmfAliases.find(name); return type == null ? {} : new type(); }
        private function dense(key:String, length:uint):Boolean { var index:uint = uint(key); return String(index) == key && index < length; }
        private function assign(target:*, key:String, value:*):void {
            var index:uint = uint(key);
            if (target is Array && key == "length") throw new Error("Invalid AMF array property");
            if (target is Array && String(index) == key && index != 0xffffffff) { count(index + 1); target[index] = value; }
            else target[key] = value;
        }
        private function traitIndex(trait:Object):int {
            for (var i:int = 0; i < traits.length; i++) {
                var other:Object = traits[i];
                if (other.name == trait.name && other.dynamic == trait.dynamic && other.fields.join("\n") == trait.fields.join("\n")) return i;
            }
            return -1;
        }
        private function write3(value:*, depth:uint):void {
            guard(depth);
            if (value === undefined) { data.writeByte(0); return; }
            if (value === null) { data.writeByte(1); return; }
            if (typeof value == "boolean") { data.writeByte(value ? 3 : 2); return; }
            if (typeof value == "number") {
                if (value == int(value) && value >= -268435456 && value <= 268435455 && !(value == 0 && 1 / value < 0)) { data.writeByte(4); write29(uint(value) & 0x1fffffff); }
                else { data.writeByte(5); data.writeDouble(Number(value)); } return;
            }
            if (typeof value == "string") { data.writeByte(6); putString3(String(value)); return; }
            if (typeof value != "object") throw new TypeError("Unsupported AMF value");
            var marker:uint = value is Date ? 8 : value is Array ? 9 : value is ByteArray ? 12 : 10;
            data.writeByte(marker); var index:int = objects3.indexOf(value);
            if (index >= 0) { write29(uint(index) << 1); return; } add(objects3, value);
            if (marker == 8) { write29(1); data.writeDouble(Date(value).getTime()); return; }
            if (marker == 12) { var bytes:ByteArray = ByteArray(value); if (bytes.length > MAX_BYTES) throw new RangeError("AMF ByteArray size limit"); write29((bytes.length << 1) | 1); data.writeBytes(bytes); size(); return; }
            var key:String;
            if (marker == 9) {
                var length:uint = count(value.length); write29((length << 1) | 1);
                for (key in value) if (!dense(key, length)) { if (key.length == 0) throw new Error("Empty AMF3 associative key"); putString3(key); write3(value[key], depth + 1); }
                putString3(""); for (var i:uint = 0; i < length; i++) write3(value[i], depth + 1); return;
            }
            var trait:Object = schema(value); index = traitIndex(trait);
            if (index >= 0) write29((uint(index) << 2) | 1);
            else {
                count(trait.fields.length); add(traits, trait); write29((trait.fields.length << 4) | (trait.dynamic ? 8 : 0) | 3); putString3(trait.name);
                for each (key in trait.fields) putString3(key);
            }
            for each (key in trait.fields) write3(value[key], depth + 1);
            if (trait.dynamic) {
                for (key in value) if (trait.fields.indexOf(key) < 0) { if (key.length == 0) throw new Error("Empty AMF3 dynamic key"); putString3(key); write3(value[key], depth + 1); }
                putString3("");
            }
        }
        private function read3(depth:uint):* {
            guard(depth); var marker:uint = data.readUnsignedByte();
            if (marker == 0) return undefined; if (marker == 1) return null; if (marker == 2) return false; if (marker == 3) return true;
            if (marker == 4) { var integer:uint = read29(); return int((integer & 0x10000000) != 0 ? integer | 0xe0000000 : integer); }
            if (marker == 5) return data.readDouble(); if (marker == 6) return string3();
            if (marker != 8 && marker != 9 && marker != 10 && marker != 12) throw new Error("Unsupported AMF3 marker " + marker);
            var header:uint = read29();
            if ((header & 1) == 0) {
                var found:* = reference(objects3, header >> 1);
                if ((marker == 8 && !(found is Date)) || (marker == 9 && !(found is Array)) || (marker == 12 && !(found is ByteArray))) throw new Error("Invalid AMF object reference type");
                return found;
            }
            var result:*; var key:String; var length:uint;
            if (marker == 8) { result = new Date(data.readDouble()); add(objects3, result); return result; }
            if (marker == 12) { length = header >> 1; if (length > MAX_BYTES || data.position + length > MAX_BYTES) throw new RangeError("AMF ByteArray size limit"); result = new ByteArray(); if (length != 0) data.readBytes(result, 0, length); add(objects3, result); return result; }
            if (marker == 9) {
                length = count(header >> 1); result = []; add(objects3, result); var members:uint = 0;
                while ((key = string3()).length != 0) { count(++members); assign(result, key, read3(depth + 1)); }
                for (var i:uint = 0; i < length; i++) result[i] = read3(depth + 1); return result;
            }
            var trait:Object;
            if ((header & 2) == 0) trait = reference(traits, header >> 2);
            else {
                if ((header & 4) != 0) throw new Error("IExternalizable AMF objects are not supported yet");
                length = count(header >> 4); trait = {name:string3(), dynamic:(header & 8) != 0, fields:[]};
                for (i = 0; i < length; i++) trait.fields.push(string3()); add(traits, trait);
            }
            result = instance(trait.name); add(objects3, result);
            for each (key in trait.fields) result[key] = read3(depth + 1);
            if (trait.dynamic) { members = 0; while ((key = string3()).length != 0) { count(++members); result[key] = read3(depth + 1); } }
            return result;
        }
        private function write0(value:*, depth:uint):void {
            guard(depth);
            if (value === undefined) { data.writeByte(6); return; } if (value === null) { data.writeByte(5); return; }
            if (typeof value == "boolean") { data.writeByte(1); data.writeBoolean(value); return; }
            if (typeof value == "number") { data.writeByte(0); data.writeDouble(Number(value)); return; }
            if (typeof value == "string") {
                var bytes:ByteArray = utf(String(value)); data.writeByte(bytes.length > 65535 ? 12 : 2);
                if (bytes.length > 65535) data.writeUnsignedInt(bytes.length); else data.writeShort(bytes.length); data.writeBytes(bytes); return;
            }
            if (typeof value != "object") throw new TypeError("Unsupported AMF value");
            if (value is Date) { data.writeByte(11); data.writeDouble(Date(value).getTime()); data.writeShort(0); return; }
            if (value is ByteArray) { data.writeByte(17); write3(value, depth + 1); return; }
            var index:int = objects0.indexOf(value);
            if (index >= 0) { if (index > 65535) throw new RangeError("AMF0 reference overflow"); data.writeByte(7); data.writeShort(index); return; }
            add(objects0, value); var key:String;
            if (value is Array) {
                var length:uint = count(value.length); var associative:Boolean = false;
                for (key in value) if (!dense(key, length)) associative = true;
                data.writeByte(associative ? 8 : 10); data.writeUnsignedInt(length);
                if (!associative) { for (var i:uint = 0; i < length; i++) write0(value[i], depth + 1); return; }
                for (key in value) { putString0(key); write0(value[key], depth + 1); }
            } else {
                var trait:Object = schema(value); data.writeByte(trait.name.length == 0 ? 3 : 16);
                if (trait.name.length != 0) putString0(trait.name);
                for each (key in trait.fields) { putString0(key); write0(value[key], depth + 1); }
                if (trait.dynamic) for (key in value) if (trait.fields.indexOf(key) < 0) { putString0(key); write0(value[key], depth + 1); }
            }
            data.writeShort(0); data.writeByte(9);
        }
        private function read0(depth:uint, marker:int = -1):* {
            guard(depth); if (marker < 0) marker = data.readUnsignedByte();
            if (marker == 0) return data.readDouble(); if (marker == 1) return data.readBoolean();
            if (marker == 2) return text(data.readUnsignedShort()); if (marker == 12) return text(data.readUnsignedInt());
            if (marker == 5) return null; if (marker == 6 || marker == 13) return undefined;
            if (marker == 7) return reference(objects0, data.readUnsignedShort());
            if (marker == 17) return read3(depth + 1);
            if (marker == 11) { var time:Number = data.readDouble(); data.readShort(); return new Date(time); }
            var result:*; var length:uint;
            if (marker == 10) { length = count(data.readUnsignedInt()); result = []; add(objects0, result); for (var i:uint = 0; i < length; i++) result.push(read0(depth + 1)); return result; }
            if (marker == 3) result = {};
            else if (marker == 8) { length = count(data.readUnsignedInt()); result = []; result.length = length; }
            else if (marker == 16) result = instance(text(data.readUnsignedShort()));
            else throw new Error("Unsupported AMF0 marker " + marker);
            add(objects0, result); var members:uint = 0;
            while (true) {
                var key:String = text(data.readUnsignedShort()); var next:uint = data.readUnsignedByte();
                if (key.length == 0 && next == 9) return result;
                count(++members); assign(result, key, read0(depth + 1, next));
            }
            return result;
        }
    }
}
