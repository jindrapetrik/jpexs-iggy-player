package com.jpexs.iggy.runtime {
    import flash.utils.ByteArray;
    import flash.utils.IDataInput;
    import flash.utils.IDataOutput;
    // Externalizers share the enclosing codec's reference tables, but cannot
    // retain a live stream after their callback returns.
    public class HostAmfIO implements IDataInput, IDataOutput {
        private var codec:Object;
        private var data:ByteArray;
        private var depth:uint;
        private var writing:Boolean;
        private var encoding:uint = 3;
        public function HostAmfIO(owner:Object, bytes:ByteArray, level:uint, output:Boolean) { codec = owner; data = bytes; depth = level; writing = output; }
        public function close():void { codec = null; data = null; }
        private function ready(output:Boolean):void { if (data == null || output != writing) throw new Error("Inactive AMF external stream"); codec.checkSize(); }
        public function get bytesAvailable():uint { ready(false); return data.bytesAvailable; }
        public function get endian():String { if (data == null) throw new Error("Inactive AMF external stream"); return data.endian; }
        public function set endian(value:String):void { if (data == null) throw new Error("Inactive AMF external stream"); data.endian = value; }
        public function get objectEncoding():uint { return encoding; }
        public function set objectEncoding(value:uint):void { if (value != 0 && value != 3) throw new ArgumentError("Invalid AMF encoding"); encoding = value; }
        public function readBoolean():Boolean { ready(false); return data.readBoolean(); }
        public function readByte():int { ready(false); return data.readByte(); }
        public function readUnsignedByte():uint { ready(false); return data.readUnsignedByte(); }
        public function readShort():int { ready(false); return data.readShort(); }
        public function readUnsignedShort():uint { ready(false); return data.readUnsignedShort(); }
        public function readInt():int { ready(false); return data.readInt(); }
        public function readUnsignedInt():uint { ready(false); return data.readUnsignedInt(); }
        public function readFloat():Number { ready(false); return data.readFloat(); }
        public function readDouble():Number { ready(false); return data.readDouble(); }
        public function readUTF():String { ready(false); return data.readUTF(); }
        public function readUTFBytes(length:uint):String { ready(false); return data.readUTFBytes(length); }
        public function readMultiByte(length:uint, charset:String):String { ready(false); return data.readMultiByte(length, charset); }
        public function readBytes(bytes:ByteArray, offset:uint = 0, length:uint = 0):void { ready(false); data.readBytes(bytes, offset, length); }
        public function readObject():* { ready(false); return codec.readNested(depth, encoding); }
        public function writeBoolean(value:Boolean):void { ready(true); data.writeBoolean(value); }
        public function writeByte(value:int):void { ready(true); data.writeByte(value); }
        public function writeShort(value:int):void { ready(true); data.writeShort(value); }
        public function writeInt(value:int):void { ready(true); data.writeInt(value); }
        public function writeUnsignedInt(value:uint):void { ready(true); data.writeUnsignedInt(value); }
        public function writeFloat(value:Number):void { ready(true); data.writeFloat(value); }
        public function writeDouble(value:Number):void { ready(true); data.writeDouble(value); }
        public function writeUTF(value:String):void { ready(true); data.writeUTF(value); codec.checkSize(); }
        public function writeUTFBytes(value:String):void { ready(true); data.writeUTFBytes(value); codec.checkSize(); }
        public function writeMultiByte(value:String, charset:String):void { ready(true); data.writeMultiByte(value, charset); codec.checkSize(); }
        public function writeBytes(bytes:ByteArray, offset:uint = 0, length:uint = 0):void { ready(true); data.writeBytes(bytes, offset, length); codec.checkSize(); }
        public function writeObject(value:*):void { ready(true); codec.writeNested(value, depth, encoding); }
    }
}
