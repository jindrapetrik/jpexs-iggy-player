package {
    import flash.display.MovieClip;
    import flash.utils.ByteArray;
    import flash.net.registerClassAlias;
    import flash.net.getClassByAlias;
    import libamf.ImportedRecord;
    public class AmfChild extends MovieClip {
        public var passed:Boolean;
        public var retained:Object;
        public function AmfChild() {
            super(); stop(); registerClassAlias("test.Record",ImportedRecord);
            var item:ImportedRecord = new ImportedRecord(); item.value = 91; item.self = item;
            var bytes:ByteArray = new ByteArray(); bytes.writeObject([item,item]); bytes.position = 0;
            retained = bytes.readObject();
            passed = retained[0] is ImportedRecord && retained[0] === retained[1] && retained[0].self === retained[0] && retained[0].value == 91 && getClassByAlias("test.Record") === ImportedRecord;
        }
    }
}
