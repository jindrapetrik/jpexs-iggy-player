package {
    import flash.display.MovieClip;
    import flash.display.Bitmap;
    import flash.display.BitmapData;
    import flash.display.Loader;
    import flash.geom.Rectangle;
    import flash.geom.Point;
    import flash.net.URLRequest;
    import flash.events.Event;
    import flash.utils.ByteArray;
    import flash.utils.Endian;
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
        public var pixelBytes:ByteArray;
        public var copyResults:String;
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
        public function bulk():void {
            var bitmap:BitmapData=new BitmapData(3,2,true,0xff112233);bitmap.setPixel32(1,0,0x80ff0000);
            var bytes:ByteArray=bitmap.getPixels(bitmap.rect);
            check(bytes.length==24 && bytes.position==24 && bytes.endian==Endian.BIG_ENDIAN,"bulk-result-state");
            bytes.position=0;check(bytes.readUnsignedInt()==0xff112233 && bytes.readUnsignedInt()==0x80ff0000,"bulk-argb-alpha");
            var output:ByteArray=bitmap.getPixels(new Rectangle(-1,-1,3,3));check(output.length==16 && output.position==16,"bulk-get-clipped");
            check(bitmap.getPixels(new Rectangle(0.5,0.5,1,1)).length==16 && bitmap.getPixels(new Rectangle(1.5,0,1,1)).length==0,"bulk-round-even");
            check(bitmap.getPixels(new Rectangle(0.6,0.6,1,1)).length==4 && bitmap.getPixels(new Rectangle(9,9,1,1)).length==0 && bitmap.getPixels(new Rectangle(0,0,-1,1)).length==0,"bulk-empty-fractional");
            for each(var endian:String in [Endian.BIG_ENDIAN,Endian.LITTLE_ENDIAN]) {
                bytes=new ByteArray();bytes.endian=endian;bytes.writeByte(42);bytes.writeUnsignedInt(0xff3366cc);bytes.writeUnsignedInt(0x8000ff00);bytes.position=1;
                bitmap.setPixels(new Rectangle(0,0,2,1),bytes);
                check(bytes.position==9 && bytes.endian==endian && bitmap.getPixel32(0,0)==0xff3366cc && bitmap.getPixel32(1,0)==0x8000ff00,"bulk-input-"+endian);
            }
            bytes=new ByteArray();for(var i:int=0;i<6;i++)bytes.writeUnsignedInt(0xff000001+i);bytes.position=0;
            bitmap.setPixels(new Rectangle(-1,-1,3,3),bytes);check(bytes.position==16 && bitmap.getPixel32(0,0)==0xff000001 && bitmap.getPixel32(1,0)==0xff000002 && bitmap.getPixel32(0,1)==0xff000003,"bulk-set-clipped");
            for each(var size:int in [0,1,3,4,5,7]) {
                bitmap.fillRect(bitmap.rect,0xff000000);bytes=new ByteArray();for(i=0;i<size;i++)bytes.writeByte(255);bytes.position=0;
                var rejected:Boolean=false;try{bitmap.setPixels(bitmap.rect,bytes);}catch(error:Error){rejected=true;}
                check(rejected && bytes.position==(size>=4?4:0) && bitmap.getPixel32(0,0)==(size>=4?0xffffffff:0xff000000) && bitmap.getPixel32(1,0)==0xff000000,"bulk-short-"+size);
            }
            bytes.position=100;bitmap.setPixels(new Rectangle(8,8,1,1),bytes);check(bytes.position==100,"bulk-empty-no-read");
            rejected=false;try{bitmap.setPixels(new Rectangle(0,0,1,1),bytes);}catch(error:Error){rejected=true;}check(rejected && bytes.position==100,"bulk-past-end");
            bytes=new ByteArray();bytes.writeUnsignedInt(0x00123456);bytes.position=0;bitmap.setPixels(new Rectangle(0,0,1,1),bytes);check(bitmap.getPixel32(0,0)==0,"bulk-zero-alpha");
            var solid:BitmapData=new BitmapData(1,1,false);bytes.position=0;solid.setPixels(solid.rect,bytes);check(solid.getPixel32(0,0)==0xff123456,"bulk-opaque");solid.dispose();
            bytes=new PixelBytes();bitmap.setPixels(new Rectangle(0,0,1,1),bytes);check(bytes.position==4 && bitmap.getPixel32(0,0)==0xff445566,"bulk-embedded-bytearray");
            rejected=false;try{bitmap.getPixels(null);}catch(error:TypeError){rejected=true;}check(rejected,"bulk-null-rect");
            rejected=false;try{bitmap.setPixels(bitmap.rect,null);}catch(error:TypeError){rejected=true;}check(rejected,"bulk-null-input");
            bytes=assetCopy.getPixels(new Rectangle(0,0,1,1));bytes.position=0;asset.setPixels(new Rectangle(0,0,1,1),bytes);check(asset.getPixel32(0,0)==0xffff0000,"bulk-embedded");
            bytes=loadedCopy.getPixels(new Rectangle(3,10,1,1));bytes.position=0;Bitmap(loader.content).bitmapData.setPixels(new Rectangle(3,10,1,1),bytes);check(Bitmap(loader.content).bitmapData.getPixel32(3,10)==0xffff0000,"bulk-loaded");
            ByteArray.defaultObjectEncoding=0;bytes=bitmap.getPixels(bitmap.rect);check(bytes.objectEncoding==0,"bulk-default-encoding");ByteArray.defaultObjectEncoding=3;
            bitmap.dispose();rejected=false;try{bitmap.getPixels(new Rectangle(0,0,1,1));}catch(error:ArgumentError){rejected=true;}check(rejected,"bulk-disposed");
            bytes=new ByteArray();for(i=0;i<48;i++)bytes.writeUnsignedInt(0xff00ffff);bytes.position=0;data.setPixels(data.rect,bytes);check(bytes.position==192,"bulk-visible-write");
            pixelBytes=data.getPixels(data.rect);
            bytes=new ByteArray();bytes.writeUnsignedInt(0xffff0000);bytes.writeByte(1);bytes.position=0;
            rejected=false;try{copy.setPixels(new Rectangle(0,0,2,1),bytes);}catch(error:Error){rejected=true;}check(rejected && bytes.position==4,"bulk-visible-partial");
        }
        public function bulkRetained():void {pixelBytes.position=0;check(pixelBytes.length==192 && pixelBytes.readUnsignedInt()==0xff00ffff,"bulk-survives-gc");}
        public function copies():void {
            copyResults=CopyPixelCases.run();
            data.copyPixels(assetCopy,assetCopy.rect,new Point(0,0));check(data.getPixel32(0,0)==0xffff0000,"copy-embedded");
            var source:BitmapData=new BitmapData(2,2,true,0x8000ff00);var mask:BitmapData=new BitmapData(2,2,true,0x80000000);
            copy.copyPixels(source,source.rect,new Point(0,0),mask,new Point(0,0),false);check(copy.getPixel32(0,0)==0x4000ff00,"copy-visible-mask");source.dispose();mask.dispose();
            asset.copyPixels(Bitmap(loader.content).bitmapData,new Rectangle(3,10,1,1),new Point(1,1));check(asset.getPixel32(1,1)==0xffff0000,"copy-loaded");
        }
        public function drop():void {loader.unload();removeChild(loader);loader=null;for each(var bitmap:Bitmap in views)removeChild(bitmap);views=[];data=null;copy=null;opaque=null;asset=null;assetCopy=null;loadedCopy=null;pixelBytes=null;}
    }
}
