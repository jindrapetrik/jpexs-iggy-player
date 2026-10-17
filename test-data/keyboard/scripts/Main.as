package {
    import flash.display.MovieClip;
    import flash.display.Sprite;
    import flash.events.KeyboardEvent;
    import flash.events.Event;
    import flash.events.FocusEvent;
    import flash.text.TextField;
    import flash.text.TextFieldType;
    public class Main extends MovieClip {
        public var downs:uint;
        public var ups:uint;
        public var lastCode:uint;
        public var location:uint;
        public var shifted:Boolean;
        public var controlled:Boolean;
        public var alternate:Boolean;
        public var ticks:uint;
        public var input:TextField;
        public var target:Sprite;
        public var holder:Sprite;
        public var targetDowns:uint;
        public var targetFocusOuts:uint;
        public function Main() {
            super();
            stop();
            stage.addEventListener(KeyboardEvent.KEY_DOWN, down);
            stage.addEventListener(KeyboardEvent.KEY_UP, up);
            input = new TextField();
            input.type = TextFieldType.INPUT;
            input.border = true;
            input.width = 300;
            input.height = 60;
            addChild(input);
            stage.focus = input;
            addEventListener(Event.ENTER_FRAME, tick);
        }
        public function get inputText():String { return input.text; }
        public function get focusCleared():Boolean { return stage.focus == null; }
        public function get targetFocused():Boolean { return stage.focus == target; }
        public function focusTarget():void {
            target = new Sprite();
            target.tabEnabled = true;
            target.addEventListener(KeyboardEvent.KEY_DOWN, targetDown);
            target.addEventListener(FocusEvent.FOCUS_OUT, targetOut);
            holder = new Sprite();
            holder.addChild(target);
            addChild(holder);
            stage.focus = target;
        }
        public function detachHolder():void { removeChild(holder); }
        public function reattachHolder():void { addChild(holder); }
        public function detachInput():void { stage.focus = input; removeChild(input); }
        public function restoreInput():void { addChild(input); stage.focus = input; }
        private function targetDown(event:KeyboardEvent):void { targetDowns++; }
        private function targetOut(event:FocusEvent):void { targetFocusOuts++; }
        private function tick(event:Event):void { ticks++; }
        private function record(event:KeyboardEvent):void {
            lastCode = event.keyCode;
            location = event.keyLocation;
            shifted = event.shiftKey;
            controlled = event.ctrlKey;
            alternate = event.altKey;
        }
        private function down(event:KeyboardEvent):void { downs++; record(event); }
        private function up(event:KeyboardEvent):void { ups++; record(event); }
    }
}
