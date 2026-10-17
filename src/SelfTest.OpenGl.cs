using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Windows.Forms;

namespace IggyViewer {
    internal static partial class SelfTest {
        static unsafe double CompareHardware(Bitmap expected,Bitmap actual,out int outliers) {
            Check(expected.Size==actual.Size,"Hardware image dimensions");Rectangle rect=new Rectangle(Point.Empty,expected.Size);
            BitmapData first=expected.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb),second=actual.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            long total=0;outliers=0;
            try {
                for(int y=0;y<expected.Height;y++)for(int x=0;x<expected.Width;x++) {
                    byte* p=(byte*)first.Scan0+y*first.Stride+4*x,q=(byte*)second.Scan0+y*second.Stride+4*x;int maximum=0;
                    for(int channel=0;channel<4;channel++){int difference=Math.Abs(p[channel]-q[channel]);total+=difference;maximum=Math.Max(maximum,difference);}
                    if(maximum>8)outliers++;
                }
            } finally {expected.UnlockBits(first);actual.UnlockBits(second);}
            return total/(4.0*expected.Width*expected.Height);
        }
        static double TimeHardwareFrame(Native native,int frame) {
            native.Seek(frame);native.Frame(false);using(Bitmap image=native.Renderer.Snapshot()){}
            double[] samples=new double[5];
            for(int i=0;i<samples.Length;i++) {
                Stopwatch watch=Stopwatch.StartNew();native.Frame(false);using(Bitmap image=native.Renderer.Snapshot()){}native.Renderer.FinishHardware();watch.Stop();samples[i]=watch.Elapsed.TotalMilliseconds;
            }
            Array.Sort(samples);return samples[samples.Length/2];
        }
        public static void RunOpenGl(string dll,string movie,string output,Log log) {
            Directory.CreateDirectory(output);string sourceHash=Hash(File.ReadAllBytes(movie));
            string reference=Path.Combine(output,"software"),hardware=Path.Combine(output,"opengl");Directory.CreateDirectory(reference);Directory.CreateDirectory(hardware);
            Dictionary<int,double> softwareTimes=new Dictionary<int,double>();
            using(Native native=new Native(dll,log)) {
                native.Open(movie,960,640);
                for(int frame=1;frame<=native.TotalFrames;frame++){native.Seek(frame);native.Frame(false);using(Bitmap image=native.Renderer.Snapshot())image.Save(Path.Combine(reference,frame.ToString("D3")+".png"),ImageFormat.Png);}
                native.Resize(1600,1000);foreach(int frame in new int[]{47,51,63,67})softwareTimes[frame]=TimeHardwareFrame(native,frame);
            }
            using(Native native=new Native(dll,log,renderer:"opengl")) {
                Check(native.Renderer.HardwareAccelerated,"OpenGL is active");
                native.Open(movie,960,640);
                for(int frame=1;frame<=native.TotalFrames;frame++) {
                    native.Seek(frame);native.Frame(false);native.Renderer.FinishHardware();
                    using(Bitmap image=native.Renderer.Snapshot())using(Bitmap expected=new Bitmap(Path.Combine(reference,frame.ToString("D3")+".png"))) {
                        image.Save(Path.Combine(hardware,frame.ToString("D3")+".png"),ImageFormat.Png);int outliers;double mean=CompareHardware(expected,image,out outliers);
                        log.Write("GPU frame="+frame+" mean channel error="+mean.ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+" pixels >8="+outliers+" gpu batches="+native.Renderer.HardwareBatches+" cpu batches="+native.Renderer.SoftwareBatches);
                        Check(mean<0.25&&outliers<image.Width*image.Height/500,"OpenGL frame "+frame+" matches software within raster edge and channel rounding tolerance");
                        if(frame==11)CheckRadialOrigin(image,log);if(frame==47)CheckScalingGrid(image,log);if(frame==49)CheckColorEffect(image,log);if(frame==51)CheckFilters(image,log);if(frame==66)CheckEditableText(image,log);
                        if(frame>=62&&frame<=64)CheckFocalFrame(native,image,frame);
                    }
                }
                native.Resize(1600,1000);foreach(int frame in new int[]{47,51,63,67}){double time=TimeHardwareFrame(native,frame);log.Write("BENCH 1600x1000 frame="+frame+" software="+softwareTimes[frame].ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+"ms OpenGL="+time.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+"ms speedup="+(softwareTimes[frame]/time).ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+"x (median draw + snapshot)");}
                native.Renderer.SetBackend("software");native.Resize(960,640);native.Seek(49);native.Frame(false);using(Bitmap image=native.Renderer.Snapshot())CheckColorEffect(image,log);
                native.Renderer.SetBackend("opengl");native.Frame(false);native.Renderer.FinishHardware();using(Bitmap image=native.Renderer.Snapshot())CheckColorEffect(image,log);
                using(PlayerForm form=new PlayerForm(native,"",log)) {
                    form.ShowInTaskbar=false;form.Opacity=0;form.Show();Application.DoEvents();form.OpenFile(movie,false);form.TestFrame(47);Application.DoEvents();
                    Canvas canvas=null;foreach(Control control in form.Controls)if(control is Canvas)canvas=(Canvas)control;
                    foreach(int frame in new int[]{47,51,67}) {
                        form.TestFrame(frame);long reads=native.Renderer.Readbacks;int targets=native.Renderer.HardwareTargetCount;
                        for(int i=0;i<5;i++){form.TestRenderFrame();canvas.Update();native.Renderer.FinishHardware();}
                        Check(native.Renderer.Readbacks==reads,"GPU geometry, filters and blending in frame "+frame+" do not read back to the CPU during normal presentation");
                        Check(native.Renderer.HardwareTargetCount==targets,"Repeated frame "+frame+" releases temporary GPU filter targets");
                    }
                    using(Bitmap capture=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(capture,new Rectangle(Point.Empty,capture.Size));capture.Save(Path.Combine(output,"gpu-window.png"),ImageFormat.Png);}
                    native.Renderer.SetBackend("software");form.TestRenderFrame();Application.DoEvents();
                    native.Renderer.SetBackend("opengl");form.TestRenderFrame();Application.DoEvents();native.Renderer.FinishHardware();
                    native.Resize(777,513);native.Frame(false);native.Renderer.FinishHardware();
                }
                native.Close();native.Open(movie,640,480);native.Frame(false);native.Renderer.FinishHardware();
            }
            string fixtureDirectory=Path.GetDirectoryName(movie),alphaFixture=Path.Combine(fixtureDirectory,"filters.iggy");
            using(Native native=new Native(dll,log,renderer:"opengl")) {
                native.Open(alphaFixture,500,400);
                foreach(int width in new int[]{500,1000}) {
                    native.Resize(width,width*4/5);native.Frame(false);native.Renderer.FinishHardware();
                    using(Bitmap image=native.Renderer.Snapshot()) {
                        NearColor(StagePixel(image,50,50),Color.FromArgb(233,185,215),2,"GPU color matrix RGB/alpha offsets");
                        NearColor(StagePixel(image,170,50),Color.FromArgb(185,215,233),2,"GPU chained translucent color matrices");
                        NearColor(StagePixel(image,29,275),Color.FromArgb(127,127,255),1,"GPU gradient palette alpha");
                        NearColor(StagePixel(image,50,275),Color.White,0,"GPU gradient knockout source interior");
                        NearColor(StagePixel(image,20,165),Color.White,0,"GPU single blur pass tail bounds");
                        Color tail=StagePixel(image,140,165);Check(tail.G==255&&tail.R<250&&tail.B<250,"GPU multiple blur passes extend bounds");
                        image.Save(Path.Combine(output,"gpu-alpha-filters-"+width+".png"),ImageFormat.Png);
                    }
                }
                native.Close();native.Open(Path.Combine(fixtureDirectory,"as3.iggy"),550,400);native.Frame(false);native.Renderer.FinishHardware();using(Bitmap image=native.Renderer.Snapshot())CheckHelloWorld(native,image,log);
            }
            Check(Hash(File.ReadAllBytes(movie))==sourceHash,"Hardware tests leave the source movie unchanged");
            log.Write("PASS: hardware OpenGL, 67 software comparisons, gradient/color/text/grid checks, timing, direct presentation, GUI capture, backend switching, resize, reopen and unchanged source.");
        }
    }
}
