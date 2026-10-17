package {
    import flash.display.MovieClip;
    import flash.display.Sprite;
    import flash.display.SimpleButton;
    import flash.events.Event;
    import flash.ui.Mouse;
    public class Main extends MovieClip {
        public var ordinary:Sprite;
        public var hand:Sprite;
        public var nested:Sprite;
        public var button:SimpleButton;
        public var ticks:uint;
        public function Main() {
            super();stop();
            Mouse.hide();
            ordinary = box(0x00ff00);ordinary.x=20;addChild(ordinary);
            hand = box(0xff0000);hand.x=140;hand.buttonMode=true;addChild(hand);
            nested = new Sprite();nested.x=260;nested.y=20;nested.buttonMode=true;
            var child:Sprite=box(0x0000ff);child.y=0;nested.addChild(child);addChild(nested);
            button = new SimpleButton(box(0xff00ff),box(0xff00ff),box(0xff00ff),box(0xff00ff));
            button.x=380;addChild(button);
            addEventListener(Event.ENTER_FRAME, tick);
        }
        private function box(color:uint):Sprite {
            var result:Sprite=new Sprite();result.y=20;
            result.graphics.beginFill(color);result.graphics.drawRect(0,0,80,60);result.graphics.endFill();return result;
        }
        private function tick(event:Event):void { ticks++; }
        public function removeHand():void { removeChild(hand); }
        public function hideMouse():void { Mouse.hide(); }
        public function showMouse():void { Mouse.show(); }
        public function discard():void {
            var temporary:Sprite = new Sprite();temporary.buttonMode=true;temporary.useHandCursor=false;
        }
    }
}
