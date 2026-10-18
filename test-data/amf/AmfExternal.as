package {
    import flash.utils.IExternalizable;
    import flash.utils.IDataInput;
    import flash.utils.IDataOutput;
    public class AmfExternal implements IExternalizable {
        private var secret:int = 37;
        public var link:Object;
        public var payload:Object;
        public var text:String = "custom";
        public var readCalls:uint;
        public var writeCalls:uint;
        public var failWrite:Boolean;
        public var failRead:Boolean;
        public var retainedInput:IDataInput;
        public var retainedOutput:IDataOutput;
        public function get value():int { return secret; }
        public function set value(next:int):void { secret = next; }
        public function writeExternal(output:IDataOutput):void {
            writeCalls++;
            retainedOutput = output;
            output.writeBoolean(failRead);
            output.endian = "littleEndian"; output.writeInt(secret); output.writeUTF(text);
            if (failWrite) throw new Error("External write failed");
            output.writeObject(link); output.writeObject(payload); output.writeByte(0x7f);
        }
        public function readExternal(input:IDataInput):void {
            readCalls++;
            retainedInput = input;
            if (input.readBoolean()) throw new Error("External read failed");
            input.endian = "littleEndian"; secret = input.readInt(); text = input.readUTF();
            link = input.readObject(); payload = input.readObject();
            if (input.readUnsignedByte() != 0x7f) throw new Error("External stream boundary");
        }
    }
}
