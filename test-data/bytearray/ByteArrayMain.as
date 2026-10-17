package {
    import flash.display.Loader;
    import flash.display.MovieClip;
    import flash.errors.EOFError;
    import flash.events.Event;
    import flash.events.IOErrorEvent;
    import flash.system.ApplicationDomain;
    import flash.utils.ByteArray;
    import flash.utils.Endian;
    import flash.utils.CompressionAlgorithm;
    public class ByteArrayMain extends MovieClip {
        public var checks:uint;
        public var failures:uint;
        public var failureList:String = "";
        public var complete:Boolean;
        public var imageReady:Boolean;
        public var retained:ByteArray;
        public var image:Loader;
        public function ByteArrayMain() {
            super(); stop();
            try { numbers(false); numbers(true); strings(); compression(); errors(); }
            catch (error:Error) { failures++; failureList += "uncaught:" + error.message; }
            retained = new ByteArray(); retained.endian = Endian.LITTLE_ENDIAN; retained.writeInt(0x12345678);
            var temporary:ByteArray = new ByteArray(); temporary.endian = Endian.LITTLE_ENDIAN; temporary.writeUTF("collect me");
            complete = true;
        }
        private function check(value:Boolean, name:String):void { checks++; if (!value) { failures++; failureList += name + ","; } }
        private function same(left:ByteArray, right:ByteArray):Boolean {
            if (left.length != right.length) return false;
            left.position = 0; right.position = 0;
            while (left.bytesAvailable != 0) if (left.readUnsignedByte() != right.readUnsignedByte()) return false;
            return true;
        }
        private function numbers(little:Boolean):void {
            var bytes:ByteArray = new ByteArray(); check(bytes.endian == Endian.BIG_ENDIAN, "default-endian");
            bytes.endian = little ? Endian.LITTLE_ENDIAN : Endian.BIG_ENDIAN;
            bytes.writeBoolean(true); bytes.writeBoolean(false); bytes.writeByte(-128); bytes.writeShort(-12345);
            bytes.writeInt(-123456789); bytes.writeUnsignedInt(0xfedcba98); bytes.writeFloat(1.5); bytes.writeDouble(-Math.PI);
            bytes.writeFloat(Infinity); bytes.writeDouble(NaN); bytes.writeFloat(-0.0); bytes.writeDouble(-0.0);
            bytes.writeFloat(1.401298464324817e-45); bytes.writeDouble(4.9406564584124654e-324);
            var reference:ByteArray = little ? new NumericLittle() : new NumericBig();
            // NaN payloads may differ; compare the other independently encoded Java bytes.
            var referenceNan:uint = 29; bytes.position = referenceNan; reference.position = referenceNan;
            for (var i:int = 0; i < 8; i++) bytes.writeByte(reference.readUnsignedByte());
            check(same(bytes, reference), "java-numeric-bytes-" + little);
            bytes.position = 0;
            check(bytes.readBoolean() && !bytes.readBoolean() && bytes.readByte() == -128, "boolean-byte");
            check(bytes.readShort() == -12345 && bytes.readInt() == -123456789 && bytes.readUnsignedInt() == 0xfedcba98, "signed-unsigned");
            check(bytes.readFloat() == 1.5 && bytes.readDouble() == -Math.PI, "float-double");
            check(bytes.readFloat() == Infinity && isNaN(bytes.readDouble()), "infinity-nan");
            check(1 / bytes.readFloat() == -Infinity && 1 / bytes.readDouble() == -Infinity, "negative-zero");
            check(bytes.readFloat() == 1.401298464324817e-45 && bytes.readDouble() == 4.9406564584124654e-324, "subnormals");
            bytes.length = 0; bytes.position = 1; bytes.writeShort(0x12345); bytes.position = 0;
            check(bytes.readUnsignedByte() == 0 && bytes.readUnsignedShort() == 0x2345, "gap-short-truncation");
            var other:ByteArray = new ByteArray(); check(other.endian == Endian.BIG_ENDIAN, "per-instance-endian");
            bytes.length = 0; var dynamicBytes:Object = bytes; dynamicBytes.writeInt("123"); dynamicBytes.writeFloat("-1.25"); bytes.position = 0;
            check(bytes.readInt() == 123 && bytes.readFloat() == -1.25, "native-numeric-coercion");
        }
        private function strings():void {
            var bytes:ByteArray = new ByteArray(); var text:String = "Příliš žluťoučký kůň 🐕";
            bytes.writeUTF(text); bytes.position = 0; check(bytes.readUTF() == text, "utf8-length-prefix");
            bytes.length = 0; bytes.endian = Endian.LITTLE_ENDIAN; bytes.writeUTF("é"); bytes.position = 0;
            check(bytes.readUnsignedByte() == 2 && bytes.readUnsignedByte() == 0 && bytes.readUTFBytes(2) == "é", "utf-little-prefix");
            bytes.length = 0; bytes.writeUTFBytes(text); check(bytes.toString() == text, "utf8-toString");
            bytes.position = 0; check(bytes.readUTFBytes(bytes.length) == text, "utf8-bytes");
            bytes.length = 0; bytes.writeByte(0xef); bytes.writeByte(0xbb); bytes.writeByte(0xbf); bytes.writeUTFBytes("ab\u0000cd");
            bytes.position = 0; check(bytes.readUTFBytes(bytes.length) == "ab" && bytes.position == bytes.length, "utf-bom-nul");
            bytes.length = 0; bytes.writeMultiByte("Příliš", "windows-1250"); bytes.position = 0;
            check(bytes.readMultiByte(bytes.length, "windows-1250") == "Příliš", "windows-1250");
            bytes.length = 0; bytes.writeMultiByte("日本", "shift-jis"); bytes.position = 0;
            check(bytes.readMultiByte(bytes.length, "shift-jis") == "日本", "shift-jis");
            bytes.length = 0; bytes.writeByte(255); bytes.writeByte(254); bytes.writeByte(65); bytes.writeByte(0);
            var previous:uint = bytes.position; check(bytes.toString() == "A" && bytes.position == previous, "utf16-bom-position");
            var longText:String = ""; for (var i:int = 0; i < 17000; i++) longText += "🐕";
            bytes.length = 0; var caught:Boolean; try { bytes.writeUTF(longText); } catch (large:RangeError) { caught = true; }
            check(caught && bytes.length == 0 && bytes.position == 0, "utf-byte-limit");
            bytes.writeUTFBytes(longText); check(bytes.length == 68000, "utfBytes-no-u16-limit");
        }
        private function payload():String { var text:String = ""; for (var i:int = 0; i < 2000; i++) text += "Příliš žluťoučký kůň 🐕\n"; return text; }
        private function compression():void {
            var text:String = payload(); var reference:ByteArray = new ByteArray(); reference.writeUTFBytes(text);
            var bytes:ByteArray = new ZlibText(); bytes.position = 7; bytes.uncompress();
            check(bytes.position == 0 && same(bytes, reference), "java-zlib");
            bytes = new RawText(); bytes.uncompress(CompressionAlgorithm.DEFLATE); check(same(bytes, reference), "java-dynamic-deflate");
            bytes = new StoredText(); bytes.uncompress("deflate"); check(same(bytes, reference), "java-stored-blocks");
            bytes = new FixedText(); bytes.uncompress("deflate"); check(bytes.toString() == "ABC", "fixed-block");
            for each (var algorithm:String in [CompressionAlgorithm.ZLIB, CompressionAlgorithm.DEFLATE]) {
                bytes = new ByteArray(); bytes.endian = Endian.LITTLE_ENDIAN; bytes.writeUTFBytes(text); bytes.position = 5; bytes.compress(algorithm);
                check(bytes.position == bytes.length && bytes.length < 2000, "compress-entire-array");
                bytes.position = 3; bytes.uncompress(algorithm); check(bytes.position == 0 && same(bytes, reference) && bytes.endian == Endian.LITTLE_ENDIAN, "roundtrip-" + algorithm);
                bytes.length = 0; bytes.compress(algorithm); bytes.uncompress(algorithm); check(bytes.length == 0 && bytes.position == 0, "empty-" + algorithm);
            }
            bytes = new CompressedPng(); bytes.uncompress(); image = new Loader(); addChild(image); image.x = 10; image.y = 10;
            image.contentLoaderInfo.addEventListener(Event.COMPLETE, loaded); image.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR, failed);
            image.loadBytes(bytes);
            check(ApplicationDomain.currentDomain.getDefinition("flash.utils.Endian") === Endian, "endian-reflection");
        }
        private function errors():void {
            var bytes:ByteArray = new ByteArray(); bytes.writeByte(7); bytes.position = 0;
            var caught:Boolean; try { bytes.readInt(); } catch (eof:EOFError) { caught = true; }
            check(caught && bytes.position == 0 && bytes.length == 1, "eof-type-atomic-read");
            bytes.length = 0; bytes.writeShort(4); bytes.position = 0; caught = false;
            try { bytes.readUTF(); } catch (utfEof:EOFError) { caught = true; }
            check(caught && bytes.position == 2, "eof-after-utf-prefix");
            caught = false; try { bytes.endian = "middleEndian"; } catch (badEndian:ArgumentError) { caught = true; }
            check(caught && bytes.endian == Endian.BIG_ENDIAN, "invalid-endian");
            caught = false; try { bytes.writeUTF(null); } catch (nullText:TypeError) { caught = true; } check(caught, "null-text");
            var dynamicBytes:Object = bytes; caught = false;
            try { dynamicBytes.writeShort(); } catch (missing:ArgumentError) { caught = true; } check(caught, "missing-argument");
            bytes = new ZlibText(); bytes.length = bytes.length - 1; bytes.position = 3; var before:uint = bytes.length; caught = false;
            try { bytes.uncompress(); } catch (truncated:Error) { caught = true; }
            check(caught && bytes.length == before && bytes.position == 3, "truncated-atomic");
            bytes = new ZlibText(); bytes.position = bytes.length - 1; var last:uint = bytes.readUnsignedByte(); bytes.position--; bytes.writeByte(last ^ 1); bytes.position = 5; caught = false;
            try { bytes.uncompress(); } catch (checksum:Error) { caught = true; } check(caught && bytes.position == 5, "bad-checksum");
            bytes = new RawText(); bytes.length = bytes.length - 1; caught = false;
            try { bytes.uncompress("deflate"); } catch (rawEof:Error) { caught = true; } check(caught, "raw-truncation");
            bytes = new ByteArray(); bytes.writeByte(7); caught = false;
            try { bytes.uncompress("deflate"); } catch (reserved:Error) { caught = true; } check(caught, "reserved-block");
            caught = false; try { bytes.compress(CompressionAlgorithm.LZMA); } catch (lzma:ArgumentError) { caught = true; } check(caught && bytes.length == 1, "unsupported-lzma");
        }
        private function loaded(event:Event):void { imageReady = true; }
        private function failed(event:IOErrorEvent):void { failures++; failureList += event.text; }
        public function afterGC():void {
            retained.position = 0; check(retained.endian == Endian.LITTLE_ENDIAN && retained.readInt() == 0x12345678, "endian-survives-moving-gc");
        }
    }
}
