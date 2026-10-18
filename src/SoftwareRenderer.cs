using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace IggyViewer {
    // GDraw ABI verified with the supplied x64 DLLs 1.2.30 and 1.2.56. All movie geometry,
    // triangulation, animation and ActionScript are evaluated by the native DLL.
    internal unsafe sealed class SoftwareRenderer : IDisposable {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr Nop();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void V1(IntPtr a);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void V2(IntPtr a, IntPtr b);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void V3(IntPtr a, IntPtr b, IntPtr c);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void DrawCall(IntPtr style, IntPtr batch, IntPtr buffer, IntPtr error);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void Tile(int x, int y, int right, int bottom, int border, IntPtr error);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int MakeTexture(IntPtr owner, int w, int h, int format, int dynamic, IntPtr result, IntPtr error);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int MakeBuffer(IntPtr owner, int type, int vertices, int indices, IntPtr result, IntPtr error);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr EndMake(IntPtr result, IntPtr error);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Lock(IntPtr handle, IntPtr owner, IntPtr error);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int BeginTarget(IntPtr rect, int format, int flags, int samples, IntPtr error);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr EndTarget(IntPtr error);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr LoadResource(IntPtr bytes, int length, IntPtr metadata);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void UpdateTexture(IntPtr handle, IntPtr owner, int x, int y, int w, int stride, int h, IntPtr source, int format);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void Filter(IntPtr style, int left, int top, int right, int bottom, IntPtr error);

        sealed class Resource {
            public IntPtr Data, Indices;
            public int Width, Height, VertexBytes, IndexBytes;
            public int OriginX, OriginY;
            public uint[] Pixels;
            public FocalMorph Focal;
            public bool Gradient;
            public bool Drawn;
            public bool Clipped;
            public uint Texture;
            public OpenGlDevice.Target Surface;
        }
        sealed class Surface {
            public int Width, Height, OriginX, OriginY;
            public uint[] Pixels;
            public byte[] Stencil;
            public OpenGlDevice.Target Gpu;
            public bool GpuNewer,CpuNewer;
            public Surface(int w, int h) { Width=w; Height=h; Pixels=new uint[w*h]; Stencil=new byte[w*h]; }
        }
        internal struct Vertex { public float X, Y, U, V, Coverage; }
        readonly Log log;
        readonly List<Delegate> callbacks = new List<Delegate>();
        readonly Dictionary<IntPtr, Resource> resources = new Dictionary<IntPtr, Resource>();
        readonly Dictionary<IntPtr, IntPtr> pending = new Dictionary<IntPtr, IntPtr>();
        readonly Dictionary<IntPtr,FocalMorph> focalOwners=new Dictionary<IntPtr,FocalMorph>();
        readonly HashSet<IntPtr> gradientOwners=new HashSet<IntPtr>();
        readonly Dictionary<IntPtr,List<FocalMorph>> focalDefinitions=new Dictionary<IntPtr,List<FocalMorph>>();
        readonly HashSet<string> notices = new HashSet<string>();
        readonly Stack<Surface> targets = new Stack<Surface>();
        readonly float[] projection=new float[12];
        bool projected;
        OpenGlDevice gpu;
        bool gpuBatch;
        public bool HardwareAccelerated {get{return gpu!=null;}}
        public string BackendName {get{return gpu==null?"Software renderer":"OpenGL renderer";}}
        public string HardwareDescription {get{return gpu==null?"Software":gpu.Description;}}
        public long HardwareBatches {get;private set;}
        public long SoftwareBatches {get;private set;}
        public long Readbacks {get{return gpu==null?0:gpu.Readbacks;}}
        internal int HardwareTargetCount {get{return gpu==null?0:gpu.TargetCount;}}
        Surface screen, target;
        int nextHandle = 4096;
        int clipX, clipY, clipRight, clipBottom;
        readonly HashSet<int> formats=new HashSet<int>();
        public IntPtr Interface { get; private set; }
        public long Triangles { get; private set; }
        public bool CorrectFocalMorphs=true;
        public int CorrectedFocalBatches {get;private set;}
        public float LastFocalRatio {get;private set;}
        public float LastFocalFocus {get;private set;}
        public int Background = unchecked((int)0xffb3dfff);
        public SoftwareRenderer(Log log) {
            this.log=log;
            Interface=Marshal.AllocHGlobal(35*8);
            for(int i=0;i<35;i++) Set(i, new Nop(delegate { return IntPtr.Zero; }));
            Set(0,new V1(delegate(IntPtr caps) {
                // Confirmed against machine code: RCX points to nine capability words.
                for(int i=0;i<9;i++)Marshal.WriteInt32(caps,i*4,0);
                Marshal.WriteInt32(caps,0,8);Marshal.WriteInt32(caps,4,256);
                // Each offscreen surface owns a separate stencil buffer.
                Marshal.WriteInt32(caps,28,1);
                BeginFrame();
            }));
            Set(1,new V2(delegate(IntPtr w,IntPtr h) { }));
            Set(2,new Tile(StartTile));
            Set(3,new V1(delegate(IntPtr e) { Error(e); }));
            Set(4,new V2(delegate(IntPtr n,IntPtr rgba) { }));
            Set(5,new V1(delegate(IntPtr bits) { ClearStencil(bits.ToInt32()); }));
            Set(6,new Nop(delegate { ClearStencil(255); return IntPtr.Zero; }));
            Set(7,new Filter(FilterQuad));
            Set(8,new DrawCall(Draw));
            Set(9,new MakeTexture(TextureBegin));
            Set(10,new V1(delegate(IntPtr result) { Notice("Unexpected paged texture upload"); }));
            Set(11,new EndMake(TextureEnd));
            Set(12,new MakeBuffer(BufferBegin));
            Set(14,new EndMake(BufferEnd));
            Set(15,new Lock(LockResource));
            Set(16,new Lock(LockResource));
            Set(17,new V1(delegate(IntPtr e) { Error(e); }));
            Set(18,new V3(delegate(IntPtr h,IntPtr o,IntPtr e) { Free(h); Error(e); }));
            Set(19,new V3(delegate(IntPtr h,IntPtr o,IntPtr e) { Free(h); Error(e); }));
            Set(20,new V3(delegate(IntPtr h,IntPtr o,IntPtr e) { Error(e); }));
            Set(21,new UpdateTexture(TextureUpdate));
            Set(22,new V3(delegate(IntPtr h,IntPtr o,IntPtr e) { Refresh(h); Error(e); }));
            Set(23,new BeginTarget(TargetBegin));
            Set(24,new EndTarget(TargetEnd));
            Set(25,new V2(TextureInfo));
            Set(26,new V2(BufferInfo));
            Set(27,new V3(delegate(IntPtr h,IntPtr o,IntPtr slot) { Free(h); }));
            Set(28,new V3(MaskBegin));
            Set(29,new V3(delegate(IntPtr rect,IntPtr slot,IntPtr e) { Error(e); }));
            Set(30,new Nop(delegate { return IntPtr.Zero; }));
            Set(31,new Nop(delegate { return IntPtr.Zero; }));
            Set(32,new LoadResource(ResourceTexture));
            Set(33,new V1(Free));
            // Reset is called unconditionally by 1.2.56 after a 3D subtree.
            // Faux-3D uses three float4 rows: x, y and the constant term.
            // The third output component is the perspective denominator.
            Set(34,new V1(delegate(IntPtr matrix) {
                projected=matrix!=IntPtr.Zero;
                if(projected)for(int i=0;i<projection.Length;i++)projection[i]=F(matrix,4*i);
            }));
            Resize(960,540);
        }
        void Set(int slot,Delegate callback) {
            callbacks.Add(callback);
            Marshal.WriteIntPtr(Interface,slot*8,Marshal.GetFunctionPointerForDelegate(callback));
        }
        static void Error(IntPtr ptr) { if(ptr!=IntPtr.Zero) *(ushort*)ptr=0; }
        void Notice(string text) { if(notices.Add(text)) log.Write("Renderer: "+text); }
        static int I(IntPtr p,int at) { return *((int*)((byte*)p+at)); }
        static IntPtr P(IntPtr p,int at) { return *((IntPtr*)((byte*)p+at)); }
        static float F(IntPtr p,int at) { return *((float*)((byte*)p+at)); }
        IntPtr Add(Resource r) { IntPtr h=new IntPtr(nextHandle++); resources.Add(h,r); return h; }
        public void SetBackend(string mode) {
            if(mode!="auto"&&mode!="opengl"&&mode!="software")throw new ArgumentException("Renderer must be auto, opengl or software.");
            if(mode=="software") {
                if(gpu==null)return;
                EnsureCpu(screen);foreach(Resource resource in resources.Values)EnsureResourceCpu(resource);
                ReleaseSurfaces();gpu.Dispose();gpu=null;
                foreach(Resource resource in resources.Values){resource.Texture=0;resource.Surface=null;}
                return;
            }
            if(gpu!=null)return;
            try {
                gpu=new OpenGlDevice(log);screen.Gpu=gpu.CreateTarget(screen.Width,screen.Height);screen.CpuNewer=true;
                target=screen;
            } catch(Exception ex) {
                if(gpu!=null){gpu.Dispose();gpu=null;}screen.Gpu=null;
                if(mode=="opengl")throw;
                log.Write("OpenGL unavailable; using software rendering: "+ex.Message);
            }
        }
        void EnsureGpu(Surface surface) {
            if(gpu==null)return;
            if(surface.Gpu==null){surface.Gpu=gpu.CreateTarget(surface.Width,surface.Height);surface.CpuNewer=true;}
            if(surface.CpuNewer){gpu.Upload(surface.Gpu,surface.Pixels);surface.CpuNewer=false;}
        }
        void EnsureCpu(Surface surface) {
            if(gpu!=null&&surface.GpuNewer){gpu.Read(surface.Gpu,surface.Pixels,null);surface.GpuNewer=false;}
        }
        void EnsureResourceCpu(Resource resource) {
            if(resource!=null&&resource.Pixels==null&&resource.Surface!=null){resource.Pixels=new uint[resource.Width*resource.Height];gpu.Read(resource.Surface,resource.Pixels,null);}
        }
        OpenGlDevice.Image GpuImage(Resource resource) {
            if(resource==null)return new OpenGlDevice.Image();
            if(resource.Surface==null&&resource.Texture==0)resource.Texture=gpu.CreateTexture(resource.Width,resource.Height,resource.Pixels);
            return new OpenGlDevice.Image {Texture=resource.Surface!=null?resource.Surface.Color:resource.Texture,Width=resource.Width,Height=resource.Height,X=resource.OriginX,Y=resource.OriginY,Flipped=resource.Surface!=null};
        }
        void ReleaseSurfaces() {
            if(gpu==null)return;
            HashSet<Surface> seen=new HashSet<Surface>(targets);seen.Add(screen);seen.Add(target);
            foreach(Surface surface in seen)if(surface!=null){gpu.DeleteTarget(surface.Gpu);surface.Gpu=null;surface.GpuNewer=false;surface.CpuNewer=false;}
        }
        public bool Present(IntPtr canvas,int width,int height) {
            if(gpu==null)return false;
            EnsureGpu(screen);gpu.Present(screen.Gpu,canvas,width,height);return true;
        }
        public void FinishHardware(){if(gpu!=null)gpu.Finish();}
        public void Resize(int width,int height) {
            width=Math.Max(1,Math.Min(4096,width)); height=Math.Max(1,Math.Min(4096,height));
            ReleaseSurfaces();
            screen=new Surface(width,height); target=screen; targets.Clear(); ResetClip();
        }
        void ResetClip() { clipX=0;clipY=0;clipRight=target.Width;clipBottom=target.Height; }
        void BeginFrame() {
            projected=false;
            target=screen; targets.Clear(); ResetClip(); Triangles=0;CorrectedFocalBatches=0;
            HardwareBatches=0;SoftwareBatches=0;
            if(gpu!=null){EnsureGpu(screen);gpu.Clear(screen.Gpu,(uint)Background);screen.GpuNewer=true;screen.CpuNewer=false;return;}
            for(int i=0;i<screen.Pixels.Length;i++) screen.Pixels[i]=(uint)Background;
            Array.Clear(screen.Stencil,0,screen.Stencil.Length);
        }
        void StartTile(int x,int y,int right,int bottom,int border,IntPtr error) {
            clipX=Math.Max(0,x-border);clipY=Math.Max(0,y-border);
            clipRight=Math.Min(target.Width,right+border);clipBottom=Math.Min(target.Height,bottom+border);Error(error);
        }
        void MaskBegin(IntPtr rect,IntPtr slot,IntPtr error) {
            // The runtime assigns a stencil BIT, not a nesting depth. The draw
            // style supplies independent read and write masks for each batch.
            int bit=1 << (unchecked((int)slot.ToInt64()) & 7);Error(error);
            int left=Math.Max(0,I(rect,0)-target.OriginX),top=Math.Max(0,I(rect,4)-target.OriginY);
            int right=Math.Min(target.Width,I(rect,8)-target.OriginX),bottom=Math.Min(target.Height,I(rect,12)-target.OriginY);
            if(gpu!=null){EnsureGpu(target);gpu.ClearStencil(target.Gpu,bit,left,top,right,bottom);target.GpuNewer=true;return;}
            for(int y=top;y<bottom;y++)for(int x=left;x<right;x++) {
                int at=y*target.Width+x;target.Stencil[at]&=(byte)~bit;
            }
        }
        void ClearStencil(int bits) {
            if(gpu!=null){EnsureGpu(target);gpu.ClearStencil(target.Gpu,bits,0,0,target.Width,target.Height);target.GpuNewer=true;return;}
            byte keep=(byte)~bits;for(int i=0;i<target.Stencil.Length;i++)target.Stencil[i]&=keep;
        }
        int BufferBegin(IntPtr owner,int type,int vertices,int indices,IntPtr output,IntPtr error) {
            Error(error);
            if(vertices<0||indices<0||vertices>128*1024*1024||indices>128*1024*1024) return 0;
            Resource r=new Resource {VertexBytes=vertices,IndexBytes=indices,Data=Marshal.AllocHGlobal(Math.Max(1,vertices)),Indices=Marshal.AllocHGlobal(Math.Max(1,indices))};
            IntPtr h=Add(r);
            Marshal.WriteIntPtr(output,0,r.Data);Marshal.WriteIntPtr(output,8,r.Indices);pending[output]=h;
            return 1;
        }
        IntPtr BufferEnd(IntPtr output,IntPtr error) { Error(error);IntPtr h=pending[output];pending.Remove(output);return h; }
        int TextureBegin(IntPtr owner,int w,int h,int format,int dynamic,IntPtr output,IntPtr error) {
            Error(error);
            if(w<=0||h<=0||w>8192||h>8192||(long)w*h>32*1024*1024) return 0;
            Resource r=new Resource {Width=w,Height=h,Data=Marshal.AllocHGlobal(w*h*4)};
            r.Gradient=gradientOwners.Contains(owner);
            FocalMorph focal;if(focalOwners.TryGetValue(owner,out focal))r.Focal=focal;
            for(int i=0;i<w*h;i++) ((uint*)r.Data)[i]=0;
            IntPtr handle=Add(r);
            Marshal.WriteIntPtr(output,0,r.Data); Marshal.WriteInt32(output,8,h);Marshal.WriteInt32(output,12,w*4);
            Marshal.WriteInt32(output,16,0);pending[output]=handle;
            return 1;
        }
        IntPtr TextureEnd(IntPtr output,IntPtr error) {
            Error(error);IntPtr h=pending[output];pending.Remove(output);Refresh(h);return h;
        }
        void Refresh(IntPtr handle) {
            Resource r;if(!resources.TryGetValue(handle,out r)||r.Width<=0)return;
            if(gpu!=null)gpu.DeleteTexture(r.Texture);r.Texture=0;
            r.Pixels=new uint[r.Width*r.Height];
            for(int i=0;i<r.Pixels.Length;i++) { uint rgba=((uint*)r.Data)[i];r.Pixels[i]=(rgba&0xff00ff00)|((rgba&255)<<16)|((rgba>>16)&255); }
        }
        int LockResource(IntPtr handle,IntPtr owner,IntPtr error) {
            Error(error);Resource r;
            if(!resources.TryGetValue(handle,out r))return 0;
            // In this DLL a cached four-XY-vertex rectangle can contain baked
            // scaling-grid coordinates from a different instance. After its
            // draw is consumed synchronously, decline reuse so the native
            // tessellator rebuilds it for the current instance. Other vertex
            // buffers and textures retain their normal cache behavior.
            if(r.Drawn&&r.VertexBytes==4*8&&r.IndexBytes==6*2){Free(handle);return 0;}
            return 1;
        }
        void BufferInfo(IntPtr h,IntPtr info) {
            Resource r;if(!resources.TryGetValue(h,out r))return;
            Marshal.WriteInt32(info,0,r.VertexBytes+r.IndexBytes);Marshal.WriteInt32(info,4,0);
        }
        void TextureInfo(IntPtr h,IntPtr info) {
            Resource r;if(!resources.TryGetValue(h,out r))return;
            Marshal.WriteInt16(info,0,(short)r.Width);Marshal.WriteInt16(info,2,0);
            Marshal.WriteInt16(info,4,(short)r.Height);Marshal.WriteInt16(info,6,0);Marshal.WriteInt32(info,8,r.Width*r.Height*4);
        }
        void TextureUpdate(IntPtr handle,IntPtr owner,int x,int y,int w,int stride,int h,IntPtr source,int format) {
            Resource r;if(!resources.TryGetValue(handle,out r)||source==IntPtr.Zero)return;
            if(r.Data==IntPtr.Zero) {
                r.Data=Marshal.AllocHGlobal(r.Width*r.Height*4);
                for(int i=0;i<r.Width*r.Height;i++){uint argb=r.Pixels==null?0:r.Pixels[i];((uint*)r.Data)[i]=(argb&0xff00ff00)|((argb&255)<<16)|((argb>>16)&255);}
            }
            for(int row=0;row<h;row++)for(int col=0;col<w;col++) {
                int px=x+col,py=y+row;
                if(px>=0&&py>=0&&px<r.Width&&py<r.Height) {
                    uint c;
                    if(format==1) { byte a=*((byte*)source+row*stride+col); c=(uint)(a<<24|a<<16|a<<8|a); }
                    else c=*(uint*)((byte*)source+row*stride+col*4);
                    ((uint*)r.Data)[py*r.Width+px]=c;
                }
            }
        }
        int TargetBegin(IntPtr rect,int format,int flags,int samples,IntPtr error) {
            Error(error);int left=I(rect,0),top=I(rect,4),right=I(rect,8),bottom=I(rect,12);
            int w=right-left,h=bottom-top;if(w<=0||h<=0||w>4096||h>4096)return 0;
            targets.Push(target); target=new Surface(w,h);target.OriginX=left;target.OriginY=top;ResetClip();return 1;
        }
        IntPtr TargetEnd(IntPtr error) {
            Error(error);if(targets.Count==0)return IntPtr.Zero;
            Surface s=target;
            if(gpu!=null)EnsureGpu(s);
            target=targets.Pop();ResetClip();
            return Add(new Resource {Width=s.Width,Height=s.Height,OriginX=s.OriginX,OriginY=s.OriginY,Pixels=gpu==null?s.Pixels:null,Surface=s.Gpu});
        }
        void FilterQuad(IntPtr style,int left,int top,int right,int bottom,IntPtr error) {
            Error(error);
            if(((byte*)style)[12]==4){ApplyFilter(style,left,top,right,bottom);return;}
            Resource cropped;
            // Faux-3D filter callbacks retain a +/-5,000,000 bounds sentinel,
            // even though BeginTarget already supplied a finite viewport crop.
            // Blit that crop at its actual origin rather than stretch it across
            // the sentinel rectangle. Keep this extent through filter chains.
            if(resources.TryGetValue(P(style,32),out cropped)&&(cropped.Clipped||right-left>4096||bottom-top>4096)) {
                left=cropped.OriginX;top=cropped.OriginY;right=left+cropped.Width;bottom=top+cropped.Height;
            }
            // FilterQuad is a screen-space blit. Its texture coordinates are
            // implicit; the fill matrices at +104 are not initialized here.
            byte* copy=stackalloc byte[240];for(int i=0;i<240;i++)copy[i]=((byte*)style)[i];
            copy[8]=1;
            Vertex a=new Vertex {X=left-target.OriginX,Y=top-target.OriginY,U=0,V=0,Coverage=1};
            Vertex b=new Vertex {X=right-target.OriginX,Y=top-target.OriginY,U=1,V=0,Coverage=1};
            Vertex c=new Vertex {X=right-target.OriginX,Y=bottom-target.OriginY,U=1,V=1,Coverage=1};
            Vertex d=new Vertex {X=left-target.OriginX,Y=bottom-target.OriginY,U=0,V=1,Coverage=1};
            BeginBatch((IntPtr)copy,2);
            try {Triangle((IntPtr)copy,a,b,c,2);Triangle((IntPtr)copy,a,c,d,2);}
            finally {EndBatch();}
        }
        static uint Pixel(Resource r,int x,int y) {
            x-=r.OriginX;y-=r.OriginY;
            return x<0||y<0||x>=r.Width||y>=r.Height?0:r.Pixels[y*r.Width+x];
        }
        void ApplyFilter(IntPtr style,int left,int top,int right,int bottom) {
            Resource source;if(!resources.TryGetValue(P(style,32),out source)||source.Pixels==null&&source.Surface==null)return;
            bool clipped=source.Clipped||right-left>4096||bottom-top>4096;
            if(clipped) {
                left=source.OriginX;top=source.OriginY;right=left+source.Width;bottom=top+source.Height;
            }
            int effect=((byte*)style)[14],w=right-left,h=bottom-top;
            if(w<1||h<1)return;
            if(w>4096||h>4096){Notice("Filter surface exceeds 4096 pixels ("+w+" x "+h+")");return;}
            if(gpu!=null&&ApplyGpuFilter(style,source,left,top,w,h,effect,clipped))return;
            EnsureResourceCpu(source);
            Resource extra;if(resources.TryGetValue(P(style,40),out extra))EnsureResourceCpu(extra);if(resources.TryGetValue(P(style,48),out extra))EnsureResourceCpu(extra);
            Resource output=new Resource {Width=w,Height=h,OriginX=left,OriginY=top,Pixels=new uint[w*h]};
            if(effect==0) {
                int rx=Math.Max(0,Math.Min(4096,(int)Math.Ceiling((F(style,152)-1)/2)));
                int ry=Math.Max(0,Math.Min(4096,(int)Math.Ceiling((F(style,156)-1)/2)));
                int passes=I(style,20)&255;
                if(passes==0||rx==0&&ry==0)return;
                // The callback bounds describe the INPUT. GDraw expects the
                // result expanded by the blur radius for each quality pass.
                // Its caller already computes that expanded rectangle for
                // subsequent filters and the final screen-space blit.
                int growX=rx*passes,growY=ry*passes;
                w+=growX*2;h+=growY*2;left-=growX;top-=growY;
                if(w>4096||h>4096){Notice("Expanded filter surface exceeds 4096 pixels");return;}
                output=new Resource {Width=w,Height=h,OriginX=left,OriginY=top,Pixels=new uint[w*h]};
                // Separable box blur over premultiplied RGBA, with transparent
                // samples beyond the source bounds.
                uint[] horizontal=new uint[w*(h+2*ry)];
                for(int pass=0;pass<passes;pass++) {
                    for(int y=-ry;y<h+ry;y++) {
                        int a=0,r=0,g=0,b=0,n=2*rx+1;
                        for(int x=-rx;x<=rx;x++)Accumulate(Pixel(source,left+x,top+y),1,ref a,ref r,ref g,ref b);
                        for(int x=0;x<w;x++) {
                            horizontal[(y+ry)*w+x]=Pack(a/n,r/n,g/n,b/n);
                            Accumulate(Pixel(source,left+x-rx,top+y),-1,ref a,ref r,ref g,ref b);
                            Accumulate(Pixel(source,left+x+rx+1,top+y),1,ref a,ref r,ref g,ref b);
                        }
                    }
                    for(int x=0;x<w;x++) {
                        int a=0,r=0,g=0,b=0,n=2*ry+1;
                        for(int y=0;y<n;y++)Accumulate(horizontal[y*w+x],1,ref a,ref r,ref g,ref b);
                        for(int y=0;y<h;y++) {
                            output.Pixels[y*w+x]=Pack(a/n,r/n,g/n,b/n);
                            if(y+1<h){Accumulate(horizontal[y*w+x],-1,ref a,ref r,ref g,ref b);Accumulate(horizontal[(y+n)*w+x],1,ref a,ref r,ref g,ref b);}
                        }
                    }
                    source=output;
                    if(pass+1<passes)output=new Resource {Width=w,Height=h,OriginX=left,OriginY=top,Pixels=new uint[w*h]};
                }
            } else if(effect==1) {
                // GDraw rearranges SWF's 4x5 matrix into a row-major 4x4
                // matrix at +160 followed by four normalized offsets at +224.
                for(int y=0;y<h;y++)for(int x=0;x<w;x++) {
                    uint pixel=Pixel(source,left+x,top+y);float a=(pixel>>24)/255f;
                    float r=a>0?((pixel>>16)&255)/(255*a):0,g=a>0?((pixel>>8)&255)/(255*a):0,b=a>0?(pixel&255)/(255*a):0;
                    int[] rgba=new int[4];
                    for(int row=0;row<4;row++){int p=160+row*16;rgba[row]=Clamp(r*F(style,p)+g*F(style,p+4)+b*F(style,p+8)+a*F(style,p+12)+F(style,224+row*4));}
                    output.Pixels[y*w+x]=Pack(rgba[3],rgba[0]*rgba[3]/255,rgba[1]*rgba[3]/255,rgba[2]*rgba[3]/255);
                }
            } else if(effect==3||effect==2) {
                Resource original;if(!resources.TryGetValue(P(style,40),out original))original=source;
                // GDraw flags: knockout=1, gradient=2, inner=4, full=8.
                // In particular, full does not mean hide the original object.
                int flags=((byte*)style)[15];bool inner=(flags&4)!=0,knockout=(flags&1)!=0,full=(flags&8)!=0;
                float dx=F(style,176),dy=F(style,180),strength=F(style,184);
                Resource gradient;resources.TryGetValue(P(style,48),out gradient);
                for(int y=0;y<h;y++)for(int x=0;x<w;x++) {
                    int wx=left+x,wy=top+y;uint obj=Pixel(original,wx,wy);
                    float oa=(obj>>24)/255f,mask=SampleAlpha(source,wx-dx,wy-dy);
                    float red=F(style,160),green=F(style,164),blue=F(style,168),alpha=F(style,172);
                    if(effect==2) {
                        float opposite=SampleAlpha(source,wx+dx,wy+dy);
                        float difference=mask-opposite;
                        if(difference<0){red=F(style,192);green=F(style,196);blue=F(style,200);alpha=F(style,204);}
                        mask=difference;
                    } else if(inner&&gradient==null)mask=1-mask;
                    if(gradient!=null&&gradient.Pixels!=null) {
                        // Bevel uses both halves of its palette: shadow at 0,
                        // transparent midpoint, highlight at 1. Its palette
                        // already supplies coverage and premultiplied RGB.
                        float coordinate=effect==2?0.5f-0.5f*Math.Max(-1,Math.Min(1,mask*strength)):Math.Max(0,Math.Min(1,mask*strength));
                        uint p=Bilinear(gradient,coordinate,0.5f,0);
                        alpha=(p>>24)/255f;red=((p>>16)&255)/255f;green=((p>>8)&255)/255f;blue=(p&255)/255f;
                    } else {
                        mask=Math.Max(0,Math.Min(1,Math.Abs(mask)*strength));
                        alpha*=mask;red*=mask;green*=mask;blue*=mask;
                    }
                    uint filtered=Pack(Clamp(alpha),Clamp(red),Clamp(green),Clamp(blue));
                    if(knockout)output.Pixels[y*w+x]=Scale(filtered,full?1:inner?oa:1-oa);
                    else if(full)output.Pixels[y*w+x]=Over(filtered,obj);
                    else if(inner)output.Pixels[y*w+x]=Atop(filtered,obj);
                    else output.Pixels[y*w+x]=Over(obj,filtered);
                }
            } else {
                // Preserve the source when a backend filter is not implemented.
                // The warning is visible in the log rather than drawing a box.
                Notice("Filter effect "+effect+" is not implemented; using its source image");
                return;
            }
            output.Clipped=clipped;
            IntPtr replacement=Add(output);Marshal.WriteIntPtr(style,32,replacement);
        }
        bool ApplyGpuFilter(IntPtr style,Resource source,int left,int top,int width,int height,int effect,bool clipped) {
            if(effect<0||effect>3)return false;
            Resource output=null;
            if(effect==0) {
                int rx=Math.Max(0,Math.Min(4096,(int)Math.Ceiling((F(style,152)-1)/2))),ry=Math.Max(0,Math.Min(4096,(int)Math.Ceiling((F(style,156)-1)/2))),passes=I(style,20)&255;
                if(passes==0||rx==0&&ry==0)return true;
                // Very large box kernels remain linear-time on the CPU rather
                // than issue thousands of texture samples per GPU fragment.
                if(rx>64||ry>64)return false;
                width+=2*rx*passes;height+=2*ry*passes;left-=rx*passes;top-=ry*passes;
                if(width>4096||height>4096)return false;
                OpenGlDevice.Image input=GpuImage(source);OpenGlDevice.Target previous=null;
                try {
                    for(int pass=0;pass<passes;pass++) {
                        OpenGlDevice.Target horizontal=gpu.CreateTarget(width,height+2*ry,false),vertical=null;
                        try {
                            gpu.Filter(horizontal,left,top-ry,input,input,new OpenGlDevice.Image(),style,0,rx);
                            vertical=gpu.CreateTarget(width,height,false);
                            OpenGlDevice.Image row=new OpenGlDevice.Image {Texture=horizontal.Color,Width=width,Height=height+2*ry,X=left,Y=top-ry,Flipped=true};
                            gpu.Filter(vertical,left,top,row,row,new OpenGlDevice.Image(),style,0,ry,true);
                        } catch {gpu.DeleteTarget(vertical);throw;} finally {gpu.DeleteTarget(horizontal);}
                        gpu.DeleteTarget(previous);previous=vertical;
                        input=new OpenGlDevice.Image {Texture=vertical.Color,Width=width,Height=height,X=left,Y=top,Flipped=true};
                    }
                    output=new Resource {Width=width,Height=height,OriginX=left,OriginY=top,Surface=previous};previous=null;
                } finally {gpu.DeleteTarget(previous);}
            } else {
                Resource original,palette;if(!resources.TryGetValue(P(style,40),out original))original=source;resources.TryGetValue(P(style,48),out palette);
                OpenGlDevice.Target result=gpu.CreateTarget(width,height,false);
                try {gpu.Filter(result,left,top,GpuImage(source),GpuImage(original),GpuImage(palette),style,effect);}
                catch {gpu.DeleteTarget(result);throw;}
                output=new Resource {Width=width,Height=height,OriginX=left,OriginY=top,Surface=result};
            }
            output.Clipped=clipped;Marshal.WriteIntPtr(style,32,Add(output));return true;
        }
        static uint Pack(int a,int r,int g,int b){return (uint)(a<<24|r<<16|g<<8|b);}
        static float SampleAlpha(Resource source,float x,float y) {
            int ix=(int)Math.Floor(x),iy=(int)Math.Floor(y);float fx=x-ix,fy=y-iy;
            float top=(Pixel(source,ix,iy)>>24)*(1-fx)+(Pixel(source,ix+1,iy)>>24)*fx;
            float bottom=(Pixel(source,ix,iy+1)>>24)*(1-fx)+(Pixel(source,ix+1,iy+1)>>24)*fx;
            return (top*(1-fy)+bottom*fy)/255f;
        }
        static uint Scale(uint p,float scale){return Pack((int)((p>>24)*scale+0.5f),(int)(((p>>16)&255)*scale+0.5f),(int)(((p>>8)&255)*scale+0.5f),(int)((p&255)*scale+0.5f));}
        static uint Atop(uint src,uint dst) {
            // Inner effects retain the original alpha even at translucent edges.
            uint effect=Scale(src,(dst>>24)/255f),rest=Scale(dst,1-(src>>24)/255f);
            return Pack((int)(dst>>24),Math.Min(255,(int)((effect>>16)&255)+(int)((rest>>16)&255)),Math.Min(255,(int)((effect>>8)&255)+(int)((rest>>8)&255)),Math.Min(255,(int)(effect&255)+(int)(rest&255)));
        }
        static uint Over(uint src,uint dst){int inverse=255-(int)(src>>24);return Pack((int)(src>>24)+(int)(dst>>24)*inverse/255,(int)((src>>16)&255)+(int)((dst>>16)&255)*inverse/255,(int)((src>>8)&255)+(int)((dst>>8)&255)*inverse/255,(int)(src&255)+(int)(dst&255)*inverse/255);}
        static void Accumulate(uint p,int sign,ref int a,ref int r,ref int g,ref int b){a+=sign*(int)(p>>24);r+=sign*(int)((p>>16)&255);g+=sign*(int)((p>>8)&255);b+=sign*(int)(p&255);}
        IntPtr ResourceTexture(IntPtr bytes,int length,IntPtr metadata) {
            try {
                int offset=I(metadata,0),format=(ushort)Marshal.ReadInt16(metadata,4);
                int w=(ushort)Marshal.ReadInt16(metadata,6),h=(ushort)Marshal.ReadInt16(metadata,8);
                if(offset<0||w<1||h<1||(long)w*h>32*1024*1024)throw new InvalidOperationException("Invalid external texture dimensions");
                int required=ExternalTextureDecoder.DataLength(w,h,format);
                if(offset>length-required)throw new InvalidOperationException("External texture outside resource file");
                uint[] pixels=new uint[w*h];
                byte[] encoded=new byte[required];Marshal.Copy(IntPtr.Add(bytes,offset),encoded,0,required);
                int[] decoded=ExternalTextureDecoder.Decode(w,h,format,encoded);
                for(int i=0;i<pixels.Length;i++){uint p=(uint)decoded[i];int a=(int)(p>>24),r=((int)((p>>16)&255)*a+127)/255,g=((int)((p>>8)&255)*a+127)/255,b=((int)(p&255)*a+127)/255;pixels[i]=(uint)(a<<24|r<<16|g<<8|b);}
                return Add(new Resource {Width=w,Height=h,Pixels=pixels});
            } catch(Exception ex){Notice("Cannot load external texture: "+ex.Message);return IntPtr.Zero;}
        }
        Vertex VertexAt(IntPtr ptr,int index,int format,IntPtr transform) {
            int stride=format==0?8:16;byte* v=(byte*)ptr+index*stride;
            float x=*(float*)v,y=*(float*)(v+4);
            Vertex result=new Vertex {U=format==2?*(float*)(v+8):0,V=format==2?*(float*)(v+12):0,Coverage=1};
            if(transform!=IntPtr.Zero) {
                result.X=F(transform,0)*x+F(transform,4)*y+F(transform,16)-target.OriginX;
                result.Y=F(transform,8)*x+F(transform,12)*y+F(transform,20)-target.OriginY;
            } else { result.X=x-target.OriginX;result.Y=y-target.OriginY; }
            if(projected) {
                float px=result.X+target.OriginX,py=result.Y+target.OriginY;
                float w=projection[2]*px+projection[6]*py+projection[10];
                result.X=((projection[0]*px+projection[4]*py+projection[8])/w+1)*screen.Width/2-target.OriginX;
                result.Y=(1-(projection[1]*px+projection[5]*py+projection[9])/w)*screen.Height/2-target.OriginY;
            }
            if(format!=2) {result.U=x;result.V=y;}
            return result;
        }
        void Draw(IntPtr style,IntPtr batch,IntPtr handle,IntPtr error) {
            Error(error);
            byte* adjusted=stackalloc byte[240];
            if(CorrectFocalMorphs&&((byte*)style)[9]==4) {
                Resource texture;
                if(resources.TryGetValue(P(style,32),out texture)&&texture.Focal!=null) {
                    FocalMorph focal=texture.Focal;float ratio;double[] correction;
                    if(focal.TryRatio(F(style,104),F(style,108),F(style,120),F(style,124),out ratio)&&focal.TryTransform(ratio,out correction)) {
                        for(int i=0;i<240;i++)adjusted[i]=((byte*)style)[i];
                        int[] u={104,108,116},v={120,124,132};
                        for(int i=0;i<3;i++) {
                            *(float*)(adjusted+u[i])=(float)(correction[0]*F(style,u[i])+correction[1]*F(style,v[i]));
                            *(float*)(adjusted+v[i])=(float)(correction[2]*F(style,u[i])+correction[3]*F(style,v[i]));
                        }
                        float focus=Math.Max(-0.999f,Math.Min(0.999f,focal.Focus(ratio))),inverse=1/(1-focus*focus);
                        *(float*)(adjusted+136)=-focus;*(float*)(adjusted+140)=-focus*inverse;*(float*)(adjusted+144)=inverse;
                        style=(IntPtr)adjusted;CorrectedFocalBatches++;LastFocalRatio=ratio;LastFocalFocus=focus;
                    } else Notice("Cannot unambiguously correct focal morph "+focal.CharacterId+" for this batch; using native parameters");
                }
            }
            int count=I(batch,16),indices=I(batch,20),format=I(batch,24);
            if(count<0||indices<0||count>1000000||indices>3000000||format<0||format>2)return;
            Resource vb=null;IntPtr vertices=P(batch,0),indexPtr=P(batch,8);
            if(handle!=IntPtr.Zero) {
                if(!resources.TryGetValue(handle,out vb)) {Notice("Unknown vertex buffer");return;}
                long vo=vertices.ToInt64(),io=indexPtr.ToInt64();
                if(vo<0||io<0||vo>vb.VertexBytes-(long)count*(format==0?8:16)||io>vb.IndexBytes-(long)indices*2){Notice("Draw outside vertex/index buffer");return;}
                vertices=IntPtr.Add(vb.Data,(int)vo);indexPtr=IntPtr.Add(vb.Indices,(int)io);
            }
            if(vertices==IntPtr.Zero||(indices>0&&indexPtr==IntPtr.Zero))return;
            if(vb!=null)vb.Drawn=true;
            if(formats.Add(format))log.Write("Vertex format "+format);
            IntPtr matrix=(I(style,4)&4)!=0?IntPtr.Zero:P(style,64);
            BeginBatch(style,format);
            try {
            if(indices>0) {
                for(int i=0;i+2<indices;i+=3) {
                    int a=((ushort*)indexPtr)[i],b=((ushort*)indexPtr)[i+1],c=((ushort*)indexPtr)[i+2];
                    if(a>=count||b>=count||c>=count){Notice("Triangle index outside vertex batch");continue;}
                    Triangle(style,VertexAt(vertices,a,format,matrix),VertexAt(vertices,b,format,matrix),VertexAt(vertices,c,format,matrix),format);
                }
            } else {
                // Immediate geometry consists of independent quads.
                for(int i=0;i+3<count;i+=4) {
                    Vertex a=VertexAt(vertices,i,format,matrix),b=VertexAt(vertices,i+1,format,matrix),c=VertexAt(vertices,i+2,format,matrix),d=VertexAt(vertices,i+3,format,matrix);
                    Triangle(style,a,b,c,format);Triangle(style,a,c,d,format);
                }
            }
            } finally {EndBatch();}
        }
        void BeginBatch(IntPtr style,int format) {
            if(gpu==null) {
                SoftwareBatches++;return;
            }
            EnsureGpu(target);
            int left=clipX,top=clipY,right=clipRight,bottom=clipBottom;
            if((I(style,4)&8)!=0){left=Math.Max(left,I(style,88)-target.OriginX);top=Math.Max(top,I(style,92)-target.OriginY);right=Math.Min(right,I(style,96)-target.OriginX);bottom=Math.Min(bottom,I(style,100)-target.OriginY);}
            Resource texture;resources.TryGetValue(P(style,32),out texture);
            bool textured=Marshal.ReadByte(style,9)!=0&&texture!=null&&(texture.Pixels!=null||texture.Surface!=null);
            OpenGlDevice.Image image=textured?GpuImage(texture):new OpenGlDevice.Image();
            gpu.Begin(target.Gpu,style,image.Texture,textured?texture.Width:1,textured?texture.Height:1,textured&&texture.Gradient,image.Flipped,format,left,top,right,bottom);
            gpuBatch=true;target.GpuNewer=true;HardwareBatches++;
        }
        void EndBatch(){if(gpuBatch){gpu.End();gpuBatch=false;}}
        static float Edge(Vertex a,Vertex b,float x,float y) { return (x-a.X)*(b.Y-a.Y)-(y-a.Y)*(b.X-a.X); }
        static int Clamp(float x) { return Math.Max(0,Math.Min(255,(int)(x*255+0.5f))); }
        void Triangle(IntPtr style,Vertex a,Vertex b,Vertex c,int format) {
            float area=Edge(a,b,c.X,c.Y);if(Math.Abs(area)<0.00001||float.IsNaN(area)||float.IsInfinity(area))return;
            if(area<0){Vertex swap=b;b=c;c=swap;area=-area;}
            if(gpuBatch){Triangles++;gpu.Triangle(a,b,c);return;}
            int minX=Math.Max(clipX,(int)Math.Floor(Math.Min(a.X,Math.Min(b.X,c.X))));
            int minY=Math.Max(clipY,(int)Math.Floor(Math.Min(a.Y,Math.Min(b.Y,c.Y))));
            int maxX=Math.Min(clipRight-1,(int)Math.Ceiling(Math.Max(a.X,Math.Max(b.X,c.X))));
            int maxY=Math.Min(clipBottom-1,(int)Math.Ceiling(Math.Max(a.Y,Math.Max(b.Y,c.Y))));
            float mr=F(style,72),mg=F(style,76),mb=F(style,80),ma=F(style,84);
            if((I(style,4)&8)!=0) {
                // GDraw scissor bounds are signed integer pixel coordinates.
                minX=Math.Max(minX,I(style,88)-target.OriginX);minY=Math.Max(minY,I(style,92)-target.OriginY);
                maxX=Math.Min(maxX,I(style,96)-target.OriginX-1);maxY=Math.Min(maxY,I(style,100)-target.OriginY-1);
            }
            // +24 points directly to the four signed 16-bit additive terms
            // of the native CXFORM (+20), in byte-channel units, not floats.
            IntPtr add=P(style,24);
            float ar=add==IntPtr.Zero?0:Marshal.ReadInt16(add,0)/255f,ag=add==IntPtr.Zero?0:Marshal.ReadInt16(add,2)/255f;
            float ab=add==IntPtr.Zero?0:Marshal.ReadInt16(add,4)/255f,aa=add==IntPtr.Zero?0:Marshal.ReadInt16(add,6)/255f;
            Resource texture;resources.TryGetValue(P(style,32),out texture);
            int shader=((byte*)style)[9];bool useTexture=shader!=0&&texture!=null&&texture.Pixels!=null;
            int readMask=((byte*)style)[16],writeMask=((byte*)style)[17];
            if(shader!=0&&!useTexture)Notice("Missing texture for shader "+shader);
            Triangles++;
            for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++) {
                float e0=Edge(b,c,x+0.5f,y+0.5f),e1=Edge(c,a,x+0.5f,y+0.5f),e2=Edge(a,b,x+0.5f,y+0.5f);
                if(e0<0||e1<0||e2<0||(e0==0&&!TopLeft(b,c))||(e1==0&&!TopLeft(c,a))||(e2==0&&!TopLeft(a,b)))continue;
                float w0=e0/area,w1=e1/area,w2=e2/area;
                int at=y*target.Width+x;
                if((target.Stencil[at]&readMask)!=readMask)continue;
                float r=1,g=1,bl=1,alpha=1;
                if(useTexture) {
                    float u=w0*a.U+w1*b.U+w2*c.U,v=w0*a.V+w1*b.V+w2*c.V;
                    if(((byte*)style)[8]==0||format!=2) {
                        float tu=u*F(style,104)+v*F(style,108)+F(style,116);
                        v=u*F(style,120)+v*F(style,124)+F(style,132);u=tu;
                    }
                    if(shader==3) {u=(float)Math.Sqrt(u*u+v*v);v=0;}
                    if(shader==4) {u=FocalDistance(u,v,F(style,136),F(style,140),F(style,144));v=0;}
                    int address=((byte*)style)[10];
                    if(address==2){u=Mirror(u);v=Mirror(v);}
                    else if(address==1) {u-=(float)Math.Floor(u);v-=(float)Math.Floor(v);}else {u=Math.Max(0,Math.Min(0.999999f,u));v=Math.Max(0,Math.Min(0.999999f,v));}
                    int tx=Math.Max(0,Math.Min(texture.Width-1,(int)(u*texture.Width))),ty=Math.Max(0,Math.Min(texture.Height-1,(int)(v*texture.Height)));
                    // Spread folds the gradient parameter above. Its palette
                    // endpoints must not blend together through texture wrap:
                    // that creates a false dark point at a radial origin.
                    bool gradient=texture.Gradient||shader==3||shader==4;
                    uint pixel=((byte*)style)[11]!=0?texture.Pixels[ty*texture.Width+tx]:Bilinear(texture,u,v,gradient?0:address);
                    alpha=(pixel>>24)/255f;r=((pixel>>16)&255)/255f;g=((pixel>>8)&255)/255f;bl=(pixel&255)/255f;
                    if(alpha>0) {r/=alpha;g/=alpha;bl/=alpha;}
                }
                int sa=Clamp((alpha*ma+aa)*(w0*a.Coverage+w1*b.Coverage+w2*c.Coverage));if(sa==0)continue;
                if(writeMask!=0){target.Stencil[at]|=(byte)writeMask;continue;}
                int sr=Clamp(r*mr+ar),sg=Clamp(g*mg+ag),sb=Clamp(bl*mb+ab);
                uint dest=target.Pixels[at];int inverse=255-sa;
                int mode=((byte*)style)[13];
                if(mode!=0) {
                    target.Pixels[at]=Composite(sr,sg,sb,sa,dest,mode);continue;
                }
                int rr=(sr*sa+(int)((dest>>16)&255)*inverse+127)/255;
                int gg=(sg*sa+(int)((dest>>8)&255)*inverse+127)/255;
                int bb=(sb*sa+(int)(dest&255)*inverse+127)/255;
                int da=sa+((int)(dest>>24)*inverse+127)/255;
                target.Pixels[at]=(uint)(da<<24|rr<<16|gg<<8|bb);
            }
        }
        static bool TopLeft(Vertex a,Vertex b){return b.Y>a.Y||(b.Y==a.Y&&b.X<a.X);}
        internal static float FocalDistance(float x,float y,float negativeFocus,float coefficient,float inverse) {
            // The DLL supplies -f, -f/(1-f*f), 1/(1-f*f). Coordinates
            // are relative to the gradient circle's centre, not its focus.
            double dx=(double)x+negativeFocus;
            double projection=-(double)coefficient*dx/inverse;
            double root=Math.Sqrt(dx*dx+(double)y*y/inverse);
            // Rationalize when the two numerator terms would cancel close
            // to the unit circle, particularly for focal ratios near +/-1.
            if(projection<0)return (float)((dx*dx+(double)y*y)/(root-projection));
            return (float)((projection+root)*inverse);
        }
        static uint Composite(int r,int g,int b,int a,uint dest,int mode) {
            if(mode==11||mode==12){int factor=mode==11?255-a:a;return Pack((int)(dest>>24)*factor/255,(int)((dest>>16)&255)*factor/255,(int)((dest>>8)&255)*factor/255,(int)(dest&255)*factor/255);}
            float sa=a/255f,da=(dest>>24)/255f;
            int rr=BlendChannel(r/255f,((dest>>16)&255)/255f,sa,da,mode);
            int gg=BlendChannel(g/255f,((dest>>8)&255)/255f,sa,da,mode);
            int bb=BlendChannel(b/255f,(dest&255)/255f,sa,da,mode);
            return Pack(Clamp(sa+da-sa*da),rr,gg,bb);
        }
        static int BlendChannel(float s,float dp,float sa,float da,int mode) {
            float d=da>0?dp/da:0,b=s;
            switch(mode) {
                case 1:b=s*d;break;case 2:b=s+d-s*d;break;case 3:b=Math.Max(s,d);break;case 4:b=Math.Min(s,d);break;
                case 5:b=Math.Min(1,s+d);break;case 6:b=Math.Max(0,d-s);break;case 7:b=1-d;break;case 8:b=Math.Abs(d-s);break;
                case 9:b=d<=0.5f?2*s*d:1-2*(1-s)*(1-d);break;case 10:b=s<=0.5f?2*s*d:1-2*(1-s)*(1-d);break;
            }
            return Clamp((1-sa)*dp+(1-da)*s*sa+sa*da*b);
        }
        public Bitmap Snapshot() {
            EnsureCpu(screen);
            Bitmap bitmap=new Bitmap(screen.Width,screen.Height,PixelFormat.Format32bppPArgb);
            BitmapData data=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
            for(int y=0;y<screen.Height;y++) {
                uint* row=(uint*)((byte*)data.Scan0+y*data.Stride);
                for(int x=0;x<screen.Width;x++)row[x]=screen.Pixels[y*screen.Width+x];
            }
            bitmap.UnlockBits(data);return bitmap;
        }
        static float Mirror(float value) {value-=2*(float)Math.Floor(value/2);return value>1?2-value:value;}
        static int Address(int value,int size,int mode) {
            if(mode==1){value%=size;return value<0?value+size:value;}
            if(mode==2){value%=2*size;if(value<0)value+=2*size;return value>=size?2*size-1-value:value;}
            return Math.Max(0,Math.Min(size-1,value));
        }
        static uint Bilinear(Resource texture,float u,float v,int address) {
            float px=u*texture.Width-0.5f,py=v*texture.Height-0.5f;
            int x=(int)Math.Floor(px),y=(int)Math.Floor(py);float fx=px-x,fy=py-y;
            uint a=texture.Pixels[Address(y,texture.Height,address)*texture.Width+Address(x,texture.Width,address)];
            uint b=texture.Pixels[Address(y,texture.Height,address)*texture.Width+Address(x+1,texture.Width,address)];
            uint c=texture.Pixels[Address(y+1,texture.Height,address)*texture.Width+Address(x,texture.Width,address)];
            uint d=texture.Pixels[Address(y+1,texture.Height,address)*texture.Width+Address(x+1,texture.Width,address)];
            uint result=0;
            for(int shift=0;shift<32;shift+=8){float upper=((a>>shift)&255)*(1-fx)+((b>>shift)&255)*fx,lower=((c>>shift)&255)*(1-fx)+((d>>shift)&255)*fx;result|=(uint)(upper*(1-fy)+lower*fy+0.5f)<<shift;}
            return result;
        }
        void Free(IntPtr handle) {
            Resource r;if(!resources.TryGetValue(handle,out r))return;
            if(gpu!=null)gpu.DeleteTexture(r.Texture);
            if(gpu!=null)gpu.DeleteTarget(r.Surface);
            if(r.Data!=IntPtr.Zero)Marshal.FreeHGlobal(r.Data);
            if(r.Indices!=IntPtr.Zero)Marshal.FreeHGlobal(r.Indices);
            resources.Remove(handle);
        }
        public void RegisterFocal(IntPtr owner,FocalMorph focal) {
            focalOwners[owner]=focal;List<FocalMorph> fills;
            if(!focalDefinitions.TryGetValue(focal.Definition,out fills)){fills=new List<FocalMorph>();focalDefinitions.Add(focal.Definition,fills);}
            fills.Add(focal);
        }
        public void RegisterGradient(IntPtr owner){gradientOwners.Add(owner);}
        public void BeginMorphFrame(){foreach(FocalMorph focal in focalOwners.Values)focal.Ratios.Clear();}
        public void AddMorphInstance(IntPtr definition,float ratio){List<FocalMorph> fills;if(focalDefinitions.TryGetValue(definition,out fills))foreach(FocalMorph focal in fills)focal.AddRatio(ratio);}
        internal void ReleaseLoadedTexture(IntPtr handle) {Free(handle);}
        internal bool ReadBitmapPixels(IntPtr handle,IntPtr definition,int width,int height,uint[] rgba) {
            Resource resource;if(!resources.TryGetValue(handle,out resource)||resource.Pixels==null)return false;
            float sx=F(definition,48),sy=F(definition,52),ox=F(definition,56),oy=F(definition,60);
            for(int y=0;y<height;y++)for(int x=0;x<width;x++) {
                int px=Math.Max(0,Math.Min(resource.Width-1,(int)((ox+(x+0.5)*sx/width)*resource.Width))),py=Math.Max(0,Math.Min(resource.Height-1,(int)((oy+(y+0.5)*sy/height)*resource.Height)));
                uint p=resource.Pixels[py*resource.Width+px];rgba[y*width+x]=(p&0xff00ff00)|((p&255)<<16)|((p>>16)&255);
            }
            return true;
        }
        internal void UpdateBitmapPixels(IntPtr handle,IntPtr rgba,int stride,int x,int y,int width,int height) {
            Resource resource;if(!resources.TryGetValue(handle,out resource)||resource.Pixels==null)return;
            for(int row=y;row<y+height;row++)for(int col=x;col<x+width;col++) {
                uint pixel=((uint*)rgba)[row*stride+col];resource.Pixels[row*resource.Width+col]=(pixel&0xff00ff00)|((pixel&255)<<16)|((pixel>>16)&255);
                if(resource.Data!=IntPtr.Zero)((uint*)resource.Data)[row*resource.Width+col]=pixel;
            }
            if(gpu!=null)gpu.DeleteTexture(resource.Texture);resource.Texture=0;
        }
        public void ClearResources() { foreach(IntPtr key in new List<IntPtr>(resources.Keys))Free(key);focalOwners.Clear();focalDefinitions.Clear();gradientOwners.Clear(); }
        public void Dispose() { ClearResources();ReleaseSurfaces();if(gpu!=null){gpu.Dispose();gpu=null;}Marshal.FreeHGlobal(Interface);Interface=IntPtr.Zero;GC.KeepAlive(callbacks); }
    }
}
