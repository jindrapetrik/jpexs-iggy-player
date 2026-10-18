package flash.utils {
    public interface IDataInput {
        function get bytesAvailable():uint;
        function get endian():String;
        function set endian(value:String):void;
        function get objectEncoding():uint;
        function set objectEncoding(value:uint):void;
        function readBoolean():Boolean;
        function readByte():int;
        function readUnsignedByte():uint;
        function readShort():int;
        function readUnsignedShort():uint;
        function readInt():int;
        function readUnsignedInt():uint;
        function readFloat():Number;
        function readDouble():Number;
        function readUTF():String;
        function readUTFBytes(length:uint):String;
        function readMultiByte(length:uint, charset:String):String;
        function readBytes(bytes:ByteArray, offset:uint = 0, length:uint = 0):void;
        function readObject():*;
    }
}
