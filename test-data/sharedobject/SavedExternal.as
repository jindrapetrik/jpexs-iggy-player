package {
    import flash.utils.IExternalizable;
    import flash.utils.IDataInput;
    import flash.utils.IDataOutput;
    public class SavedExternal implements IExternalizable {
        private var number:int = 314;
        public var shared:Object;
        public var self:Object;
        public function get value():int { return number; }
        public function writeExternal(output:IDataOutput):void { output.writeInt(number); output.writeObject(shared); output.writeObject(self); }
        public function readExternal(input:IDataInput):void { number = input.readInt(); shared = input.readObject(); self = input.readObject(); }
    }
}
