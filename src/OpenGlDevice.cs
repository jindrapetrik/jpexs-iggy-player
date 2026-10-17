using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace IggyViewer {
    // A single-threaded WGL context. GDraw geometry is drawn into framebuffer
    // objects; the canvas presents their color texture without a CPU readback.
    internal unsafe sealed class OpenGlDevice : IDisposable {
        internal sealed class Target {
            internal uint Framebuffer,Color,Stencil,Destination;
            internal int Width,Height;
            internal bool Complete;
        }
        internal struct Image {
            internal uint Texture;internal int Width,Height,X,Y;internal bool Flipped;
        }
        [StructLayout(LayoutKind.Sequential)] struct PixelFormat {
            internal ushort Size,Version;internal uint Flags;
            internal byte Type,ColorBits,RedBits,RedShift,GreenBits,GreenShift,BlueBits,BlueShift,AlphaBits,AlphaShift;
            internal byte AccumBits,AccumRed,AccumGreen,AccumBlue,AccumAlpha,DepthBits,StencilBits,AuxBuffers,LayerType,Reserved;
            internal uint LayerMask,VisibleMask,DamageMask;
        }
        sealed class ContextWindow : NativeWindow {
            internal ContextWindow(){CreateHandle(new CreateParams {Caption="Iggy OpenGL context",ClassStyle=0x20,Width=1,Height=1});}
        }
        [DllImport("user32")] static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32")] static extern int ReleaseDC(IntPtr window,IntPtr dc);
        [DllImport("gdi32")] static extern int ChoosePixelFormat(IntPtr dc,ref PixelFormat format);
        [DllImport("gdi32")] static extern int GetPixelFormat(IntPtr dc);
        [DllImport("gdi32")] static extern bool SetPixelFormat(IntPtr dc,int index,ref PixelFormat format);
        [DllImport("gdi32")] static extern bool SwapBuffers(IntPtr dc);
        [DllImport("opengl32")] static extern IntPtr wglCreateContext(IntPtr dc);
        [DllImport("opengl32")] static extern bool wglMakeCurrent(IntPtr dc,IntPtr context);
        [DllImport("opengl32")] static extern bool wglDeleteContext(IntPtr context);
        [DllImport("opengl32",CharSet=CharSet.Ansi)] static extern IntPtr wglGetProcAddress(string name);
        [DllImport("opengl32")] static extern IntPtr glGetString(uint name);
        [DllImport("opengl32")] static extern uint glGetError();
        [DllImport("opengl32")] static extern void glGetIntegerv(uint name,out int value);
        [DllImport("opengl32")] static extern void glViewport(int x,int y,int width,int height);
        [DllImport("opengl32")] static extern void glEnable(uint cap);
        [DllImport("opengl32")] static extern void glDisable(uint cap);
        [DllImport("opengl32")] static extern void glBlendFunc(uint source,uint destination);
        [DllImport("opengl32")] static extern void glScissor(int x,int y,int width,int height);
        [DllImport("opengl32")] static extern void glColorMask(byte r,byte g,byte b,byte a);
        [DllImport("opengl32")] static extern void glStencilMask(uint mask);
        [DllImport("opengl32")] static extern void glStencilFunc(uint function,int reference,uint mask);
        [DllImport("opengl32")] static extern void glStencilOp(uint fail,uint depthFail,uint pass);
        [DllImport("opengl32")] static extern void glClearStencil(int value);
        [DllImport("opengl32")] static extern void glClearColor(float r,float g,float b,float a);
        [DllImport("opengl32")] static extern void glClear(uint mask);
        [DllImport("opengl32")] static extern void glGenTextures(int count,out uint texture);
        [DllImport("opengl32")] static extern void glDeleteTextures(int count,ref uint texture);
        [DllImport("opengl32")] static extern void glBindTexture(uint target,uint texture);
        [DllImport("opengl32")] static extern void glTexParameteri(uint target,uint parameter,int value);
        [DllImport("opengl32")] static extern void glTexImage2D(uint target,int level,int format,int width,int height,int border,uint pixels,uint type,IntPtr data);
        [DllImport("opengl32")] static extern void glTexSubImage2D(uint target,int level,int x,int y,int width,int height,uint pixels,uint type,IntPtr data);
        [DllImport("opengl32")] static extern void glCopyTexSubImage2D(uint target,int level,int xoffset,int yoffset,int x,int y,int width,int height);
        [DllImport("opengl32")] static extern void glPixelStorei(uint parameter,int value);
        [DllImport("opengl32")] static extern void glReadPixels(int x,int y,int width,int height,uint format,uint type,IntPtr data);
        [DllImport("opengl32")] static extern void glReadBuffer(uint buffer);
        [DllImport("opengl32")] static extern void glDrawBuffer(uint buffer);
        [DllImport("opengl32")] static extern void glBegin(uint mode);
        [DllImport("opengl32")] static extern void glEnd();
        [DllImport("opengl32")] static extern void glVertex2f(float x,float y);
        [DllImport("opengl32")] static extern void glTexCoord2f(float u,float v);
        [DllImport("opengl32")] static extern void glColor4f(float r,float g,float b,float a);
        [DllImport("opengl32")] static extern void glFinish();
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void Gen(int count,out uint value);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void Delete(int count,ref uint value);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void Bind(uint target,uint value);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void AttachTexture(uint target,uint attachment,uint textureTarget,uint texture,int level);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate uint CheckTarget(uint target);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void Storage(uint target,uint format,int width,int height);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void AttachBuffer(uint target,uint attachment,uint bufferTarget,uint buffer);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate uint CreateShader(uint kind);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void ShaderSource(uint shader,int count,IntPtr source,IntPtr lengths);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void ObjectCall(uint value);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate uint CreateProgram();
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void AttachShader(uint program,uint shader);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void GetObject(uint value,uint parameter,out int result);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void GetLog(uint value,int capacity,out int length,StringBuilder text);
        [UnmanagedFunctionPointer(CallingConvention.Winapi,CharSet=CharSet.Ansi)] delegate int UniformLocation(uint program,string name);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void Uniform1(int location,int value);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void Uniform2(int location,float x,float y);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void Uniform3(int location,float x,float y,float z);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void Uniform4(int location,float x,float y,float z,float w);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate int SwapInterval(int interval);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void ActiveTexture(uint unit);
        readonly ContextWindow window;
        IntPtr dc,context;
        PixelFormat pixelFormat;
        int formatIndex;
        readonly Gen genFramebuffer,genBuffer;
        readonly Delete deleteFramebuffer,deleteBuffer;
        readonly Bind bindFramebuffer,bindBuffer;
        readonly AttachTexture attachTexture;
        readonly CheckTarget checkTarget;
        readonly Storage storage;
        readonly AttachBuffer attachBuffer;
        readonly CreateShader createShader;
        readonly ShaderSource shaderSource;
        readonly ObjectCall compileShader,deleteShader,linkProgram,useProgram,deleteProgram;
        readonly CreateProgram createProgram;
        readonly AttachShader attachShader;
        readonly GetObject shaderStatus,programStatus;
        readonly GetLog shaderLog,programLog;
        readonly UniformLocation uniformLocation;
        readonly Uniform1 uniform1;
        readonly Uniform2 uniform2;
        readonly Uniform3 uniform3;
        readonly Uniform4 uniform4;
        readonly ActiveTexture activeTexture;
        readonly Dictionary<string,int> uniforms=new Dictionary<string,int>();
        readonly HashSet<Target> targets=new HashSet<Target>();
        readonly Dictionary<long,Stack<Target>> targetPool=new Dictionary<long,Stack<Target>>();
        long pooledBytes;
        readonly HashSet<uint> textures=new HashSet<uint>();
        uint program,filterProgram,currentProgram;
        int blendMode,batchLeft,batchTop,batchRight,batchBottom;
        Target batchTarget;
        bool disposed;
        uint[] colorRead;
        byte[] stencilRead;
        internal readonly string Description;
        internal long Readbacks;
        internal int TargetCount {get{return targets.Count;}}
        const uint Fbo=0x8d40,ColorAttachment=0x8ce0,Renderbuffer=0x8d41,StencilAttachment=0x8d20,Texture2D=0xde1;
        T Function<T>(string name,string alternate=null) where T:class {
            IntPtr pointer=wglGetProcAddress(name);
            if(Invalid(pointer)&&alternate!=null)pointer=wglGetProcAddress(alternate);
            if(Invalid(pointer))throw new NotSupportedException("Missing OpenGL function "+name);
            return (T)(object)Marshal.GetDelegateForFunctionPointer(pointer,typeof(T));
        }
        static bool Invalid(IntPtr pointer){long value=pointer.ToInt64();return value==0||value==1||value==2||value==3||value==-1;}
        internal OpenGlDevice(Log log) {
            window=new ContextWindow();
            try {
                dc=GetDC(window.Handle);
                pixelFormat=new PixelFormat {Size=(ushort)Marshal.SizeOf(typeof(PixelFormat)),Version=1,Flags=4|0x20|1,ColorBits=32,AlphaBits=8,StencilBits=8};
                formatIndex=ChoosePixelFormat(dc,ref pixelFormat);
                if(formatIndex==0||!SetPixelFormat(dc,formatIndex,ref pixelFormat))throw new NotSupportedException("Cannot select an OpenGL pixel format.");
                context=wglCreateContext(dc);
                if(context==IntPtr.Zero||!wglMakeCurrent(dc,context))throw new NotSupportedException("Cannot create an OpenGL context.");
                string vendor=Marshal.PtrToStringAnsi(glGetString(0x1f00)),renderer=Marshal.PtrToStringAnsi(glGetString(0x1f01)),version=Marshal.PtrToStringAnsi(glGetString(0x1f02));
                string driver=(renderer??"").ToLowerInvariant();
                if(string.IsNullOrEmpty(version)||driver.Contains("gdi generic")||driver.Contains("llvmpipe")||driver.Contains("softpipe")||driver.Contains("swiftshader")||driver.Contains("software")||driver.Contains("basic render driver"))throw new NotSupportedException("Only a software OpenGL driver is available.");
                Description="OpenGL: "+renderer+" ("+vendor+", "+version+")";
                genFramebuffer=Function<Gen>("glGenFramebuffers","glGenFramebuffersEXT");deleteFramebuffer=Function<Delete>("glDeleteFramebuffers","glDeleteFramebuffersEXT");
                bindFramebuffer=Function<Bind>("glBindFramebuffer","glBindFramebufferEXT");attachTexture=Function<AttachTexture>("glFramebufferTexture2D","glFramebufferTexture2DEXT");
                checkTarget=Function<CheckTarget>("glCheckFramebufferStatus","glCheckFramebufferStatusEXT");genBuffer=Function<Gen>("glGenRenderbuffers","glGenRenderbuffersEXT");
                deleteBuffer=Function<Delete>("glDeleteRenderbuffers","glDeleteRenderbuffersEXT");bindBuffer=Function<Bind>("glBindRenderbuffer","glBindRenderbufferEXT");
                storage=Function<Storage>("glRenderbufferStorage","glRenderbufferStorageEXT");attachBuffer=Function<AttachBuffer>("glFramebufferRenderbuffer","glFramebufferRenderbufferEXT");
                createShader=Function<CreateShader>("glCreateShader");shaderSource=Function<ShaderSource>("glShaderSource");compileShader=Function<ObjectCall>("glCompileShader");
                deleteShader=Function<ObjectCall>("glDeleteShader");shaderStatus=Function<GetObject>("glGetShaderiv");shaderLog=Function<GetLog>("glGetShaderInfoLog");
                createProgram=Function<CreateProgram>("glCreateProgram");attachShader=Function<AttachShader>("glAttachShader");linkProgram=Function<ObjectCall>("glLinkProgram");
                useProgram=Function<ObjectCall>("glUseProgram");deleteProgram=Function<ObjectCall>("glDeleteProgram");programStatus=Function<GetObject>("glGetProgramiv");programLog=Function<GetLog>("glGetProgramInfoLog");
                uniformLocation=Function<UniformLocation>("glGetUniformLocation");uniform1=Function<Uniform1>("glUniform1i");uniform2=Function<Uniform2>("glUniform2f");
                uniform3=Function<Uniform3>("glUniform3f");uniform4=Function<Uniform4>("glUniform4f");
                activeTexture=Function<ActiveTexture>("glActiveTexture");
                int maxTexture,maxRenderbuffer;glGetIntegerv(0xd33,out maxTexture);glGetIntegerv(0x84e8,out maxRenderbuffer);
                if(maxTexture<8192||maxRenderbuffer<4096)throw new NotSupportedException("The OpenGL driver cannot accommodate the player's texture and render-target limits.");
                program=Link(FragmentShader);filterProgram=Link(FilterShader);
                glDisable(0xbd0);glDisable(0xb71);glDisable(0xb44);glPixelStorei(0xd05,1);glPixelStorei(0xcf5,1);
                IntPtr swap=wglGetProcAddress("wglSwapIntervalEXT");if(!Invalid(swap))((SwapInterval)Marshal.GetDelegateForFunctionPointer(swap,typeof(SwapInterval)))(0);
                log.Write(Description);
            } catch {Dispose();throw;}
        }
        uint Compile(uint kind,string source) {
            uint shader=createShader(kind);IntPtr text=Marshal.StringToHGlobalAnsi(source);
            try {shaderSource(shader,1,(IntPtr)(&text),IntPtr.Zero);compileShader(shader);int compiled;shaderStatus(shader,0x8b81,out compiled);
                if(compiled==0){int size;StringBuilder message=new StringBuilder(4096);shaderLog(shader,message.Capacity,out size,message);throw new NotSupportedException("OpenGL shader compilation: "+message);}
                return shader;
            } catch {deleteShader(shader);throw;} finally {Marshal.FreeHGlobal(text);}
        }
        uint Link(string source) {
            uint vertex=Compile(0x8b31,VertexShader),fragment=0,result=0;
            try {
                fragment=Compile(0x8b30,source);result=createProgram();attachShader(result,vertex);attachShader(result,fragment);linkProgram(result);int linked;programStatus(result,0x8b82,out linked);
                if(linked==0){int size;StringBuilder message=new StringBuilder(4096);programLog(result,message.Capacity,out size,message);throw new NotSupportedException("OpenGL shader link: "+message);}
                return result;
            } catch {if(result!=0)deleteProgram(result);throw;} finally {deleteShader(vertex);if(fragment!=0)deleteShader(fragment);}
        }
        void Use(uint value){useProgram(value);currentProgram=value;}
        int U(string name){string key=currentProgram+":"+name;int location;if(!uniforms.TryGetValue(key,out location)){location=uniformLocation(currentProgram,name);uniforms.Add(key,location);}return location;}
        internal Target CreateTarget(int width,int height,bool stencil=true) {
            long key=((long)width<<33)|((long)height<<1)|(stencil?1L:0L);Stack<Target> available;
            if(targetPool.TryGetValue(key,out available)) {
                Target reused=available.Pop();if(available.Count==0)targetPool.Remove(key);
                pooledBytes-=TargetBytes(reused);targets.Add(reused);if(stencil)Clear(reused,0);return reused;
            }
            Target target=new Target {Width=width,Height=height};targets.Add(target);
            try {
                target.Color=CreateTexture(width,height,null);genFramebuffer(1,out target.Framebuffer);bindFramebuffer(Fbo,target.Framebuffer);
                attachTexture(Fbo,ColorAttachment,Texture2D,target.Color,0);
                // A packed depth/stencil attachment is widely supported even
                // on drivers that reject standalone stencil renderbuffers.
                if(stencil){genBuffer(1,out target.Stencil);bindBuffer(Renderbuffer,target.Stencil);storage(Renderbuffer,0x88f0,width,height);attachBuffer(Fbo,0x8d00,Renderbuffer,target.Stencil);attachBuffer(Fbo,StencilAttachment,Renderbuffer,target.Stencil);}
                uint status=checkTarget(Fbo);if(status!=0x8cd5)throw new NotSupportedException("Incomplete OpenGL framebuffer: 0x"+status.ToString("x"));
                target.Complete=true;if(stencil)Clear(target,0);
                return target;
            } catch {DeleteTarget(target);throw;}
        }
        internal void DeleteTarget(Target target) {
            if(target==null||!targets.Remove(target))return;
            long bytes=TargetBytes(target);
            if(!disposed&&target.Complete&&pooledBytes+bytes<=64*1024*1024) {
                long key=((long)target.Width<<33)|((long)target.Height<<1)|(target.Stencil!=0?1L:0L);Stack<Target> available;
                if(!targetPool.TryGetValue(key,out available)){available=new Stack<Target>();targetPool.Add(key,available);}available.Push(target);pooledBytes+=bytes;return;
            }
            DestroyTarget(target);
        }
        static long TargetBytes(Target target){return (long)target.Width*target.Height*(4+(target.Stencil==0?0:4)+(target.Destination==0?0:4));}
        void DestroyTarget(Target target) {
            if(target.Framebuffer!=0)deleteFramebuffer(1,ref target.Framebuffer);
            if(target.Stencil!=0)deleteBuffer(1,ref target.Stencil);
            DeleteTexture(target.Color);target.Color=0;
            DeleteTexture(target.Destination);target.Destination=0;
        }
        internal uint CreateTexture(int width,int height,uint[] pixels) {
            uint texture;glGenTextures(1,out texture);textures.Add(texture);glBindTexture(Texture2D,texture);
            glTexParameteri(Texture2D,0x2801,0x2600);glTexParameteri(Texture2D,0x2800,0x2600);glTexParameteri(Texture2D,0x2802,0x812f);glTexParameteri(Texture2D,0x2803,0x812f);
            fixed(uint* data=pixels)glTexImage2D(Texture2D,0,0x8058,width,height,0,0x80e1,0x1401,(IntPtr)data);
            return texture;
        }
        internal void DeleteTexture(uint texture){if(texture!=0&&textures.Remove(texture))glDeleteTextures(1,ref texture);}
        void BindTarget(Target target){bindFramebuffer(Fbo,target.Framebuffer);glViewport(0,0,target.Width,target.Height);glDrawBuffer(ColorAttachment);glReadBuffer(ColorAttachment);}
        internal void Clear(Target target,uint color) {
            BindTarget(target);glDisable(0xc11);glColorMask(1,1,1,1);glStencilMask(255);glClearStencil(0);
            glClearColor(((color>>16)&255)/255f,((color>>8)&255)/255f,(color&255)/255f,(color>>24)/255f);glClear(0x4000|0x400);
        }
        internal void ClearStencil(Target target,int bits,int left,int top,int right,int bottom) {
            BindTarget(target);glEnable(0xc11);glScissor(left,target.Height-bottom,Math.Max(0,right-left),Math.Max(0,bottom-top));
            glStencilMask((uint)bits);glClearStencil(0);glClear(0x400);
        }
        internal void Read(Target target,uint[] pixels,byte[] stencil) {
            BindTarget(target);int length=target.Width*target.Height;
            if(colorRead==null||colorRead.Length<length)colorRead=new uint[length];
            fixed(uint* data=colorRead)glReadPixels(0,0,target.Width,target.Height,0x80e1,0x1401,(IntPtr)data);
            for(int y=0;y<target.Height;y++)Array.Copy(colorRead,(target.Height-1-y)*target.Width,pixels,y*target.Width,target.Width);
            if(stencil!=null) {
                if(stencilRead==null||stencilRead.Length<length)stencilRead=new byte[length];
                fixed(byte* data=stencilRead)glReadPixels(0,0,target.Width,target.Height,0x1901,0x1401,(IntPtr)data);
                for(int y=0;y<target.Height;y++)Array.Copy(stencilRead,(target.Height-1-y)*target.Width,stencil,y*target.Width,target.Width);
            }
            Readbacks++;
        }
        internal void Upload(Target target,uint[] pixels) {
            int length=target.Width*target.Height;if(colorRead==null||colorRead.Length<length)colorRead=new uint[length];
            for(int y=0;y<target.Height;y++)Array.Copy(pixels,y*target.Width,colorRead,(target.Height-1-y)*target.Width,target.Width);
            glBindTexture(Texture2D,target.Color);fixed(uint* data=colorRead)glTexSubImage2D(Texture2D,0,0,0,target.Width,target.Height,0x80e1,0x1401,(IntPtr)data);
        }
        internal void Begin(Target target,IntPtr style,uint texture,int textureWidth,int textureHeight,bool gradient,bool flipped,int format,int left,int top,int right,int bottom) {
            BindTarget(target);Use(program);glEnable(0xc11);glScissor(left,target.Height-bottom,Math.Max(0,right-left),Math.Max(0,bottom-top));
            int read=Marshal.ReadByte(style,16),write=Marshal.ReadByte(style,17);
            glEnable(0xb90);glStencilFunc(0x202,read|write,(uint)read);glStencilMask((uint)write);glStencilOp(0x1e00,0x1e00,0x1e01);
            byte color=(byte)(write==0?1:0);glColorMask(color,color,color,color);
            blendMode=write==0?Marshal.ReadByte(style,13):0;
            if(blendMode==0){glEnable(0xbe2);glBlendFunc(1,0x303);}else glDisable(0xbe2);
            batchTarget=target;batchLeft=left;batchTop=top;batchRight=right;batchBottom=bottom;
            uniform2(U("viewport"),target.Width,target.Height);uniform1(U("image"),0);uniform1(U("textured"),texture==0?0:1);uniform1(U("present"),0);
            uniform1(U("kind"),Marshal.ReadByte(style,9));uniform1(U("address"),Marshal.ReadByte(style,10));uniform1(U("nearest"),Marshal.ReadByte(style,11)==0?0:1);
            uniform1(U("gradient"),gradient?1:0);uniform1(U("directUV"),Marshal.ReadByte(style,8)!=0&&format==2?1:0);
            uniform1(U("imageFlipped"),flipped?1:0);uniform1(U("blend"),blendMode);uniform1(U("destination"),1);
            uniform2(U("imageSize"),Math.Max(1,textureWidth),Math.Max(1,textureHeight));
            float* s=(float*)style;uniform4(U("multiply"),s[18],s[19],s[20],s[21]);
            IntPtr add=Marshal.ReadIntPtr(style,24);uniform4(U("addition"),add==IntPtr.Zero?0:Marshal.ReadInt16(add,0)/255f,add==IntPtr.Zero?0:Marshal.ReadInt16(add,2)/255f,add==IntPtr.Zero?0:Marshal.ReadInt16(add,4)/255f,add==IntPtr.Zero?0:Marshal.ReadInt16(add,6)/255f);
            uniform3(U("uRow"),s[26],s[27],s[29]);uniform3(U("vRow"),s[30],s[31],s[33]);uniform3(U("focus"),s[34],s[35],s[36]);
            glBindTexture(Texture2D,texture);
            if(blendMode!=0) {
                activeTexture(0x84c1);if(target.Destination==0)target.Destination=CreateTexture(target.Width,target.Height,null);
                glBindTexture(Texture2D,target.Destination);activeTexture(0x84c0);
            } else glBegin(4);
        }
        static void Vertex(SoftwareRenderer.Vertex v){glColor4f(1,1,1,v.Coverage);glTexCoord2f(v.U,v.V);glVertex2f(v.X,v.Y);}
        internal void Triangle(SoftwareRenderer.Vertex a,SoftwareRenderer.Vertex b,SoftwareRenderer.Vertex c) {
            if(blendMode!=0) {
                int left=Math.Max(batchLeft,(int)Math.Floor(Math.Min(a.X,Math.Min(b.X,c.X)))),top=Math.Max(batchTop,(int)Math.Floor(Math.Min(a.Y,Math.Min(b.Y,c.Y))));
                int right=Math.Min(batchRight,(int)Math.Ceiling(Math.Max(a.X,Math.Max(b.X,c.X)))),bottom=Math.Min(batchBottom,(int)Math.Ceiling(Math.Max(a.Y,Math.Max(b.Y,c.Y))));
                if(right<=left||bottom<=top)return;
                activeTexture(0x84c1);glBindTexture(Texture2D,batchTarget.Destination);
                glCopyTexSubImage2D(Texture2D,0,left,batchTarget.Height-bottom,left,batchTarget.Height-bottom,right-left,bottom-top);activeTexture(0x84c0);glBegin(4);
            }
            Vertex(a);Vertex(b);Vertex(c);if(blendMode!=0)glEnd();
        }
        internal void End(){if(blendMode==0)glEnd();}
        void FilterImage(string name,Image image,int unit) {
            activeTexture((uint)(0x84c0+unit));glBindTexture(Texture2D,image.Texture);uniform1(U(name),unit);uniform2(U(name+"Size"),Math.Max(1,image.Width),Math.Max(1,image.Height));
            uniform2(U(name+"Origin"),image.X,image.Y);uniform1(U(name+"Flip"),image.Flipped?1:0);
        }
        internal void Filter(Target target,int left,int top,Image source,Image original,Image palette,IntPtr style,int effect,int radius=0,bool vertical=false) {
            BindTarget(target);Use(filterProgram);glDisable(0xc11);glDisable(0xb90);glDisable(0xbe2);glColorMask(1,1,1,1);
            uniform2(U("viewport"),target.Width,target.Height);uniform2(U("origin"),left,top);uniform1(U("effect"),effect);uniform1(U("radius"),radius);uniform1(U("vertical"),vertical?1:0);
            uniform1(U("hasPalette"),palette.Texture==0?0:1);
            FilterImage("sourceImage",source,0);FilterImage("originalImage",original,1);FilterImage("paletteImage",palette,2);activeTexture(0x84c0);
            float* s=(float*)style;
            uniform4(U("row0"),s[40],s[41],s[42],s[43]);uniform4(U("row1"),s[44],s[45],s[46],s[47]);uniform4(U("row2"),s[48],s[49],s[50],s[51]);uniform4(U("row3"),s[52],s[53],s[54],s[55]);uniform4(U("offsets"),s[56],s[57],s[58],s[59]);
            int flags=Marshal.ReadByte(style,15);uniform1(U("inner"),(flags&4)!=0?1:0);uniform1(U("knockout"),(flags&1)!=0?1:0);uniform1(U("full"),(flags&8)!=0?1:0);
            uniform3(U("shadow"),s[44],s[45],s[46]);
            glBegin(7);glVertex2f(0,0);glVertex2f(target.Width,0);glVertex2f(target.Width,target.Height);glVertex2f(0,target.Height);glEnd();
        }
        internal void Finish(){glFinish();uint error=glGetError();if(error!=0)throw new InvalidOperationException("OpenGL error 0x"+error.ToString("x"));}
        internal void Present(Target target,IntPtr canvas,int width,int height) {
            IntPtr canvasDc=GetDC(canvas);
            try {
                int currentFormat=GetPixelFormat(canvasDc);
                if(currentFormat==0){if(!SetPixelFormat(canvasDc,formatIndex,ref pixelFormat))throw new InvalidOperationException("Cannot set canvas OpenGL pixel format.");}
                else if(currentFormat!=formatIndex)throw new InvalidOperationException("Canvas has an incompatible OpenGL pixel format.");
                if(!wglMakeCurrent(canvasDc,context))throw new InvalidOperationException("Cannot activate the OpenGL canvas.");
                bindFramebuffer(Fbo,0);glDrawBuffer(0x405);glViewport(0,0,width,height);glDisable(0xc11);glDisable(0xb90);glDisable(0xbe2);glColorMask(1,1,1,1);
                Use(program);uniform2(U("viewport"),width,height);uniform1(U("present"),1);uniform1(U("image"),0);glBindTexture(Texture2D,target.Color);
                glBegin(7);glTexCoord2f(0,1);glVertex2f(0,0);glTexCoord2f(1,1);glVertex2f(width,0);glTexCoord2f(1,0);glVertex2f(width,height);glTexCoord2f(0,0);glVertex2f(0,height);glEnd();
                if(!SwapBuffers(canvasDc))throw new InvalidOperationException("OpenGL buffer presentation failed.");
            } finally {wglMakeCurrent(dc,context);ReleaseDC(canvas,canvasDc);}
        }
        public void Dispose() {
            if(disposed)return;disposed=true;
            if(context!=IntPtr.Zero) {
                wglMakeCurrent(dc,context);
                foreach(Target target in new List<Target>(targets))DeleteTarget(target);
                foreach(Stack<Target> available in targetPool.Values)foreach(Target target in available)DestroyTarget(target);targetPool.Clear();pooledBytes=0;
                foreach(uint texture in new List<uint>(textures))DeleteTexture(texture);
                if(program!=0&&deleteProgram!=null)deleteProgram(program);
                if(filterProgram!=0&&deleteProgram!=null)deleteProgram(filterProgram);
                wglMakeCurrent(IntPtr.Zero,IntPtr.Zero);wglDeleteContext(context);context=IntPtr.Zero;
            }
            if(dc!=IntPtr.Zero){ReleaseDC(window.Handle,dc);dc=IntPtr.Zero;}
            window.DestroyHandle();
        }
        const string FilterShader=@"#version 120
uniform sampler2D sourceImage,originalImage,paletteImage;
uniform vec2 viewport,origin,sourceImageSize,sourceImageOrigin,originalImageSize,originalImageOrigin,paletteImageSize,paletteImageOrigin;
uniform int sourceImageFlip,originalImageFlip,paletteImageFlip,effect,radius,vertical,hasPalette,inner,knockout,full;
uniform vec4 row0,row1,row2,row3,offsets;
uniform vec3 shadow;
vec4 bytes(vec4 c){return floor(clamp(c,0.0,1.0)*255.0+0.5)/255.0;}
vec4 pixel(sampler2D image,vec2 position,vec2 size,vec2 start,int flip){
 vec2 p=position-start;if(p.x<0.0||p.y<0.0||p.x>=size.x||p.y>=size.y)return vec4(0.0);
 if(flip!=0)p.y=size.y-1.0-p.y;return texture2D(image,(p+0.5)/size);
}
vec4 source(vec2 p){return pixel(sourceImage,p,sourceImageSize,sourceImageOrigin,sourceImageFlip);}
float alphaAt(vec2 p){vec2 base=floor(p),f=fract(p);return mix(mix(source(base).a,source(base+vec2(1,0)).a,f.x),mix(source(base+vec2(0,1)).a,source(base+vec2(1,1)).a,f.x),f.y);}
vec4 palettePixel(vec2 p){p=clamp(p,vec2(0.0),paletteImageSize-1.0);if(paletteImageFlip!=0)p.y=paletteImageSize.y-1.0-p.y;return texture2D(paletteImage,(p+0.5)/paletteImageSize);}
vec4 paletteAt(float value){vec2 p=vec2(value,0.5)*paletteImageSize-0.5,base=floor(p),f=fract(p);
 return bytes(mix(mix(palettePixel(base),palettePixel(base+vec2(1,0)),f.x),mix(palettePixel(base+vec2(0,1)),palettePixel(base+vec2(1,1)),f.x),f.y));}
vec4 over(vec4 a,vec4 b){return a+floor(b*255.0*(1.0-a.a)+0.0001)/255.0;}
void main(){
 vec2 world=origin+vec2(gl_FragCoord.x-0.5,viewport.y-gl_FragCoord.y-0.5);
 if(effect==0){vec4 sum=vec4(0.0);for(int i=-radius;i<=radius;i++)sum+=source(world+(vertical!=0?vec2(0.0,float(i)):vec2(float(i),0.0)));
  gl_FragColor=floor(sum*255.0/float(2*radius+1)+0.002)/255.0;return;}
 if(effect==1){vec4 c=source(world);if(c.a>0.0)c.rgb/=c.a;
  vec4 result=floor(clamp(vec4(dot(c,row0),dot(c,row1),dot(c,row2),dot(c,row3))+offsets,0.0,1.0)*255.0+0.5);
  gl_FragColor=vec4(floor(result.rgb*result.a/255.0)/255.0,result.a/255.0);return;}
 vec4 object=pixel(originalImage,world,originalImageSize,originalImageOrigin,originalImageFlip);
 float mask=alphaAt(world-shadow.xy);vec4 color=row0;
 if(effect==2){mask-=alphaAt(world+shadow.xy);if(mask<0.0)color=row2;}
 else if(inner!=0&&hasPalette==0)mask=1.0-mask;
 vec4 filtered;
 if(hasPalette!=0){float coordinate=effect==2?0.5-0.5*clamp(mask*shadow.z,-1.0,1.0):clamp(mask*shadow.z,0.0,1.0);filtered=paletteAt(coordinate);}
 else filtered=bytes(color*clamp(abs(mask)*shadow.z,0.0,1.0));
 if(knockout!=0)gl_FragColor=bytes(filtered*(full!=0?1.0:inner!=0?object.a:1.0-object.a));
 else if(full!=0)gl_FragColor=over(filtered,object);
 else if(inner!=0)gl_FragColor=vec4(min(vec3(1.0),bytes(filtered*object.a).rgb+bytes(object*(1.0-filtered.a)).rgb),object.a);
 else gl_FragColor=over(object,filtered);
}";
        const string VertexShader=@"#version 120
uniform vec2 viewport;
varying vec2 uv;varying float coverage;
void main(){gl_Position=vec4(gl_Vertex.x*2.0/viewport.x-1.0,1.0-gl_Vertex.y*2.0/viewport.y,0.0,1.0);uv=gl_MultiTexCoord0.xy;coverage=gl_Color.a;}";
        const string FragmentShader=@"#version 120
uniform sampler2D image,destination;
uniform vec2 imageSize;
uniform vec2 viewport;
uniform int textured,kind,address,nearest,gradient,directUV,present,imageFlipped,blend;
uniform vec4 multiply,addition;
uniform vec3 uRow,vRow,focus;
varying vec2 uv;varying float coverage;
float rounded(float v){return floor(clamp(v,0.0,1.0)*255.0+0.5)/255.0;}
vec2 addressed(vec2 p,int mode){
 if(mode==1)return mod(p,imageSize);
 if(mode==2){p=mod(p,2.0*imageSize);return mix(p,2.0*imageSize-1.0-p,step(imageSize,p));}
 return clamp(p,vec2(0.0),imageSize-1.0);
}
vec4 texel(vec2 p,int mode){p=addressed(p,mode);if(imageFlipped!=0)p.y=imageSize.y-1.0-p.y;return texture2D(image,(p+0.5)/imageSize);}
void main(){
 if(present!=0){gl_FragColor=texture2D(image,uv);return;}
 vec4 color=vec4(1.0);
 if(textured!=0){
  vec2 p=uv;if(directUV==0)p=vec2(dot(vec3(uv,1.0),uRow),dot(vec3(uv,1.0),vRow));
  if(kind==3)p=vec2(length(p),0.0);
  if(kind==4){float dx=p.x+focus.x,projection=-focus.y*dx/focus.z,root=sqrt(dx*dx+p.y*p.y/focus.z);
   float distance=projection<0.0?(dx*dx+p.y*p.y)/(root-projection):(projection+root)*focus.z;p=vec2(distance,0.0);}
  if(address==2){p=mod(p,2.0);p=mix(p,2.0-p,step(vec2(1.0),p));}
  else if(address==1)p=fract(p);else p=clamp(p,0.0,0.999999);
  if(nearest!=0)color=texel(floor(p*imageSize),0);
  else{vec2 position=p*imageSize-0.5,base=floor(position),f=fract(position);int mode=(gradient!=0||kind==3||kind==4)?0:address;
   color=mix(mix(texel(base,mode),texel(base+vec2(1.0,0.0),mode),f.x),mix(texel(base+vec2(0.0,1.0),mode),texel(base+vec2(1.0,1.0),mode),f.x),f.y);
   color=floor(color*255.0+0.5)/255.0;}
  if(color.a>0.0)color.rgb/=color.a;
 }
 float alpha=rounded((color.a*multiply.a+addition.a)*coverage);if(alpha==0.0)discard;
 vec3 rgb=floor(clamp(color.rgb*multiply.rgb+addition.rgb,0.0,1.0)*255.0+0.5)/255.0;
 if(blend==0){gl_FragColor=vec4(rgb*alpha,alpha);return;}
 vec4 dest=texture2D(destination,gl_FragCoord.xy/viewport);
 if(blend==11||blend==12){gl_FragColor=floor(dest*(blend==11?1.0-alpha:alpha)*255.0+0.0001)/255.0;return;}
 vec3 d=dest.a>0.0?dest.rgb/dest.a:vec3(0.0),b=rgb;
 if(blend==1)b=rgb*d;else if(blend==2)b=rgb+d-rgb*d;else if(blend==3)b=max(rgb,d);else if(blend==4)b=min(rgb,d);
 else if(blend==5)b=min(vec3(1.0),rgb+d);else if(blend==6)b=max(vec3(0.0),d-rgb);else if(blend==7)b=1.0-d;else if(blend==8)b=abs(d-rgb);
 else if(blend==9)b=mix(2.0*rgb*d,1.0-2.0*(1.0-rgb)*(1.0-d),step(vec3(0.500001),d));
 else if(blend==10)b=mix(2.0*rgb*d,1.0-2.0*(1.0-rgb)*(1.0-d),step(vec3(0.500001),rgb));
 vec3 result=(1.0-alpha)*dest.rgb+(1.0-dest.a)*rgb*alpha+alpha*dest.a*b;
 gl_FragColor=floor(clamp(vec4(result,alpha+dest.a-alpha*dest.a),0.0,1.0)*255.0+0.5)/255.0;
}";
    }
}
