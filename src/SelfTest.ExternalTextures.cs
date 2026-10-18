using System;
using System.Drawing;
using System.IO;
using TextureFormat = IggyViewer.ExternalTextureDecoder;

namespace IggyViewer {
    internal static partial class SelfTest {
        static byte[] TextureHex(string text) {
            byte[] result=new byte[text.Length/2];
            for(int i=0;i<result.Length;i++)result[i]=Convert.ToByte(text.Substring(i*2,2),16);
            return result;
        }
        public static void RunExternalTextures(string dll,string movie,string output,Log log,string renderer) {
            Directory.CreateDirectory(output);
            string[] encoded={"0b162111","218f","01fc","8040","82","40","77","7b","77"};
            int[] formats={TextureFormat.FORMAT_RGBA_8888, TextureFormat.FORMAT_RGBA_4444_LE, TextureFormat.FORMAT_RGBA_5551_LE, TextureFormat.FORMAT_LA_88, TextureFormat.FORMAT_LA_44, TextureFormat.FORMAT_I_8, TextureFormat.FORMAT_I_4, TextureFormat.FORMAT_L_8, TextureFormat.FORMAT_L_4, TextureFormat.FORMAT_DXT1, TextureFormat.FORMAT_DXT3, TextureFormat.FORMAT_DXT5, TextureFormat.FORMAT_BC7};
            uint[] expected={0x110b1621,0x88ff2211,0xffff0008,0x40808080,0x88222222,0x40404040,0x77777777,0xff7b7b7b,0xff777777};
            for(int f=0;f<encoded.Length;f++)
                Check((uint)ExternalTextureDecoder.Decode(1,1,0x100|formats[f],TextureHex(encoded[f]))[0]==expected[f],"Raw texture channels, format "+formats[f]);
            int[] dxt=ExternalTextureDecoder.Decode(4,4,TextureFormat.FORMAT_DXT1,TextureHex("00f81f00e4e4e4e4"));
            uint[] colors={0xffff0000,0xff0000ff,0xffaa0055,0xff5500aa};
            for(int i=0;i<16;i++)Check((uint)dxt[i]==colors[i%4],"DXT1 color interpolation");
            foreach(int pixel in ExternalTextureDecoder.Decode(3,2,TextureFormat.FORMAT_DXT1,TextureHex("000000f8ffffffff")))Check(pixel==0,"DXT1 transparency and edge clipping");
            dxt=ExternalTextureDecoder.Decode(4,4,TextureFormat.FORMAT_DXT3,TextureHex("1032547698badcfe000000f8ffffffff"));
            for(int i=0;i<16;i++)Check(dxt[i]==(i*17<<24|0xaa0000),"DXT3 explicit alpha and four-color mode");
            int[][] alphas={new int[]{255,0,218,182,145,109,72,36},new int[]{0,255,51,102,153,204,0,255}};
            for(int branch=0;branch<2;branch++) {
                dxt=ExternalTextureDecoder.Decode(4,4,TextureFormat.FORMAT_DXT5,TextureHex(branch==0?"ff0088c6fa88c6fa00f8000000000000":"00ff88c6fa88c6fa00f8000000000000"));
                for(int i=0;i<16;i++)Check(dxt[i]==(alphas[branch][i%8]<<24|0xff0000),"DXT5 interpolated alpha");
            }
            // Independent Pillow DDS decoder reference, six differently encoded blocks.
            string[] hashes={"7614e16971eb102a8bc8c70b3657c0ce72d4bf496274fa21923f1ad9df8a3f3d","49f01a419349b954b4a196610d90780bbfae57282237266a549cbfbbd6783dd3","5694127e1ea9d485d8874f878bdfe06cb22f790f2ccf5b5b5eb40130b7ca4b6c"};
            int[] dxtFormats={TextureFormat.FORMAT_DXT1,TextureFormat.FORMAT_DXT3,TextureFormat.FORMAT_DXT5};
            for(int f=0;f<dxtFormats.Length;f++) {
                int format=dxtFormats[f];
                byte[] blocks=new byte[6*(format==TextureFormat.FORMAT_DXT1?8:16)];
                for(int i=0;i<blocks.Length;i++)blocks[i]=(byte)(i*73+19+(i/7)*31);
                using(MemoryStream bytes=new MemoryStream()) {
                    foreach(int pixel in ExternalTextureDecoder.Decode(12,8,format,blocks))for(int shift=24;shift>=0;shift-=8)bytes.WriteByte((byte)(pixel>>shift));
                    Check(Hash(bytes.ToArray()).Replace("-","").ToLowerInvariant()==hashes[f],"Pillow DDS reference, format "+format);
                }
            }
            Check(ExternalTextureDecoder.DataLength(4,2,0x300|TextureFormat.FORMAT_RGBA_8888)==44&&ExternalTextureDecoder.DataLength(5,3,0x300|TextureFormat.FORMAT_DXT1)==32,"Mip chain sizes");
            Reject(()=>ExternalTextureDecoder.Decode(4,4,0x200|TextureFormat.FORMAT_DXT5,new byte[16]),"truncated lower mip");
            Reject(()=>ExternalTextureDecoder.DataLength(4,4,0x400|TextureFormat.FORMAT_RGBA_8888),"excess mip count");
            Reject(()=>ExternalTextureDecoder.DataLength(1,1,12),"unknown format 12");
            Reject(()=>ExternalTextureDecoder.DataLength(int.MaxValue,int.MaxValue,TextureFormat.FORMAT_RGBA_8888),"texture size overflow");
            using(Native native=new Native(dll,log,renderer:renderer)) {
                native.Open(movie,350,100);Check(native.Frame(false),"External texture fixture frame");
                using(Bitmap image=native.Renderer.Snapshot()) {
                    int[] rgb={0xff0000,0xffff00,0x0000ff,0x808080,0x555555,0xbfbfbf,0xc0c0c0,0x666666,0x777777,0xff0000,0x0000ff,0x00ff00,0xff0000};
                    for(int i=0;i<rgb.Length;i++) {
                        Color actual=image.GetPixel(30+i%7*50,30+i/7*50);
                        Check(Math.Abs(actual.R-(rgb[i]>>16&255))<=1&&Math.Abs(actual.G-(rgb[i]>>8&255))<=1&&Math.Abs(actual.B-(rgb[i]&255))<=1,"Native texture format "+formats[i]+": "+actual);
                    }
                    image.Save(Path.Combine(output,"external-textures.png"));
                }
            }
            log.Write("PASS: raw formats 0-11 and BC7, mip chains, packed channels, DXT alpha, native resources ("+renderer+")");
        }
    }
}
