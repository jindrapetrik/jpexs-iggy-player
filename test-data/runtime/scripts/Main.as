package {
    import flash.display.Loader;
    import flash.display.LoaderInfo;
    import flash.display.MovieClip;
    import flash.events.Event;
    import flash.events.IOErrorEvent;
    import flash.media.SoundChannel;
    import flash.net.SharedObject;
    import flash.net.URLRequest;
    public class Main extends MovieClip {
        public var before:uint;
        public var persisted:Boolean;
        public var cleared:Boolean;
        public var sameStore:Boolean;
        public var channelReady:Boolean;
        public var ioErrors:uint;
        public var completes:uint;
        public var errorsAtReturn:uint;
        public var correctEvent:Boolean;
        public var eventTypeOk:Boolean;
        public var eventTextOk:Boolean;
        public var eventTargetOk:Boolean;
        public var eventCountsOk:Boolean;
        public var eventUrlOk:Boolean;
        public var cancelErrors:uint;
        public var loadReturned:Boolean;
        public var infoType:Boolean;
        public var storageFailed:Boolean;
        public var literal:String = "Loader";
        public var picker:FilePicker;
        public var pickerReady:Boolean;
        public var pickerReturned:Boolean;
        public var pickerEvents:uint;
        public var pickerSelects:uint;
        public var pickerEventOk:Boolean;
        public var pickerEventsAtReturn:uint;
        public var loader:Loader;
        private var cancelled:Loader;
        public function Main() {
            super();
            var channel:SoundChannel = new SoundChannel();
            channel.stop();
            channelReady = channel.position == 0 && channel.leftPeak == 0 && channel.rightPeak == 0 && channel.soundTransform.volume == 1;
            var store:SharedObject = SharedObject.getLocal("runtime-test");
            sameStore = store === SharedObject.getLocal("runtime-test", "/");
            before = store.data.counter ? uint(store.data.counter) : 0;
            persisted = before == 0 || (store.data.child.label == "žluťoučký🙂" && store.data.child.list[1] === "x" && store.data.child.list[2] === true && store.data.child.list[3] === null && store.data.child.list[4] === undefined && store.data.child.list[5] == -3.5 && isNaN(store.data.child.list[6]) && store.data.child.list[7] == Infinity && store.data.child.list[8] == -Infinity);
            store.data.counter = before + 1;
            store.data.child = {label:"žluťoučký🙂", list:[1,"x",true,null,undefined,-3.5,NaN,Infinity,-Infinity]};
            try {
                if (store.flush() != "flushed") throw new Error("Flush did not complete");
            } catch (error:Error) { storageFailed = true; return; }
            var empty:SharedObject = SharedObject.getLocal("runtime-clear-test");
            cleared = empty.data.value === undefined;
            empty.data.value = "temporary";
            empty.flush();
            empty.clear();
            loader = new Loader();
            infoType = loader.contentLoaderInfo is LoaderInfo;
            loader.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR, failed);
            loader.contentLoaderInfo.addEventListener(Event.COMPLETE, completed);
            loader.load(new URLRequest("https://example.invalid/first.swf"));
            loader.load(new URLRequest("https://example.invalid/second.swf"));
            errorsAtReturn = ioErrors;
            loadReturned = true;
            cancelled = new Loader();
            cancelled.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR, cancelFailed);
            cancelled.load(new URLRequest("https://example.invalid/cancelled.swf"));
            cancelled.close();
            picker = new FilePicker();
            pickerReady = picker.inherited && picker.fileList.length == 0;
            picker.addEventListener(Event.CANCEL, pickerCancelled);
            picker.addEventListener(Event.SELECT, pickerSelected);
            pickerReturned = picker.browse();
            pickerEventsAtReturn = pickerEvents;
        }
        private function failed(event:IOErrorEvent):void {
            ioErrors++;
            eventTypeOk = event.type == IOErrorEvent.IO_ERROR;
            eventTextOk = event.text.length > 0;
            eventTargetOk = event.target === loader.contentLoaderInfo;
            eventCountsOk = loader.contentLoaderInfo.bytesLoaded == 0 && loader.contentLoaderInfo.bytesTotal == 0;
            eventUrlOk = loader.contentLoaderInfo.url == "https://example.invalid/second.swf";
            correctEvent = loadReturned && eventTypeOk && eventTextOk && eventTargetOk && eventCountsOk && eventUrlOk;
        }
        private function completed(event:Event):void { completes++; }
        private function cancelFailed(event:IOErrorEvent):void { cancelErrors++; }
        private function pickerCancelled(event:Event):void {
            pickerEvents++;
            pickerEventOk = pickerReturned && event.type == Event.CANCEL && event.target === picker && picker.fileList.length == 0;
            if (pickerEvents == 1) pickerReturned = picker.browse();
        }
        private function pickerSelected(event:Event):void { pickerSelects++; }
    }
}
