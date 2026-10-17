package {
    import flash.display.MovieClip;
    import flash.geom.Matrix;
    public class Main extends MovieClip {
        public var clip:MovieClip;
        public var zero:Boolean;
        public var positive:Boolean;
        public var negative:Boolean;
        public var normal:Boolean;
        public var finite:Boolean;
        public var converted:Boolean;
        public var changed:Boolean;
        public var calls:uint;
        public var runs:uint;
        public var normalScale:Number;
        public var normalRotation:Number;
        public function Main() { super(); stop(); clip = new MovieClip(); addChild(clip); run(); }
        private function close(a:Number,b:Number):Boolean { return Math.abs(a-b) < 0.0001; }
        public function run():void {
            runs++;
            clip.transform.matrix = new Matrix(2,0,0,3,12,34);
            clip.scaleY = 0; clip.scaleY = 0;
            zero = clip.scaleY == 0;
            clip.scaleY = 2;
            positive = close(clip.scaleY,2) && close(clip.scaleX,2) && clip.x == 12 && clip.y == 34;
            clip.scaleY = 0; clip.scaleY = -3;
            negative = close(clip.scaleY,-3) && close(clip.scaleX,2);
            clip.transform.matrix = new Matrix(2,0,0,3,12,34);
            clip.rotation = 30; clip.scaleY = 4;
            normalScale = clip.scaleY; normalRotation = clip.rotation;
            normal = close(clip.scaleY,4) && close(clip.rotation,30);
            clip.scaleY = 0; clip.scaleY = NaN;
            finite = clip.scaleY == 0;
            clip.scaleY = 0; clip.scaleY = Infinity;
            finite = finite && clip.scaleY == 0;
            calls = 0;
            var target:Object = clip;
            clip.scaleY = 0; target.scaleY = {valueOf:convert};
            converted = calls == 1 && close(clip.scaleY,-2);
            clip.scaleY = 0; target.scaleY = {valueOf:change};
            changed = calls == 2 && close(clip.scaleY,6);
        }
        private function convert():Number { calls++; return -2; }
        private function change():Number { calls++; clip.transform.matrix = new Matrix(1,0,0,3); return 6; }
    }
}
