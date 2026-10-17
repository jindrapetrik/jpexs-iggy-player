using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace IggyViewer {
    internal static partial class SelfTest {
        static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException("TEST FAILED: "+message);}
        static string Hash(byte[] bytes){using(SHA256 sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes));}
        static void Reject(Action action,string label){try{action();}catch(InvalidDataException){return;}catch(NotSupportedException){return;}throw new InvalidOperationException("Expected rejection: "+label);}
        static unsafe int Different(Bitmap a,Bitmap b) {
            Check(a.Size==b.Size,"Compared images must have the same dimensions");
            Rectangle area=new Rectangle(0,0,a.Width,a.Height);
            BitmapData first=a.LockBits(area,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try {
                BitmapData second=b.LockBits(area,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
                try {
                    int count=0;
                    for(int y=0;y<a.Height;y++) {
                        int* p=(int*)((byte*)first.Scan0+y*first.Stride),q=(int*)((byte*)second.Scan0+y*second.Stride);
                        for(int x=0;x<a.Width;x++)if(p[x]!=q[x])count++;
                    }
                    return count;
                } finally {b.UnlockBits(second);}
            } finally {a.UnlockBits(first);}
        }
        static int RedPixels(Bitmap image,Rectangle region) {
            int count=0;
            for(int y=region.Top;y<region.Bottom;y++)for(int x=region.Left;x<region.Right;x++) {
                Color color=image.GetPixel(x,y);if(color.R>200&&color.G<60&&color.B<60)count++;
            }
            return count;
        }
        static void CheckEditableText(Bitmap image,Log log) {
            // These six fields are red in the original graphics.swf. Their
            // black static captions cannot satisfy this visibility check.
            Rectangle[] fields={new Rectangle(65,121,142,75),new Rectangle(65,215,142,74),new Rectangle(442,121,301,75),new Rectangle(442,209,180,74),new Rectangle(28,390,264,75),new Rectangle(452,299,275,230)};
            for(int i=0;i<fields.Length;i++)Check(RedPixels(image,fields[i])>200,"Visible editable text field "+i+" in frame 66");
            Check(RedPixels(image,new Rectangle(450,531,280,20))==0,"Multiline text must be clipped to its field");
            log.Write("PASS: all six editable text fields visible in frame 66; multiline clipping");
        }
        static void CheckColorEffect(Bitmap image,Log log) {
            // Reference colors sampled independently from graphics.swf frame 49.
            // Its CXFORM adds (30,40,255,20), including a nonzero alpha term.
            Color rectangle=Color.FromArgb(35,219,255),circle=Color.FromArgb(234,45,255),outline=Color.FromArgb(35,45,255);
            Check(image.GetPixel(350,200).ToArgb()==rectangle.ToArgb(),"Frame 49 rectangle color must include RGB and alpha additions");
            Check(image.GetPixel(530,330).ToArgb()==circle.ToArgb(),"Frame 49 circle color must include RGB and alpha additions");
            Check(image.GetPixel(500,280).ToArgb()==circle.ToArgb(),"Frame 49 overlapping circle must retain its transformed color");
            int matchingOutline=0;
            for(int y=175;y<220;y++)for(int x=285;x<290;x++)if(image.GetPixel(x,y).ToArgb()==outline.ToArgb())matchingOutline++;
            Check(matchingOutline>30,"Frame 49 black outline must also receive the color addition");
            Check(image.GetPixel(750,200).ToArgb()==Color.White.ToArgb(),"Frame 49 background must remain white");
            log.Write("PASS: frame 49 color effect matches SWF RGB values: rectangle 35/219/255, circle 234/45/255, outline 35/45/255");
        }
        static void CheckRadialOrigin(Bitmap image,Log log) {
            // graphics.swf shape 27: yellow -> red, repeat spread, origin
            // (4980,3500) twips = (249,175) pixels. The first palette color
            // must stay yellow, rather than blending with its red far end.
            int cx=(int)(249.0*image.Width/500),cy=(int)(175.0*image.Height/400);
            for(int y=cy-1;y<=cy+1;y++)for(int x=cx-1;x<=cx+1;x++) {
                Color color=image.GetPixel(x,y);
                Check(color.R==255&&color.G>=250&&color.B==0,"Frame 11 radial origin must have no dark point at "+x+","+y);
            }
            // Across this inner radius green should decrease outwards. An
            // origin mixed with the last palette entry violates that ordering.
            int previous=255;
            for(int step=0;step<=10;step++) {
                int x=cx+(int)(step*5.0*image.Width/500);
                int green=image.GetPixel(x,cy).G;
                Check(green<=previous+1,"Frame 11 radial gradient must darken away from its yellow origin");
                previous=green;
            }
            log.Write("PASS: frame 11 yellow radial origin without a dark point; monotonic falloff at "+image.Width+"x"+image.Height);
        }
        static void CheckHelloWorld(Native native,Bitmap image,Log log) {
            Check(native.StageWidth==550&&native.StageHeight==400,"AS3 fixture stage");
            Check(native.Renderer.Triangles>100,"AS3 TextField must produce native glyph geometry");
            int hello=0,world=0,outside=0;
            for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++) {
                Color color=image.GetPixel(x,y);if(color.R>=100||color.G>=100||color.B>=100)continue;
                double sx=x*550.0/image.Width,sy=y*400.0/image.Height;
                if(sx>=100&&sx<=132&&sy>=50&&sy<=70)hello++;
                else if(sx>132&&sx<=170&&sy>=50&&sy<=70)world++;
                else outside++;
            }
            Check(hello>30&&world>30,"Both words in the AS3-created Hello world! field must be visible");
            Check(outside==0,"AS3 text must stay at its scripted position");
            log.Write("PASS: AS3 Hello world! visible at "+image.Width+"x"+image.Height+", triangles="+native.Renderer.Triangles);
        }
        public static void RunAs3(string dll,string movie,string output,Log log) {
            Directory.CreateDirectory(output);string hash=Hash(File.ReadAllBytes(movie));
            using(Native native=new Native(dll,log)) {
                for(int cycle=0;cycle<3;cycle++) {
                    native.Open(movie,1100,800);Check(native.Frame(false),"AS3 first frame");
                    using(Bitmap image=native.Renderer.Snapshot()) {
                        CheckHelloWorld(native,image,log);
                        image.Save(Path.Combine(output,"hello-"+cycle+".png"),ImageFormat.Png);
                        GC.Collect();GC.WaitForPendingFinalizers();native.Frame(false);
                        using(Bitmap repeated=native.Renderer.Snapshot())Check(Different(image,repeated)==0,"AS3 text redraw and borrowed font buffer lifetime");
                    }
                    native.Resize(550,400);native.Frame(false);
                    using(Bitmap resized=native.Renderer.Snapshot())CheckHelloWorld(native,resized,log);
                    native.Seek(1);native.Frame(false);
                    using(Bitmap sought=native.Renderer.Snapshot())CheckHelloWorld(native,sought,log);
                    // Switch to a movie with embedded Times New Roman between
                    // device-font runs, exercising cache flush and font priority.
                    string graphics=Path.Combine(Path.GetDirectoryName(movie),"graphics.iggy");
                    native.Open(graphics,960,640);native.Seek(66);Check(native.Frame(false),"Embedded-font movie after device-font movie");
                    using(Bitmap embedded=native.Renderer.Snapshot())CheckEditableText(embedded,log);
                    native.Close();
                }
                native.Dispose();native.Dispose();
            }
            foreach(string message in log.Messages)Check(message.IndexOf("Tried to use font",StringComparison.Ordinal)<0,"No missing device font warnings");
            Check(Hash(File.ReadAllBytes(movie))==hash,"AS3 fixture changed on disk");
            log.Write("PASS: native AS3 text, resize, seek, redraw, GC, device/embedded font switching, reopen and repeated Dispose; source unchanged");
        }
        static Color StagePixel(Bitmap image,int x,int y) {return image.GetPixel(x*image.Width/500,y*image.Height/400);}
        static void NearColor(Color actual,Color expected,int tolerance,string label) {
            Check(Math.Abs(actual.R-expected.R)<=tolerance&&Math.Abs(actual.G-expected.G)<=tolerance&&Math.Abs(actual.B-expected.B)<=tolerance,label+": "+actual+", expected "+expected);
        }
        static void CheckFilters(Bitmap image,Log log) {
            // Independent reference: the filter flags and palette stops in
            // graphics.swf frame 51, rather than the DLL's rendered result.
            NearColor(StagePixel(image,425,50),Color.Cyan,0,"Frame 51 color matrix");
            foreach(int x in new int[]{60,240})NearColor(StagePixel(image,x,50),Color.Lime,0,"Frame 51 retained shadow source");
            NearColor(StagePixel(image,150,50),Color.White,0,"Frame 51 shadow knockout");
            NearColor(StagePixel(image,235,165),Color.White,0,"Frame 51 glow knockout");
            NearColor(StagePixel(image,145,270),Color.White,0,"Frame 51 bevel knockout");
            NearColor(StagePixel(image,325,270),Color.Lime,0,"Frame 51 full bevel source");
            NearColor(StagePixel(image,425,270),Color.Lime,0,"Frame 51 full gradient bevel source");
            Color highlight=StagePixel(image,390,248),shadow=StagePixel(image,463,270);
            Check(highlight.R>200&&highlight.B>170&&highlight.G<100,"Frame 51 gradient bevel magenta highlight");
            Check(shadow.R>170&&shadow.G<120&&shadow.B<120,"Frame 51 gradient bevel red shadow");
            log.Write("PASS: frame 51 color matrix, full bevel/gradient bevel source, signed gradient palette and knockout at "+image.Width+"x"+image.Height);
        }
        public static void RunFilters(string dll,string movie,string output,Log log) {
            Directory.CreateDirectory(output);string hash=Hash(File.ReadAllBytes(movie));
            using(Native native=new Native(dll,log)) {
                native.Open(movie,500,400);
                foreach(int width in new int[]{500,1000}) {
                    native.Resize(width,width*4/5);Check(native.Frame(false),"Filter fixture rendering");
                    using(Bitmap image=native.Renderer.Snapshot()) {
                        // RGBA(64,128,192,128), channels B+10/R+20/G+30,
                        // alpha=A/2+40; independently evaluated over white.
                        NearColor(StagePixel(image,50,50),Color.FromArgb(233,185,215),2,"Color matrix RGB offsets and alpha");
                        NearColor(StagePixel(image,170,50),Color.FromArgb(185,215,233),2,"Chained color matrices on translucent source");
                        NearColor(StagePixel(image,20,165),Color.White,0,"Single blur pass has no tail ten pixels outside");
                        Color tail=StagePixel(image,140,165);
                        Check(tail.G==255&&tail.R<250&&tail.B<250,"Three blur passes extend ten pixels outside");
                        // Both gradient stops are half-transparent blue. The
                        // palette supplies alpha, independently of blur alpha.
                        NearColor(StagePixel(image,29,275),Color.FromArgb(127,127,255),1,"Gradient filter must not multiply palette coverage twice");
                        NearColor(StagePixel(image,50,275),Color.White,0,"Gradient glow knockout retains transparent source interior");
                        image.Save(Path.Combine(output,"filters-"+width+".png"),ImageFormat.Png);
                        native.Frame(false);using(Bitmap repeated=native.Renderer.Snapshot())Check(Different(image,repeated)==0,"Stable filter redraw");
                    }
                }
            }
            Check(Hash(File.ReadAllBytes(movie))==hash,"Filter fixture changed on disk");
            log.Write("PASS: RGBA matrix offsets, alpha, filter chains, blur quality/bounds, gradient alpha, knockout, resize and redraw");
        }
        static void CheckGradientSpread(Bitmap image,int frame,Log log) {
            // SWF shape 25: repeat linear, period 106 px, first reset at x=155.
            // Shapes 29/31: reflect/repeat radial, radius 72.125 px at (249,175).
            double boundary=frame==10?155:249+72.125;
            int left=(int)Math.Ceiling(boundary*image.Width/500-0.5)-1,right=left+1;
            int y=(int)(175.0*image.Height/400);
            Check(image.GetPixel(left,y).G<=8,"Frame "+frame+" gradient end must stay red up to its boundary");
            if(frame!=12) {
                Check(image.GetPixel(right,y).G>=248,"Frame "+frame+" repeat must restart at yellow without a blended seam");
            } else {
                Check(image.GetPixel(right,y).G<=8,"Reflect must turn back at red rather than restart at yellow");
                int secondPeak=(int)((249+2*72.125)*image.Width/500);
                Check(image.GetPixel(secondPeak,y).G>=248,"Reflect must return to yellow after two radii");
            }
            log.Write("PASS: frame "+frame+" gradient spread boundary retains the correct endpoint colors");
        }
        static void CheckScalingGrid(Bitmap image,Log log) {
            // Centres are taken from graphics.swf, in the 500x400 stage.
            // Each of its four panels has two separate magenta interiors.
            foreach(int y in new int[]{100,250})foreach(int x in new int[]{100,150,300,390}) {
                Color color=image.GetPixel((int)Math.Round(x*image.Width/500.0),(int)Math.Round(y*image.Height/400.0));
                Check(color.ToArgb()==Color.Magenta.ToArgb(),"Frame 47 scaling-grid interior at "+x+","+y);
            }
            int inside=0,outside=0;
            for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++) {
                Color color=image.GetPixel(x,y);
                if(color.R<250||color.G>5||color.B<250)continue;
                double sx=x*500.0/image.Width,sy=y*400.0/image.Height;
                bool inPanel=(sx>=50&&sx<=200||sx>=225&&sx<=475)&&(sy>=50&&sy<=150||sy>=175&&sy<=325);
                if(inPanel)inside++;else outside++;
            }
            Check(inside>image.Width*image.Height/100,"Frame 47 magenta interiors must remain visible");
            Check(outside==0,"Frame 47 must not contain displaced magenta geometry outside its panels");
            log.Write("PASS: frame 47 all eight magenta scaling-grid interiors in position; no displaced magenta pixels");
        }
        static void CheckFocalGradients(Log log) {
            int count=0;
            foreach(float focus in new float[]{-0.999f,-0.75f,-0.25f,0,0.25f,0.75f,0.999f}) {
                float inverse=1/(1-focus*focus),coefficient=-focus*inverse;
                Check(SoftwareRenderer.FocalDistance(focus,0,-focus,coefficient,inverse)==0,"Gradient origin must be the focus");
                for(int angle=0;angle<24;angle++)foreach(float radius in new float[]{0.1f,0.5f,1,2}) {
                    float x=(float)(radius*Math.Cos(angle*Math.PI/12)),y=(float)(radius*Math.Sin(angle*Math.PI/12));
                    // Independent reference: intersect the ray from the focus
                    // through the sample with the unit circle; t is 1/lambda.
                    double dx=(double)x-focus,dy=y,a=dx*dx+dy*dy,b=2*focus*dx,c=(double)focus*focus-1;
                    double lambda=(-b+Math.Sqrt(b*b-4*a*c))/(2*a),expected=1/lambda;
                    float actual=SoftwareRenderer.FocalDistance(x,y,-focus,coefficient,inverse);
                    Check(Math.Abs(actual-expected)<0.0001*Math.Max(1,expected),"Focal ray/circle intersection, focus="+focus+", x="+x+", y="+y);
                    count++;
                }
            }
            log.Write("PASS: focal gradient focus and "+count+" independent ray/circle checks");
        }
        static void CheckFocalMorphMath(Log log) {
            FocalMorph morph=new FocalMorph {StartFocus=-0.75f,EndFocus=0.5f};
            float[] start={0.004f,-0.003f,0.002f,0.005f},end={-0.02f,-0.12f,0.13f,-0.03f};
            Array.Copy(start,morph.Start,4);Array.Copy(end,morph.End,4);
            foreach(float ratio in new float[]{0,1f/3,2f/3,1}) {
                double[] correction;Check(morph.TryTransform(ratio,out correction),"Invertible focal morph");
                double[] n=new double[4],d=new double[4];
                for(int i=0;i<4;i++){n[i]=start[i]+((double)end[i]-start[i])*ratio;d[i]=start[i]+((double)end[i]/20-start[i])*ratio;}
                double det=n[0]*n[3]-n[1]*n[2],desiredDet=d[0]*d[3]-d[1]*d[2];
                foreach(double x in new double[]{-300,0,120})foreach(double y in new double[]{-70,0,500}) {
                    double u=(n[3]*x-n[1]*y)/det,v=(n[0]*y-n[2]*x)/det;
                    double actualU=correction[0]*u+correction[1]*v,actualV=correction[2]*u+correction[3]*v;
                    double expectedU=(d[3]*x-d[1]*y)/desiredDet,expectedV=(d[0]*y-d[2]*x)/desiredDet;
                    Check(Math.Abs(actualU-expectedU)<1e-8*Math.Max(1,Math.Abs(expectedU))&&Math.Abs(actualV-expectedV)<1e-8*Math.Max(1,Math.Abs(expectedV)),"Focal morph matrix compensation");
                }
                Check(Math.Abs(morph.Focus(ratio)-(-0.75+1.25*ratio)*0.999)<1e-6,"Interpolated signed focal point");
                morph.AddRatio(ratio);
            }
            foreach(float ratio in morph.Ratios) {
                double[] n=new double[4];for(int i=0;i<4;i++)n[i]=start[i]+((double)end[i]-start[i])*ratio;
                double det=(n[0]*n[3]-n[1]*n[2])*16384;float actual;
                Check(morph.TryRatio((float)(n[3]/det),(float)(-n[1]/det),(float)(-n[2]/det),(float)(n[0]/det),out actual)&&Math.Abs(actual-ratio)<1e-6,"Several instances of one focal morph");
            }
            FocalMorph ambiguous=new FocalMorph();Array.Copy(start,ambiguous.Start,4);Array.Copy(start,ambiguous.End,4);ambiguous.AddRatio(0);ambiguous.AddRatio(1);
            double determinant=((double)start[0]*start[3]-(double)start[1]*start[2])*16384;float selected;
            Check(!ambiguous.TryRatio((float)(start[3]/determinant),(float)(-start[1]/determinant),(float)(-start[2]/determinant),(float)(start[0]/determinant),out selected),"Ambiguous instance must retain native parameters");
            double[] unused;Check(!new FocalMorph().TryTransform(0,out unused),"Singular matrix must retain native parameters");
            log.Write("PASS: focal morph matrix/focus interpolation, shared instances, ambiguous and singular fallback");
        }
        static void CheckFocalFrame(Native native,Bitmap image,int frame) {
            Check(native.Renderer.CorrectedFocalBatches>0,"Corrected focal morph in frame "+frame);
            float ratio=frame==62?0:frame==63?21845f/65535:43691f/65535;
            Check(Math.Abs(native.Renderer.LastFocalRatio-ratio)<1e-6,"Actual display instance morph ratio in frame "+frame);
            Check(Math.Abs(native.Renderer.LastFocalFocus-(0.734375f+(0.35546875f-0.734375f)*ratio)*0.999f)<1e-6,"Animated focal point in frame "+frame);
            int min=255,max=0;
            for(int y=85;y<315;y++)for(int x=581;x<859;x++){int green=image.GetPixel(x,y).G;min=Math.Min(min,green);max=Math.Max(max,green);}
            Check(max-min>150,"Focal gradient must retain visible contrast in frame "+frame);
        }
        public static void RunSeek(string dll,string movie,string output,Log log) {
            Directory.CreateDirectory(output);
            string sourceHash=Hash(File.ReadAllBytes(movie));
            System.Collections.Generic.List<Bitmap> reference=new System.Collections.Generic.List<Bitmap>();
            try {
            using(Native native=new Native(dll,log)) {
                native.Open(movie,960,640);
                for(int frame=1;frame<=6;frame++) {
                    native.Seek(frame);native.Frame(false);
                    reference.Add(native.Renderer.Snapshot());
                }
                Bitmap fourth=reference[3];fourth.Save(Path.Combine(output,"reference4.png"),ImageFormat.Png);
                Check(fourth.GetPixel(300,100).ToArgb()==Color.Red.ToArgb(),"Frame 4 red shape position");
                Check(fourth.GetPixel(500,300).ToArgb()==Color.Lime.ToArgb(),"Frame 4 green shape position");
                Check(fourth.GetPixel(850,350).ToArgb()==Color.White.ToArgb(),"Frame 4 must not be displaced offstage");
                native.Seek(5);native.Frame(false);native.Seek(4);native.Frame(false);
                using(Bitmap backwards=native.Renderer.Snapshot()) {
                    backwards.Save(Path.Combine(output,"after5-to4.png"),ImageFormat.Png);
                    int difference=Different(fourth,backwards);
                    log.Write("Seek 5 -> 4: "+difference+" pixels differ from sequential frame 4");
                    Check(difference==0,"Seek 5 -> 4 must reconstruct frame 4");
                }
                for(int frame=reference.Count;frame>=1;frame--) {
                    native.Seek(frame);native.Frame(false);
                    using(Bitmap backwards=native.Renderer.Snapshot()) {
                        int difference=Different(reference[frame-1],backwards);
                        if(difference!=0)backwards.Save(Path.Combine(output,"different-"+frame+".png"),ImageFormat.Png);
                        Check(difference==0,"Backwards seek to frame "+frame+" (different pixels="+difference+")");
                    }
                }
                native.Close();native.Open(movie,960,640);
                foreach(int frame in new int[]{4,5,2,6,3,4}) {
                    native.Seek(frame);native.Frame(false);
                    using(Bitmap jumped=native.Renderer.Snapshot())Check(Different(reference[frame-1],jumped)==0,"Nonadjacent seek to frame "+frame);
                }
            }
            } finally {foreach(Bitmap bitmap in reference)bitmap.Dispose();}
            Check(Hash(File.ReadAllBytes(movie))==sourceHash,"Seeking must not change the source movie");
            log.Write("PASS: backwards seek 5 -> 4, frames 1–6 backwards, fresh/nonadjacent jumps, unchanged source");
        }
        public static void RunFocalInstances(string dll,string movie,string output,Log log) {
            Directory.CreateDirectory(output);
            using(Native native=new Native(dll,log)) {
                native.Open(movie,960,640);Check(native.TotalFrames==2,"Two-frame focal instances fixture");
                for(int frame=1;frame<=2;frame++) {
                    native.Seek(frame);native.Frame(false);
                    Check(native.Renderer.CorrectedFocalBatches>=2,"Both instances of a shared focal fill must be corrected");
                    using(Bitmap corrected=native.Renderer.Snapshot()) {
                        corrected.Save(Path.Combine(output,"instances-"+frame+".png"),ImageFormat.Png);
                        native.Renderer.CorrectFocalMorphs=false;native.Frame(false);
                        using(Bitmap original=native.Renderer.Snapshot()) {
                            // Only the end instance differs in frame 1; both intermediate
                            // instances differ in frame 2. Test each region independently.
                            for(int instance=frame==1?1:0;instance<2;instance++) {
                                int different=0;
                                for(int y=110;y<420;y++)for(int x=75+instance*411;x<270+instance*411;x++)if(corrected.GetPixel(x,y).G!=original.GetPixel(x,y).G)different++;
                                Check(different>10000,"Shared focal instance "+instance+" must use its own morph ratio in frame "+frame);
                            }
                        }
                        native.Renderer.CorrectFocalMorphs=true;native.Frame(false);
                        using(Bitmap repeated=native.Renderer.Snapshot()) {
                            repeated.Save(Path.Combine(output,"instances-repeated-"+frame+".png"),ImageFormat.Png);
                            // Native palette caching has a separate shared-instance issue:
                            // a repeated draw may reuse the other instance's blue palette.
                            // Both endpoints have identical green stops, so that channel
                            // independently verifies the matrix/focus compensation.
                            int different=0;
                            for(int y=0;y<corrected.Height;y++)for(int x=0;x<corrected.Width;x++)if(corrected.GetPixel(x,y).G!=repeated.GetPixel(x,y).G)different++;
                            Check(different==0,"Stable shared focal matrix and focus");
                        }
                    }
                }
            }
            log.Write("PASS: simultaneous instances with ratios 0/1 and 21845/65535, 43691/65535");
        }
        public static void Run(string dll,string movie,string output,Log log) {
            Directory.CreateDirectory(output);
            CheckDllLookup(output);
            CheckFocalGradients(log);
            CheckFocalMorphMath(log);
            byte[] original=File.ReadAllBytes(movie);string hash=Hash(original);
            byte[] prepared=Container.Prepare(original,log);
            Check(Hash(original)==hash,"Container.Prepare changed its input");
            Check(Hash(Container.Prepare(prepared,log))==Hash(prepared),"Preparation is not idempotent");
            Reject(()=>Container.Prepare(new byte[7],log),"truncated header");
            byte[] bad=(byte[])original.Clone();bad[9]=32;
            Reject(()=>Container.Prepare(bad,log),"32-bit input");
            bad=(byte[])original.Clone();bad[0]=0;
            Reject(()=>Container.Prepare(bad,log),"invalid magic");
            for(int mode=0;mode<8;mode++) {
                byte[] block=new byte[16];block[0]=(byte)(1<<mode);
                int[] pixels=Bc7Decoder.decode(4,4,block);int expected=mode<4?unchecked((int)0xff000000):0;
                foreach(int pixel in pixels)Check(pixel==expected,"BC7 zero endpoints, mode "+mode);
            }
            Reject(()=>Bc7Decoder.decode(5,3,new byte[16]),"truncated BC7 edge block");
            log.Write("PASS: container normalization, input validation, BC7 modes 0–7");
            using(Native native=new Native(dll,log)) {
                native.Open(movie,960,640);Check(native.TotalFrames==67,"Expected graphics.iggy with 67 frames");
                for(int frame=1;frame<=native.TotalFrames;frame++) {
                    native.Seek(frame);Check(native.CurrentFrame==frame,"Seek to "+frame);Check(native.Frame(false),"Render frame "+frame);
                    using(Bitmap image=native.Renderer.Snapshot()) {
                        if(frame==11)CheckRadialOrigin(image,log);
                        if(frame==10||frame==12||frame==13)CheckGradientSpread(image,frame,log);
                        if(frame==49)CheckColorEffect(image,log);
                        if(frame==51)CheckFilters(image,log);
                        if(frame==47)CheckScalingGrid(image,log);
                        if(frame>=62&&frame<=64)CheckFocalFrame(native,image,frame);
                        if(frame==66)CheckEditableText(image,log);
                        image.Save(Path.Combine(output,frame.ToString("D3")+".png"),ImageFormat.Png);
                    }
                }
                log.Write("PASS: all 67 frames rendered through the native DLL");
                native.Seek(11);native.Frame(false);
                using(Bitmap origin=native.Renderer.Snapshot()) {
                    CheckRadialOrigin(origin,log);native.Frame(false);
                    using(Bitmap repeated=native.Renderer.Snapshot())Check(Different(origin,repeated)==0,"Stable repeated radial gradient drawing");
                }
                native.Resize(1000,800);native.Frame(false);
                using(Bitmap resized=native.Renderer.Snapshot())CheckRadialOrigin(resized,log);
                native.Seek(51);native.Frame(false);
                using(Bitmap filters=native.Renderer.Snapshot())CheckFilters(filters,log);
                native.Resize(960,640);
                native.Seek(47);native.Frame(false);
                using(Bitmap grid=native.Renderer.Snapshot()) {
                    CheckScalingGrid(grid,log);
                    native.Frame(false);
                    using(Bitmap repeated=native.Renderer.Snapshot())Check(Different(grid,repeated)==0,"Stable repeated scaling-grid drawing");
                }
                native.Resize(1000,800);native.Frame(false);
                using(Bitmap resized=native.Renderer.Snapshot())CheckScalingGrid(resized,log);
                native.Resize(960,640);
                native.Seek(63);native.Frame(false);
                using(Bitmap corrected=native.Renderer.Snapshot()) {
                    native.Renderer.CorrectFocalMorphs=false;native.Frame(false);
                    Check(native.Renderer.CorrectedFocalBatches==0,"Native focal mode must bypass compatibility adapter");
                    using(Bitmap originalFocal=native.Renderer.Snapshot()) {
                        Check(Different(corrected,originalFocal)>10000,"Frame 63 must visibly differ from native focal mode");
                        originalFocal.Save(Path.Combine(output,"focal-native63.png"),ImageFormat.Png);
                    }
                    native.Renderer.CorrectFocalMorphs=true;native.Frame(false);
                    using(Bitmap repeated=native.Renderer.Snapshot())Check(Different(corrected,repeated)==0,"Focal mode toggle must be reversible");
                }
                log.Write("PASS: focal morph frames 62–64, animated focus, visible gradient contrast, reversible native mode");
                native.Seek(0);Check(native.CurrentFrame==1,"Lower seek boundary");
                native.Seek(100000);Check(native.CurrentFrame==67,"Upper seek boundary");
                native.Seek(42);native.Frame(false);
                using(Bitmap before=native.Renderer.Snapshot()) {
                    native.Frame(false);using(Bitmap repeated=native.Renderer.Snapshot())Check(Different(before,repeated)==0,"Repeated draw must be stable");
                    native.MouseMove(400,300);native.Frame(false);
                    using(Bitmap hover=native.Renderer.Snapshot()) {
                        Check(native.CurrentFrame==42,"Button hover must keep the paused frame");
                        Check(Different(before,hover)>100,"Button hover must change its appearance");
                        hover.Save(Path.Combine(output,"button-hover.png"),ImageFormat.Png);
                        native.MouseButton(0,true);native.Frame(false);
                        using(Bitmap down=native.Renderer.Snapshot()) {
                            Check(native.CurrentFrame==42,"Button press must keep the paused frame");
                            Check(Different(hover,down)>100,"Button press must change its hovered appearance");
                            down.Save(Path.Combine(output,"button-down.png"),ImageFormat.Png);
                        }
                    }
                    native.MouseButton(0,false);native.MouseLeave();native.Wheel(1);native.Key(65,true);native.Character('A');native.Key(65,false);
                    native.Frame(false);Check(native.CurrentFrame==42,"Other input must keep the paused frame");
                }
                native.Seek(1);native.SetPaused(false);int start=native.CurrentFrame;
                for(int i=0;i<8;i++){native.MouseMove(10+i*5,10);native.Frame(false);}
                Check(native.CurrentFrame==start,"Input alone must not tick a playing movie");
                for(int i=0;i<12;i++){Thread.Sleep(25);Check(native.Frame(true),"Timed playback");}
                Check(native.CurrentFrame>start,"Playback must advance using the native clock");native.SetPaused(true);
                int paused=native.CurrentFrame;Thread.Sleep(60);native.Frame(false);Check(native.CurrentFrame==paused,"Pause");
                for(int i=0;i<8;i++) {
                    native.MouseMove(10+i*5,10);native.Frame(false);
                    Check(native.CurrentFrame==paused,"Mouse movement must not advance a paused movie");
                }
                Action[] inputs={delegate{native.MouseButton(0,true);},delegate{native.MouseButton(0,false);},delegate{native.Wheel(1);},delegate{native.Key(65,true);},delegate{native.Character('A');},delegate{native.Key(65,false);},delegate{native.MouseLeave();}};
                foreach(Action input in inputs) {
                    input();native.Frame(false);native.Frame(true);
                    Check(native.CurrentFrame==paused,"Mouse/key input must not advance a paused movie");
                }
                Thread.Sleep(100);native.Frame(false);Check(native.CurrentFrame==paused,"Pause must also hold without input");
                native.SetPaused(false);
                for(int i=0;i<6&&native.CurrentFrame==paused;i++){Thread.Sleep(25);native.Frame(true);}
                Check(native.CurrentFrame!=paused,"Playback must resume after paused input");native.SetPaused(true);
                log.Write("PASS: seek boundaries, interactive paused buttons, mouse/key input without timeline ticks, timed playback, pause and resume");
                native.Seek(66);native.Frame(false);native.MouseMove(100,150);native.MouseButton(0,true);native.MouseButton(0,false);
                Check(native.TextFieldFocused,"Editable text must receive keyboard focus");
                native.Frame(false);
                using(Bitmap beforeText=native.Renderer.Snapshot()) {
                    native.Character('A');native.Character('B');native.Frame(false);
                    Check(native.CurrentFrame==66,"Editing text must keep the paused frame");
                    using(Bitmap text=native.Renderer.Snapshot()) {
                        Check(Different(beforeText,text)>30,"Typed text must update visibly during pause");
                        text.Save(Path.Combine(output,"text-input.png"),ImageFormat.Png);
                    }
                }
                log.Write("PASS: editable text keyboard focus and visible editing during pause");
                native.Resize(480,320);native.Frame(false);
                using(Bitmap image=native.Renderer.Snapshot())Check(image.Width==480&&image.Height==320,"Resize");
                native.Close();native.Open(movie,640,480);native.Seek(53);Check(native.Frame(false),"Reopen and editable text frame");
                try{native.Open(Path.Combine(output,"missing-file.iggy"),640,480);throw new InvalidOperationException("Missing movie was accepted");}catch(FileNotFoundException){}
                Check(native.Player==IntPtr.Zero,"Failed Open must release the old player");
                native.Open(movie,640,480);Check(native.Frame(false),"Recover after failed Open");
                native.Dispose();native.Dispose();
                log.Write("PASS: resize, reopen, failed Open recovery, repeated Dispose");
            }
            Check(Hash(File.ReadAllBytes(movie))==hash,"The original file changed on disk");
            log.Write("PASS: source SHA-256 unchanged: "+hash.Replace("-",""));
            log.Write("SELF-TEST PASSED");
        }
        public static void RunFixups(string dll,string movie,string output,Log log) {
            Directory.CreateDirectory(output);byte[] original=File.ReadAllBytes(movie);string hash=Hash(original);
            byte[] prepared=Container.Prepare(original,log);
            Check(Hash(original)==hash,"Fixup preparation must not change its input");
            Check(Hash(Container.Prepare(prepared,log))==Hash(prepared),"Large fixup preparation must be idempotent");
            int blocks=BitConverter.ToInt32(original,28),reduced=0;
            for(int i=0;i<blocks;i++) {
                int at=32+i*16;if(BitConverter.ToUInt16(original,at)!=0)continue;
                int before=BitConverter.ToInt32(original,at+4),after=BitConverter.ToInt32(prepared,at+4);
                Check(after<=65536,"All prepared fixup programs must fit the native temporary buffer");
                if(before>65536){Check(after<before,"Oversized fixup program must shrink");reduced++;}
            }
            Check(reduced>0,"Large fixup fixture must exercise an oversized program");
            using(Native native=new Native(dll,log)) {
                for(int cycle=0;cycle<2;cycle++) {
                    native.Open(movie,550,550);Check(native.StageWidth==550&&native.StageHeight==550,"Game fixture stage after relocation");
                    Check(native.Frame(false),"Game fixture native drawing after relocation");
                    Check(native.Renderer.Triangles>1000,"Game fixture must produce native geometry");
                    using(Bitmap image=native.Renderer.Snapshot()) {
                        image.Save(Path.Combine(output,"initial-"+cycle+".png"),ImageFormat.Png);
                        native.Frame(false);using(Bitmap repeated=native.Renderer.Snapshot())Check(Different(image,repeated)==0,"Stable drawing after large relocation");
                    }
                    log.Write("PASS: Game fixture loaded, stage="+native.StageWidth+"x"+native.StageHeight+", frames="+native.TotalFrames+", triangles="+native.Renderer.Triangles);
                    CheckLoaderCounters(native,original.LongLength);
                    Check(ReadValue(native,"transform.perspectiveProjection.fieldOfView")==55,"Game fixture root projection field of view");
                    Check(ReadValue(native,"transform.perspectiveProjection.projectionCenter.x")==275&&ReadValue(native,"transform.perspectiveProjection.projectionCenter.y")==200,"Game fixture root projection center");
                    if(cycle==0) {
                        for(int step=0;step<100&&ReadValue(native,"sec_count")<=3;step++) {
                            Thread.Sleep(50);Check(native.Frame(true),"Game fixture playback through preloader timers");
                        }
                        Check(native.LoaderInfoStageReads>=6,"Game fixture on_tick must read Stage counters repeatedly");
                        Check(ReadValue(native,"loadingDone","IggyValueGetBooleanRS")==1,"Game fixture preloader loadingDone");
                        Check(ReadValue(native,"sec_count")>3,"Game fixture preloader timer completes");
                        Check(ReadValue(native,"mc_load.visible","IggyValueGetBooleanRS")==0,"Game fixture hides its loading indicator");
                        Check(ReadValue(native,"play_btn.visible","IggyValueGetBooleanRS")==1,"Game fixture on_end exposes play button");
                        native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);
                        GC.Collect();GC.WaitForPendingFinalizers();CheckLoaderCounters(native,original.LongLength);
                        Check(ReadValue(native,"transform.perspectiveProjection.fieldOfView")==55,"Root projection survives native and managed GC");
                        Check(ReadValue(native,"transform.perspectiveProjection.projectionCenter.y")==200,"Projection remains linked to native root after GC");
                        Check(native.Frame(false),"Game fixture drawing after preloader and GC");
                        using(Bitmap image=native.Renderer.Snapshot())image.Save(Path.Combine(output,"preloader-complete.png"),ImageFormat.Png);
                        log.Write("PASS: Game fixture actual AS3 preloader completed; loadingDone=true, loading indicator hidden, play button visible, Stage reads="+native.LoaderInfoStageReads);
                    }
                    native.Close();
                }
                native.Dispose();native.Dispose();
            }
            Check(Hash(File.ReadAllBytes(movie))==hash,"Large fixture changed on disk");
            foreach(string message in log.Messages)Check(message.IndexOf("Uncaught exception",StringComparison.Ordinal)<0,"No uncaught AS3 exceptions during Game fixture initialization/preloader");
            log.Write("PASS: large fixup compression, idempotence, native loading/drawing, Game fixture constructor and preloader without uncaught AS3 exceptions, reopen and unchanged source. Full Game fixture gameplay is not asserted.");
        }
    }
}
