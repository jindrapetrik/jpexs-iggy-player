package {
    import flash.display.MovieClip;
    import flash.events.KeyboardEvent;
    import flash.events.Event;
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
