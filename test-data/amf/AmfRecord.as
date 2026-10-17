package {
    public dynamic class AmfRecord extends AmfBase {
        public static var created:uint;
        public var title:String = "initial";
        [Transient] public var scratch:int = 9;
        private var amountValue:int;
        public function AmfRecord() { super(); created++; }
        public function get amount():int { return amountValue; }
        public function set amount(value:int):void { amountValue = value; }
        public function get readOnly():String { return "ignored"; }
    }
}
