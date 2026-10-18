package {
    import flash.display.MovieClip;
    import flash.display.Bitmap;
    import flash.display.BitmapData;
    import flash.display.Loader;
    import flash.geom.Rectangle;
    import flash.net.URLRequest;
    import flash.events.Event;
    public class BitmapDataMain extends MovieClip {
        public var failures:uint;
        public var details:String="";
        public var loaded:Boolean;
        public var data:BitmapData;
        public var copy:BitmapData;
        public var opaque:BitmapData;
        public var asset:BitmapData;
        public var assetCopy:BitmapData;
        public var loader:Loader;
        public var loadedCopy:BitmapData;
        public var conversions:uint;
        private var views:Array=[];
        public function BitmapDataMain() {
            super();stop();data=new BitmapData(8,6,true,0xff0000ff);
            check(data.rect.x==0 && data.rect.y==0 && data.rect.width==8 && data.rect.height==6,"rect");
            data.fillRect(new Rectangle(-2,-1,4,3),0x80ff0000);
            check(data.getPixel32(0,0)==0x80ff0000 && data.getPixel32(2,0)==0xff0000ff,"clipped-fill");
            data.setPixel32(2,2,0x800000ff);data.setPixel(2,2,0x00ff00);
            check(data.getPixel32(2,2)==0x8000ff00 && data.getPixel(2,2)==0x00ff00,"setPixel-alpha");
            data.setPixel32(3,3,0x00123456);check(data.getPixel32(3,3)==0 && data.getPixel(3,3)==0,"zero-alpha");
            data.setPixel32(-1,0,0xffffffff);check(data.getPixel32(-1,0)==0 && data.getPixel(8,0)==0,"outside");
            data.fillRect(new Rectangle(2,2,-4,2),0);data.fillRect(new Rectangle(2,2,0,2),0);check(data.getPixel32(2,2)==0x8000ff00,"empty-fill");
            copy=data.clone();copy.setPixel32(0,0,0xffffff00);check(data.getPixel32(0,0)==0x80ff0000 && copy.getPixel32(0,0)==0xffffff00,"independent-clone");
            opaque=new BitmapData(4,4,false,0x00112233);opaque.setPixel32(0,0,0x0000ff00);check(!opaque.transparent && opaque.getPixel32(0,0)==0xff00ff00,"opaque-alpha");
            opaque.fillRect(opaque.rect,0x00ff00ff);check(opaque.getPixel32(0,0)==0xffff00ff,"opaque-fill");
            asset=new EmbeddedPixels(4,4);assetCopy=asset.clone();asset.setPixel32(0,0,0xff00ffff);check(assetCopy.getPixel32(0,0)==0xffff0000,"embedded-private-clone");
            var other:BitmapData=new EmbeddedPixels(4,4);check(other.getPixel32(0,0)==0xffff0000,"embedded-private-instance");other.dispose();
            var nullRejected:Boolean;try{data.fillRect(null,0);}catch(error:Error){nullRejected=true;}check(nullRejected,"null-rectangle");
            var disposed:BitmapData=new BitmapData(1,1);disposed.dispose();var rejected:Boolean;try{disposed.getPixel32(0,0);}catch(error:Error){rejected=true;}check(rejected,"disposed-error");
            place(data,10,10,8);place(copy,90,10,8);place(opaque,170,10,12);place(asset,230,10,12);place(assetCopy,290,10,12);
            loader=new Loader();loader.x=360;loader.y=10;addChild(loader);loader.contentLoaderInfo.addEventListener(Event.COMPLETE,function(event:Event):void {
                var image:BitmapData=Bitmap(loader.content).bitmapData;check(image.getPixel32(3,10)==0xffff0000,"loaded-get");
                loadedCopy=image.clone();image.setPixel32(3,10,0xff00ffff);check(loadedCopy.getPixel32(3,10)==0xffff0000,"loaded-private-clone");place(loadedCopy,410,10,1);loaded=true;
            });loader.load(new URLRequest("loader/colors.png"));
        }
        private function check(value:Boolean,label:String):void {if(!value){failures++;details+=label+";";}}
        private function place(value:BitmapData,x:int,y:int,scale:int):void {var bitmap:Bitmap=new Bitmap(value);bitmap.x=x;bitmap.y=y;bitmap.scaleX=scale;bitmap.scaleY=scale;views.push(bitmap);addChild(bitmap);}
        public function change():void {data.fillRect(data.rect,0xff00ff00);copy.setPixel32(0,0,0x800000ff);opaque.fillRect(opaque.rect,0x00ffff00);asset.fillRect(asset.rect,0xff0000ff);}
        public function disposeDisplayed():void {copy.dispose();var rejected:Boolean;try{copy.setPixel(0,0,0);}catch(error:Error){rejected=true;}check(rejected,"disposed-set");}
        public function convert():void {
            var x:Object={valueOf:function():Number {conversions++;return 1;}};data.setPixel(x,x,0xffff0000);check(conversions==2 && data.getPixel32(1,1)==0xffff0000,"argument-conversion");
            var doomed:BitmapData=new BitmapData(1,1);var color:Object={valueOf:function():Number {doomed.dispose();return 0;}};
            var rejected:Boolean;try{doomed.setPixel32(0,0,color);}catch(error:Error){rejected=true;}check(rejected,"disposed-during-conversion");
        }
        public function drop():void {loader.unload();removeChild(loader);loader=null;for each(var bitmap:Bitmap in views)removeChild(bitmap);views=[];data=null;copy=null;opaque=null;asset=null;assetCopy=null;loadedCopy=null;}
    }
}
