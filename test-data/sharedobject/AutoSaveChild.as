package {
    import flash.display.MovieClip;
    import flash.net.SharedObject;
    import flash.net.registerClassAlias;
    public class AutoSaveChild extends MovieClip {
        public var before:uint;
        public var passed:Boolean;
        public function AutoSaveChild() {
            super(); stop(); registerClassAlias("auto.ChildRecord",SavedRecord);
            var store:SharedObject = SharedObject.getLocal("loaded-auto"); before = store.data.counter ? store.data.counter : 0;
            passed = before == 0 || (store.data.record is SavedRecord && store.data.record.owner === store.data);
            var record:SavedRecord = new SavedRecord(); record.owner = store.data; store.data.record = record;
            store.data.counter = before + 1;
        }
    }
}
