package {
    import flash.display.BitmapData;
    import flash.geom.Point;
    import flash.geom.Rectangle;
    public class CopyPixelCases {
        private static var output:String;
        private static function record(name:String,b:BitmapData):void {
            output+=name+":";for(var y:int=0;y<b.height;y++)for(var x:int=0;x<b.width;x++)output+=b.getPixel32(x,y).toString(16)+",";output+="\n";
        }
        private static function grid(w:int,h:int,alpha:Boolean=false):BitmapData {
            var b:BitmapData=new BitmapData(w,h,true,0);
            for(var y:int=0;y<h;y++)for(var x:int=0;x<w;x++)b.setPixel32(x,y,((alpha?((x+y)%3)*127:255)<<24)|((x+y*w+1)*0x110907));return b;
        }
        private static function geometry(name:String,rect:Rectangle,point:Point,mask:BitmapData=null,alphaPoint:Point=null):void {
            var s:BitmapData=grid(4,3);var d:BitmapData=new BitmapData(5,4,true,0xff000088);
            d.copyPixels(s,rect,point,mask,alphaPoint);record(name,d);s.dispose();d.dispose();
        }
        public static function run():String {
            output="";var alphas:Array=[0,1,127,128,254,255];
            for(var opaqueSource:int=0;opaqueSource<2;opaqueSource++)for(var opaqueDest:int=0;opaqueDest<2;opaqueDest++)for(var merge:int=0;merge<2;merge++)for(var maskKind:int=0;maskKind<5;maskKind++)for each(var sa:uint in alphas)for each(var da:uint in alphas) {
                var s:BitmapData=new BitmapData(1,1,!opaqueSource,(sa<<24)|0xff0000);var d:BitmapData=new BitmapData(1,1,!opaqueDest,(da<<24)|0xff);
                var mask:BitmapData=maskKind==0?null:new BitmapData(1,1,maskKind!=4,((maskKind==1?0:maskKind==2?128:255)<<24)|0xffffff);
                d.copyPixels(s,s.rect,new Point(0,0),mask,null,Boolean(merge));
                record("blend-"+opaqueSource+opaqueDest+merge+"-"+maskKind+"-"+sa+"-"+da,d);s.dispose();d.dispose();if(mask!=null)mask.dispose();
            }
            geometry("full",new Rectangle(0,0,4,3),new Point(0,0));
            geometry("source-negative",new Rectangle(-2,-1,5,4),new Point(0,0));
            geometry("dest-negative",new Rectangle(0,0,4,3),new Point(-2,-1));
            geometry("source-end",new Rectangle(2,1,9,9),new Point(1,1));
            geometry("dest-end",new Rectangle(0,0,4,3),new Point(3,3));
            geometry("empty",new Rectangle(0,0,-1,2),new Point(0,0));
            geometry("outside",new Rectangle(20,20,3,3),new Point(0,0));
            geometry("fraction-even",new Rectangle(0.5,0.5,1,1),new Point(0.5,0.5));
            geometry("fraction-point",new Rectangle(0,0,2,2),new Point(1.9,-0.9));
            geometry("fraction-rect",new Rectangle(1.5,0.6,1,1),new Point(0,0));
            geometry("huge-point",new Rectangle(0,0,2,2),new Point(1e20,0));
            mask=grid(2,2,true);
            geometry("mask-default",new Rectangle(0,0,4,3),new Point(0,0),mask);
            geometry("mask-negative",new Rectangle(0,0,4,3),new Point(0,0),mask,new Point(-1,-1));
            geometry("mask-offset",new Rectangle(1,1,3,2),new Point(0,0),mask,new Point(1,0));
            geometry("mask-source-clip",new Rectangle(-1,-1,4,3),new Point(-1,0),mask,new Point(-1,0));
            geometry("mask-point-fraction",new Rectangle(0,0,4,3),new Point(0,0),mask,new Point(0.9,0.9));mask.dispose();
            mask=new BitmapData(1,1,false,0);geometry("mask-opaque-bounds",new Rectangle(0,0,4,3),new Point(0,0),mask,new Point(-1,0));mask.dispose();
            for(var alias:int=0;alias<5;alias++) {
                d=grid(4,3,alias>=2);s=alias==3?grid(4,3,true):d;mask=alias>=2?d:null;
                d.copyPixels(s,new Rectangle(alias==1?1:0,alias==1?1:0,3,2),new Point(alias==1?0:1,alias==1?0:1),mask,new Point(0,0),alias==4);
                record("overlap-"+alias,d);if(s!=d)s.dispose();d.dispose();
            }
            s=new BitmapData(1,1,true,0x80ff0000);mask=new BitmapData(1,1,true,0x80000000);d=new BitmapData(1,1,true,0x800000ff);
            d.copyPixels(s,s.rect,new Point(0,0),mask);record("mask-default-merge",d);d.dispose();
            d=new BitmapData(1,1,true,0x800000ff);d.copyPixels(s,s.rect,new Point(0,0),mask,new Point(0,0),false);record("mask-explicit-merge",d);d.dispose();mask.dispose();
            for each(var color:uint in [0x7850a0c0,0x01010101,0xfedcba98,0x7f123456]) {
                s.setPixel32(0,0,color);d=new BitmapData(1,1,true,0x80406080);d.copyPixels(s,s.rect,new Point(0,0),null,null,true);record("color-"+color.toString(16),d);d.dispose();
            }
            d=new BitmapData(1,1);var caught:Boolean=false;try{d.copyPixels(null,s.rect,new Point());}catch(e:Error){caught=true;}output+="null-source:"+caught+"\n";
            caught=false;try{d.copyPixels(s,null,new Point());}catch(e:Error){caught=true;}output+="null-rect:"+caught+"\n";
            caught=false;try{d.copyPixels(s,s.rect,null);}catch(e:Error){caught=true;}output+="null-point:"+caught+"\n";
            mask=new BitmapData(1,1);mask.dispose();caught=false;try{d.copyPixels(s,s.rect,new Point(),mask);}catch(e:Error){caught=true;}output+="disposed-mask:"+caught+"\n";
            s.dispose();caught=false;try{d.copyPixels(s,new Rectangle(0,0,1,1),new Point());}catch(e:Error){caught=true;}output+="disposed-source:"+caught+"\n";
            d.dispose();caught=false;try{d.copyPixels(d,new Rectangle(0,0,1,1),new Point());}catch(e:Error){caught=true;}output+="disposed-dest:"+caught+"\n";
            return output;
        }
    }
}
