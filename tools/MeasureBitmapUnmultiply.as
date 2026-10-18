package {
    import flash.display.BitmapData;
    import flash.display.MovieClip;
    import flash.net.URLLoader;
    import flash.net.URLRequest;
    public class MeasureBitmapUnmultiply extends MovieClip {
        public function MeasureBitmapUnmultiply() {
            var samples:Array=[];
            var bitmap:BitmapData=new BitmapData(1,1,true,0);
            // Setting the nearest unmultiplied value produces each valid
            // premultiplied channel. Measure Flash's inverse lookup.
            for(var alpha:int=1;alpha<256;alpha++) {
                var row:Array=[];
                for(var channel:int=0;channel<=alpha;channel++) {
                    bitmap.setPixel32(0,0,(alpha<<24)|(Math.round(channel*255/alpha)<<16));
                    row.push((bitmap.getPixel32(0,0)>>16)&255);
                }
                samples.push(row.join(","));
            }
            bitmap.dispose();
            var request:URLRequest=new URLRequest("/result");
            request.method="POST";request.data=samples.join("\n");
            new URLLoader().load(request);stop();
        }
    }
}
