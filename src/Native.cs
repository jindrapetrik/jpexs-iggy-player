using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;

namespace IggyViewer {
    internal sealed class Log {
        readonly string path;
        public readonly List<string> Messages = new List<string>();
        public Log(string path) { this.path = path; File.WriteAllText(path, "Iggy native player\r\n"); }
        public void Write(string text) {
            lock(Messages){Messages.Add(text);File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss.fff ") + text + "\r\n");}
        }
    }

    internal unsafe sealed partial class Native : IDisposable {
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr LoadLibraryEx(string name, IntPtr file, uint flags);
        [DllImport("kernel32", SetLastError = true)] static extern IntPtr GetProcAddress(IntPtr module, string name);
        [DllImport("kernel32")] static extern bool FreeLibrary(IntPtr module);
        [DllImport("kernel32")] internal static extern uint SetErrorMode(uint mode);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void Void0();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void VoidP(IntPtr a);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void VoidPP(IntPtr a, IntPtr b);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void VoidPI(IntPtr a, int b);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void VoidPII(IntPtr a, int b, int c);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void VoidII(int a,int b);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void FontCache(IntPtr a, int b, int c, int d, int e);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr PtrP(IntPtr a);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr DebugName(IntPtr buffer,IntPtr capacity);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void IntOption(int value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void InstallTrueType(IntPtr bytes,uint fontIndex,IntPtr name,int nameLength,byte style);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void FontNameStyle(IntPtr name,int nameLength,byte style);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr Info(int what);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr Create(IntPtr bytes, uint size, IntPtr options);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int CreateLibrary(IntPtr name, IntPtr bytes, uint size, IntPtr options);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr LibraryGeneric(int library);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void LibraryDestroy(int library);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void InstallResources(IntPtr generic,IntPtr bytes,uint length,IntPtr result);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void Warning(IntPtr user, IntPtr player, int code, IntPtr text);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void Trace(IntPtr user, IntPtr player, IntPtr text, int length);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Ready(IntPtr player);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void EventCall(IntPtr player, IntPtr ev, IntPtr result);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void MakeMouse(IntPtr ev, int x, int y);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void MakeKey(IntPtr ev, int key, int down);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void MakeKeyEvent(IntPtr ev, int type, int code, int location);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void SetFocus(IntPtr player,IntPtr target,int keyCode);
        readonly IntPtr module;
        readonly int rootInstanceOffset,stageInstanceOffset;
        readonly bool modernDll,correctLoaderInfo;
        LoaderInfoCompatibility loaderInfo;
        RuntimeCompatibility runtimeCompatibility;
        SoundCompatibility soundCompatibility;
        internal int TrackedSoundChannels {get{return soundCompatibility==null?0:soundCompatibility.Active;}}
        CursorCompatibility cursorCompatibility;
        internal bool CorrectZeroScale=true;
        internal int LastAdvancedFrames;
        RuntimeScriptPlan runtimeScripts;
        SharedObjectStorage sharedObjectStorage;
        ImageLoader imageLoader;
        CompilerLifetime compilerLifetime;
        internal MovieActivity Activity;
        internal long LoadedImageBytes {get{return imageLoader==null?0:imageLoader.AllocatedBytes;}}
        internal string StorageDirectory=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"sharedobjects");
        internal int LoaderInfoStageReads {get{return loaderInfo==null?0:loaderInfo.StageReads;}}
        readonly List<Delegate> bindings = new List<Delegate>();
        readonly Log log;
        readonly Warning warning;
        readonly Trace trace;
        internal readonly Mp3Decoder Mp3;
        internal readonly bool AudioEnabled;
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void AudioVolume(IntPtr player,float volume);
        readonly AudioVolume audioVolume;
        internal bool Muted {get;private set;}
        readonly Void0 shutdown;
        readonly Void0 flushInstalledFonts;
        readonly FontNameStyle removeFont;
        readonly InstallTrueType installTrueType;
        readonly IntOption installedFontMaxCount;
        readonly Create create;
        readonly CreateLibrary createLibrary;
        readonly LibraryGeneric libraryGeneric;
        readonly LibraryDestroy libraryDestroy;
        readonly InstallResources installResources;
        readonly List<int> libraries=new List<int>();
        readonly Dictionary<string,string> loadedUrls=new Dictionary<string,string>(StringComparer.Ordinal);
        readonly Dictionary<string,IntPtr> deviceFontData=new Dictionary<string,IntPtr>(StringComparer.OrdinalIgnoreCase);
        readonly List<KeyValuePair<string,byte>> installedDeviceFonts=new List<KeyValuePair<string,byte>>();
        readonly HashSet<string> embeddedFonts=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly VoidP destroy, initialize, play, tick, draw;
        readonly VoidP updateTickWithoutRender;
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void SetTime(IntPtr player,double seconds);
        readonly SetTime setTime;
        readonly Stopwatch wallClock=new Stopwatch();
        internal bool UseWallClock=true;
        readonly VoidPP backgroundColor;
        readonly FontCache fontCache;
        readonly VoidPI antiAlias, pause;
        readonly VoidPII display;
        readonly VoidPII goToFrame;
        readonly Ready ready, valid;
        readonly PtrP properties;
        readonly PtrP focusedText;
        readonly SetFocus setFocus;
        readonly EventCall dispatch;
        readonly MakeMouse mouseMove;
        readonly MakeKeyEvent key;
        readonly VoidPI mouseButton;
        readonly VoidPI character, mouseWheel;
        readonly VoidP mouseLeave,focusLost;
        readonly Dictionary<int,int> pressedKeys=new Dictionary<int,int>();
        public readonly SoftwareRenderer Renderer;
        public IntPtr Player { get; private set; }
        public string Version { get; private set; }
        public string FileName { get; private set; }
        public int StageWidth { get; private set; }
        public int StageHeight { get; private set; }
        public int TotalFrames { get { return Player==IntPtr.Zero?1:Math.Max(1,Marshal.ReadInt32(Player,104)); } }
        public int CurrentFrame { get { return Player==IntPtr.Zero?1:Math.Max(1,Marshal.ReadInt32(Player,rootInstanceOffset+368)+1); } }
        public bool TextFieldFocused {get{return Player!=IntPtr.Zero&&focusedText(Player)!=IntPtr.Zero;}}
        bool mouseInside;
        // AS3's request survives mouse leave and applies immediately on load;
        // the host confines it to its canvas window, including WM_SETCURSOR.
        public bool HideSystemCursor {get{return Player!=IntPtr.Zero&&cursorCompatibility!=null&&cursorCompatibility.Hidden;}}
        public bool HandCursor {
            get {
                if(Player==IntPtr.Zero||!mouseInside)return false;
                // Reuse the DLL's hit test, including masks, hit areas, button
                // states and mouseChildren. Do not keep native instance pointers
                // across calls: AS3 can remove objects or collect them.
                IntPtr target=Marshal.ReadIntPtr(Player,modernDll?10752:10672);
                bool hand=false;
                for(int depth=0;target!=IntPtr.Zero&&depth<100;depth++,target=Marshal.ReadIntPtr(target,128)) {
                    if(target==IntPtr.Add(Player,stageInstanceOffset))return hand;
                    byte type=Marshal.ReadByte(target,3);
                    if((type==3||type==7)&&(cursorCompatibility!=null?cursorCompatibility.IsButton(target):type==7&&(Marshal.ReadByte(target)&0x20)!=0))hand=true;
                }
                return false;
            }
        }
        bool initialized,disposed,stoppedBySeek;
        IntPtr emptyStyles;

        internal T Bind<T>(string name) where T : class {
            IntPtr ptr = GetProcAddress(module, name);
            if (ptr == IntPtr.Zero) throw new InvalidOperationException("Missing DLL export: " + name);
            Delegate fn = Marshal.GetDelegateForFunctionPointer(ptr, typeof(T));
            bindings.Add(fn);
            return (T)(object)fn;
        }
        internal static string Utf8(IntPtr ptr) {
            if (ptr == IntPtr.Zero) return "";
            byte* b = (byte*)ptr; int n = 0;
            while (n < 1024 * 1024 && b[n] != 0) n++;
            byte[] bytes = new byte[n]; Marshal.Copy(ptr, bytes, 0, n);
            return Encoding.UTF8.GetString(bytes);
        }
        public Native(string dll, Log log,bool correctFocalMorphs=true,bool correctLoaderInfo=true,string renderer="software",bool audio=false) {
            this.log = log;
            this.correctLoaderInfo=correctLoaderInfo;
            if (!Environment.Is64BitProcess) throw new InvalidOperationException("A 64-bit process is required.");
            module = LoadLibraryEx(Path.GetFullPath(dll), IntPtr.Zero, 0x100 | 0x800);
            if (module == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Cannot load " + dll);
            try {
            Version = Utf8(Bind<Info>("IggyGetInfo")(0)).Trim();
            log.Write("DLL: "+Path.GetFullPath(dll));
            log.Write(Version);
            bool modern=Version.StartsWith("Iggy version 1.2.56\n");
            modernDll=modern;
            if (!modern&&!Version.StartsWith("Iggy version 1.2.30\n")) {
                throw new NotSupportedException("This GDraw binding supports the supplied x64 Iggy 1.2.30 and 1.2.56 DLLs only.");
            }
            if(correctLoaderInfo)LoaderInfoCompatibility.VerifyBinary(dll,modern);
            rootInstanceOffset=modern?9488:9408;stageInstanceOffset=modern?9904:9824;
            shutdown = Bind<Void0>("IggyShutdown");
            flushInstalledFonts=Bind<Void0>("IggyFlushInstalledFonts");
            removeFont=Bind<FontNameStyle>("IggyFontRemoveUTF16");
            installTrueType=Bind<InstallTrueType>("IggyFontInstallTruetypeUTF16");
            installedFontMaxCount=Bind<IntOption>("IggySetInstalledFontMaxCount");
            create = Bind<Create>("IggyPlayerCreateFromMemory");
            createLibrary=Bind<CreateLibrary>("IggyLibraryCreateFromMemory");
            libraryGeneric=Bind<LibraryGeneric>("IggyLibraryGetGeneric");
            libraryDestroy=Bind<LibraryDestroy>("IggyLibraryDestroy");
            installResources=Bind<InstallResources>("IggyGenericInstallResourceFile");
            destroy = Bind<VoidP>("IggyPlayerDestroy");
            initialize = Bind<VoidP>("IggyPlayerInitializeAndTickRS");
            play = Bind<VoidP>("IggyPlayerPlay");
            tick = Bind<VoidP>("IggyPlayerTickRS");
            draw = Bind<VoidP>("IggyPlayerDraw");
            backgroundColor=Bind<VoidPP>("IggyPlayerGetBackgroundColor");
            fontCache=Bind<FontCache>("IggyPlayerSetBitmapFontCaching");
            ready = Bind<Ready>("IggyPlayerReadyToTick");
            valid = Bind<Ready>("IggyPlayerGetValid");
            antiAlias = Bind<VoidPI>("IggyPlayerSetAntialiasing");
            pause=Bind<VoidPI>("IggyPlayerPause");
            display = Bind<VoidPII>("IggyPlayerSetDisplaySize");
            goToFrame=Bind<VoidPII>("IggyPlayerGotoFrameRS");
            properties = Bind<PtrP>("IggyPlayerProperties");
            focusedText=Bind<PtrP>("IggyIMEGetFocusedTextfield");
            setFocus=Bind<SetFocus>("IggyPlayerSetFocusRS");
            dispatch = Bind<EventCall>("IggyPlayerDispatchEventRS");
            mouseMove = Bind<MakeMouse>("IggyMakeEventMouseMove");
            mouseButton = Bind<VoidPI>("IggyMakeEventMouseButton");
            key = Bind<MakeKeyEvent>("IggyMakeEventKey");
            updateTickWithoutRender=Bind<VoidP>("IggyPlayerDebugUpdateReadyToTickWithFakeRender");
            setTime=Bind<SetTime>("IggyPlayerDebugSetTime");
            focusLost=Bind<VoidP>("IggyMakeEventFocusLost");
            character = Bind<VoidPI>("IggyMakeEventChar");
            mouseWheel = Bind<VoidPI>("IggyMakeEventMouseWheel");
            mouseLeave = Bind<VoidP>("IggyMakeEventMouseLeave");
            warning = delegate(IntPtr u, IntPtr p, int code, IntPtr text) { log.Write("DLL " + code + ": " + Utf8(text)); };
            trace = delegate(IntPtr u, IntPtr p, IntPtr text, int length) { log.Write("trace: " + Utf8(text)); };
            if(modern) {
                // This supplied build takes four host callbacks, copied by Init:
                // optional variadic debug output, debug name, malloc and free.
                // Ignoring extra x64 arguments is intentional for debug output.
                PtrP debug=delegate(IntPtr format){return IntPtr.Zero;};
                PtrP allocate=delegate(IntPtr size){try{return Marshal.AllocHGlobal(size);}catch(OutOfMemoryException){return IntPtr.Zero;}};
                VoidP release=delegate(IntPtr ptr){Marshal.FreeHGlobal(ptr);};
                // The name callback also returns the supplied character buffer.
                DebugName name=delegate(IntPtr buffer,IntPtr capacity){if(capacity.ToInt64()>0)Marshal.WriteByte(buffer,0);return buffer;};
                Delegate[] callbacks={debug,name,allocate,release};
                IntPtr* table=stackalloc IntPtr[4];
                for(int i=0;i<callbacks.Length;i++){bindings.Add(callbacks[i]);table[i]=Marshal.GetFunctionPointerForDelegate(callbacks[i]);}
                Bind<VoidPP>("IggyInit")(IntPtr.Zero,(IntPtr)table);
            } else Bind<VoidP>("IggyInit")(IntPtr.Zero);
            initialized = true;
            // Both supported builds expose option 2 for synchronous AS3 goto.
            // Scripts can access the destination's named children immediately
            // after gotoAndStop/gotoAndPlay, as in Flash Player.
            Bind<VoidII>("IggyConfigureBool")(2,1);
            Bind<VoidPP>("IggySetWarningCallback")(Marshal.GetFunctionPointerForDelegate(warning), IntPtr.Zero);
            Bind<VoidPP>("IggySetTraceCallbackUTF8")(Marshal.GetFunctionPointerForDelegate(trace), IntPtr.Zero);
            audioVolume=Bind<AudioVolume>("IggyPlayerSetAudioVolume");
            AudioEnabled=audio;
            if(audio) {
                Bind<Void0>("IggyAudioUseWaveOut")();
                Mp3=new Mp3Decoder(this,log);
                log.Write("Audio: native WaveOut output enabled (PCM, ADPCM and MP3).");
            }
            Renderer = new SoftwareRenderer(log);
            Renderer.CorrectFocalMorphs=correctFocalMorphs;
            Renderer.SetBackend(renderer);
            Bind<VoidP>("IggySetGDraw")(Renderer.Interface);
            emptyStyles=Marshal.AllocHGlobal(8);Marshal.WriteInt64(emptyStyles,0);
            } catch {
                if(Renderer!=null)Renderer.Dispose();
                if(initialized)shutdown();
                if(Mp3!=null)Mp3.Dispose();
                ReleaseDeviceFonts();
                if(emptyStyles!=IntPtr.Zero){Marshal.FreeHGlobal(emptyStyles);emptyStyles=IntPtr.Zero;}
                FreeLibrary(module);throw;
            }
        }
        void InstallDeviceFonts() {
            // The DLL has no access to Windows fonts by itself. Device text
            // (including a TextField created by AS3) needs host-installed TTFs.
            // Names are copied by InstallTruetype; font bytes are borrowed and
            // must remain alive until IggyShutdown, across movie close/reopen.
            installedFontMaxCount(32);
            string directory=Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            string[][] families={
                new string[]{"Times New Roman","_serif","times.ttf","timesbd.ttf","timesi.ttf","timesbi.ttf"},
                new string[]{"Arial","_sans","arial.ttf","arialbd.ttf","ariali.ttf","arialbi.ttf"},
                new string[]{"Courier New","_typewriter","cour.ttf","courbd.ttf","couri.ttf","courbi.ttf"}
            };
            int installed=0;
            foreach(string[] family in families)for(byte style=0;style<4;style++) {
                string file=Path.Combine(directory,family[style+2]);
                if(!File.Exists(file))continue;
                IntPtr bytes;
                if(!deviceFontData.TryGetValue(file,out bytes)) {
                    byte[] data=File.ReadAllBytes(file);bytes=Marshal.AllocHGlobal(data.Length);
                    deviceFontData.Add(file,bytes);Marshal.Copy(data,0,bytes,data.Length);
                }
                for(int alias=0;alias<2;alias++) {
                    // Installed fonts have native lookup priority. Preserve
                    // outlines already supplied by the movie and its imports.
                    if(embeddedFonts.Contains(family[alias]))continue;
                    IntPtr name=Marshal.StringToHGlobalUni(family[alias]);
                    try{installTrueType(bytes,0,name,-1,style);installedDeviceFonts.Add(new KeyValuePair<string,byte>(family[alias],style));installed++;}
                    finally{Marshal.FreeHGlobal(name);}
                }
            }
            log.Write("Registered "+installed+" device font names/styles from Windows (Times New Roman, Arial, Courier New and generic aliases).");
        }
        void ReleaseDeviceFonts() {
            foreach(IntPtr bytes in deviceFontData.Values)Marshal.FreeHGlobal(bytes);
            deviceFontData.Clear();
        }
        void RemoveDeviceFonts() {
            foreach(KeyValuePair<string,byte> font in installedDeviceFonts) {
                IntPtr name=Marshal.StringToHGlobalUni(font.Key);
                try{removeFont(name,-1,font.Value);}finally{Marshal.FreeHGlobal(name);}
            }
            installedDeviceFonts.Clear();embeddedFonts.Clear();
        }
        public void Open(string file, int width, int height) {
            Close();
            try {
            FileName = Path.GetFullPath(file);
            byte[] original=File.ReadAllBytes(FileName);
            if(correctLoaderInfo) {
                List<byte[]> sources=new List<byte[]> {original};
                CollectRuntimeSources(FileName,original,sources,new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                runtimeScripts=new RuntimeScriptPlan(sources);
            }
            LoadImports(FileName,original,new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            byte[] bytes = Container.Prepare(runtimeScripts==null?original:runtimeScripts.Apply(original,true,log),log);
            RegisterGlobalScripts(bytes);
            if (bytes.Length < 32) throw new InvalidDataException("The Iggy file is too short.");
            foreach(string font in Container.EmbeddedFonts(bytes))embeddedFonts.Add(font);
            InstallDeviceFonts();
            log.Write("Open: " + FileName + " (" + bytes.Length + " bytes)");
            byte* options = stackalloc byte[80];
            for(int i=0;i<80;i++)options[i]=0;
            if(runtimeScripts!=null&&runtimeScripts.NeedsLoader)compilerLifetime=new CompilerLifetime(module,modernDll);
            bool deferClasses=runtimeScripts!=null&&runtimeScripts.NeedsLoader;
            using(StartupClassBinding deferred=deferClasses?new StartupClassBinding(module,modernDll):null)
                fixed (byte* data = bytes) Player = create((IntPtr)data, (uint)bytes.Length, (IntPtr)options);
            if (Player == IntPtr.Zero) throw new InvalidDataException("The Iggy DLL rejected this file. See the log for details.");
            NormalizeTimelineMatrices(Player);
            NormalizeEmptyStyleTables(Player);
            NormalizeAudio(Player);
            InstallTextures(Player,FileName);
            IntPtr prop = properties(Player);
            StageWidth = Math.Max(1, Marshal.ReadInt32(prop, 0));
            StageHeight = Math.Max(1, Marshal.ReadInt32(prop, 4));
            log.Write("Stage: " + StageWidth + " x " + StageHeight);
            antiAlias(Player, 0);
            fontCache(Player,0,0,0,0);
            Resize(width, height);
            if(Muted)audioVolume(Player,0);
            if(correctLoaderInfo)loaderInfo=new LoaderInfoCompatibility(module,modernDll,Player,original.LongLength,log);
            if(correctLoaderInfo)runtimeCompatibility=new RuntimeCompatibility(module,modernDll,Player,log,CorrectZeroScale);
            if(AudioEnabled&&runtimeScripts!=null&&runtimeScripts.NeedsAudio)soundCompatibility=new SoundCompatibility(this,module,modernDll,log);
            if(correctLoaderInfo)cursorCompatibility=new CursorCompatibility(this,module,modernDll,log);
            if(runtimeScripts!=null&&runtimeScripts.NeedsLoader)Activity=new MovieActivity(this,module,modernDll);
            if(runtimeScripts!=null&&runtimeScripts.NeedsLoader)imageLoader=new ImageLoader(this,module,modernDll,log);
            if(imageLoader!=null&&loaderInfo!=null)loaderInfo.LoadedInfo=imageLoader.LookupLoaderInfo;
            if(runtimeScripts!=null&&(runtimeScripts.NeedsSharedObject||runtimeScripts.NeedsLoader))sharedObjectStorage=new SharedObjectStorage(this,StorageDirectory,log,imageLoader);
            wallClock.Restart();UpdateClock();
            if(deferClasses){PublishGlobalScripts(Marshal.ReadIntPtr(Player,modernDll?10560:10480));MovieFunction<VoidP>(0x42d0,0x1cd0)(Player);}
            initialize(Player);
            PatchDeferredDomainClasses();
            play(Player);
            if (valid(Player) == 0) throw new InvalidDataException("The movie failed during initialization; see the log.");
            } catch {Close();throw;}
        }
        string ResolveImport(string from,string url) {
            if(url.IndexOf("://",StringComparison.Ordinal)>=0)return null;
            string basePath=Path.Combine(Path.GetDirectoryName(from),url.Replace('/',Path.DirectorySeparatorChar));
            string[] candidates={Path.ChangeExtension(basePath,".iggy"),basePath,Path.Combine(Path.GetDirectoryName(from),Path.GetFileNameWithoutExtension(basePath)+".iggy")};
            foreach(string p in candidates)if(File.Exists(p))return Path.GetFullPath(p);
            return null;
        }
        void CollectRuntimeSources(string from,byte[] bytes,List<byte[]> sources,HashSet<string> visited) {
            if(!visited.Add(from))return;
            foreach(string url in Container.Imports(bytes)) {
                string file=ResolveImport(from,url);if(file==null||visited.Contains(file))continue;
                byte[] data=File.ReadAllBytes(file);sources.Add(data);CollectRuntimeSources(file,data,sources,visited);
            }
        }
        void LoadImports(string from,byte[] bytes,HashSet<string> visiting) {
            if(!visiting.Add(from))throw new InvalidDataException("Cyclic Iggy imports: "+from);
            foreach(string url in Container.Imports(bytes)) {
                string file=ResolveImport(from,url);
                string registered;
                if(loadedUrls.TryGetValue(url,out registered)) {
                    if(file!=null&&!String.Equals(file,registered,StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Import URL refers to two different local files: "+url);
                    continue;
                }
                if(file==null){log.Write("Missing import: "+url+" (requested by "+from+")");continue;}
                byte[] original=File.ReadAllBytes(file);LoadImports(file,original,visiting);
                byte[] data=Container.Prepare(runtimeScripts==null?original:runtimeScripts.Apply(original,false,log),log),name=Encoding.UTF8.GetBytes(url+"\0");
                RegisterGlobalScripts(data);
                foreach(string font in Container.EmbeddedFonts(data))embeddedFonts.Add(font);
                byte* options=stackalloc byte[80];for(int j=0;j<80;j++)options[j]=0;
                int id;
                fixed(byte* p=data)fixed(byte* n=name)id=createLibrary((IntPtr)n,(IntPtr)p,(uint)data.Length,(IntPtr)options);
                if(id<0||id>31)throw new InvalidDataException("DLL rejected imported library: "+url);
                libraries.Add(id);loadedUrls.Add(url,file);
                NormalizeTimelineMatrices(libraryGeneric(id));NormalizeEmptyStyleTables(libraryGeneric(id));NormalizeAudio(libraryGeneric(id));InstallTextures(libraryGeneric(id),file);
                log.Write("Registered import "+url+" from "+file);
            }
            visiting.Remove(from);
        }
        void InstallTextures(IntPtr generic,string file) {
            string resource=Path.ChangeExtension(file,".raw.iggytex");
            if(!File.Exists(resource))resource=Path.ChangeExtension(file,".iggytex");
            if(!File.Exists(resource)) {
                IntPtr metadata=Marshal.ReadIntPtr(generic,512);
                if(metadata!=IntPtr.Zero&&(ushort)Marshal.ReadInt16(metadata)>0)log.Write("Missing external texture file for "+file);
                return;
            }
            byte[] bytes=File.ReadAllBytes(resource);
            fixed(byte* data=bytes) {int result=0;installResources(generic,(IntPtr)data,(uint)bytes.Length,(IntPtr)(&result));}
            log.Write("Installed external textures from "+resource);
        }
        // The third GotoFrame argument is stop, not play.
        public void Seek(int frame) {
            if(Player==IntPtr.Zero)return;
            int destination=Math.Max(0,Math.Min(TotalFrames-1,frame-1));
            goToFrame(Player,destination,1);TickWithHitBounds();pause(Player,1);
            stoppedBySeek=true;
        }
        public void SetPaused(bool value) {
            if(Player==IntPtr.Zero)return;
            UpdateClock();
            if(value)pause(Player,1);
            else {
                // Only a transport seek imposes stop on the root timeline.
                // Resuming the host clock must preserve the movie's AS3 stop().
                if(stoppedBySeek){goToFrame(Player,CurrentFrame-1,0);stoppedBySeek=false;}
                play(Player);
            }
        }
        internal void SetMuted(bool value) {Muted=value;if(Player!=IntPtr.Zero)audioVolume(Player,value?0:1);}
        public void Resize(int width, int height) {
            width=Math.Max(1,Math.Min(4096,width));height=Math.Max(1,Math.Min(4096,height));
            Renderer.Resize(width,height);
            if (Player != IntPtr.Zero) display(Player,width,height);
        }
        void NormalizeTimelineMatrices(IntPtr generic) {
            int normalized=NormalizeTimelineMatricesList(Marshal.ReadIntPtr(generic,128));
            int count=Marshal.ReadInt32(generic,160);IntPtr dictionary=Marshal.ReadIntPtr(generic,152);
            if(count<0||count>65536)throw new InvalidDataException("Invalid native dictionary length.");
            for(int i=0;i<count;i++) {
                IntPtr definition=Marshal.ReadIntPtr(dictionary,i*8);
                if(definition!=IntPtr.Zero&&Marshal.ReadByte(definition)==3)normalized+=NormalizeTimelineMatricesList(Marshal.ReadIntPtr(definition,56));
            }
            if(normalized>0)log.Write("Resolved "+normalized+" inherited placement matrices in memory for native frame seeking; source file unchanged.");
        }
        static int NormalizeTimelineMatricesList(IntPtr command) {
            // The DLL's SWF loader resolves inherited matrices before its seek
            // optimizer can discard placements whose lifetimes have ended.
            // Loading compiled Iggy bypasses that preprocessing (0x4C3B0).
            Dictionary<int,byte[]> matrices=new Dictionary<int,byte[]>();
            HashSet<IntPtr> visited=new HashSet<IntPtr>();int normalized=0;
            while(command!=IntPtr.Zero) {
                if(!visited.Add(command)||visited.Count>1000000)throw new InvalidDataException("Invalid native timeline command list.");
                int type=Marshal.ReadByte(command,8);
                if(type==2) {
                    int depth=(ushort)Marshal.ReadInt16(command,40),flags=Marshal.ReadByte(command,9);
                    byte[] matrix;
                    if((flags&1)!=0) {
                        matrix=new byte[24];Marshal.Copy(IntPtr.Add(command,16),matrix,0,24);matrices[depth]=matrix;
                    } else if((Marshal.ReadIntPtr(command,48)!=IntPtr.Zero||(flags&32)!=0)&&matrices.TryGetValue(depth,out matrix)) {
                        Marshal.Copy(matrix,0,IntPtr.Add(command,16),24);Marshal.WriteByte(command,9,(byte)(flags|1));normalized++;
                    }
                } else if(type==3)matrices.Remove((ushort)Marshal.ReadInt16(command,10));
                command=Marshal.ReadIntPtr(command);
            }
            return normalized;
        }
        void NormalizeEmptyStyleTables(IntPtr generic) {
            int count=Marshal.ReadInt32(generic,160),normalized=0;
            IntPtr dictionary=Marshal.ReadIntPtr(generic,152);
            if(count<0||count>65536)throw new InvalidDataException("Invalid native dictionary length.");
            for(int i=0;i<count;i++) {
                IntPtr shape=Marshal.ReadIntPtr(dictionary,i*8);if(shape==IntPtr.Zero)continue;
                int type=Marshal.ReadByte(shape);if(type!=1&&type!=2)continue;
                int groups=(ushort)Marshal.ReadInt16(shape,56);IntPtr data=Marshal.ReadIntPtr(shape,72);
                for(int g=0;g<groups;g++)for(int field=32;field<=40;field+=8) {
                    int at=g*64+field;
                    IntPtr table=Marshal.ReadIntPtr(data,at);
                    if(table==IntPtr.Zero){Marshal.WriteIntPtr(data,at,emptyStyles);normalized++;continue;}
                    int styles=Marshal.ReadInt32(table),stride=field==32?72:88;
                    if(styles<0||styles>32767)throw new InvalidDataException("Invalid morph style count.");
                    for(int style=0;style<styles;style++) {
                        IntPtr fill=IntPtr.Add(table,8+style*stride);
                        int fillType=Marshal.ReadByte(fill);
                        if(fillType==16||fillType==18||fillType==19)Renderer.RegisterGradient(IntPtr.Add(fill,8));
                        if(type!=2||fillType!=19)continue;
                        IntPtr gradient=Marshal.ReadIntPtr(fill,64);if(gradient==IntPtr.Zero)continue;
                        FocalMorph focal=new FocalMorph {Definition=shape,CharacterId=(ushort)Marshal.ReadInt16(shape,2),StartFocus=*((float*)((byte*)gradient+4)),EndFocus=*((float*)((byte*)gradient+8))};
                        for(int j=0;j<4;j++){focal.Start[j]=*((float*)((byte*)fill+16+j*4));focal.End[j]=*((float*)((byte*)fill+40+j*4));}
                        Renderer.RegisterFocal(IntPtr.Add(fill,8),focal);
                        log.Write("Registered focal morph "+focal.CharacterId+": focus "+focal.StartFocus+" -> "+focal.EndFocus);
                    }
                }
            }
            if(normalized>0)log.Write("Normalized "+normalized+" empty style tables in the loaded movie (native destructor requires non-null tables).");
        }
        void UpdateClock() {if(UseWallClock&&Player!=IntPtr.Zero)setTime(Player,1.0+wallClock.Elapsed.TotalSeconds);}
        internal void TickOnce() {UpdateClock();TickWithHitBounds();}
        void TickWithHitBounds() {
            if(!mouseInside){tick(Player);if(soundCompatibility!=null)soundCompatibility.AfterTick();return;}
            // Tick also recomputes hover for a stationary mouse. Apply the
            // same current morph bounds as for dispatched mouse events so an
            // invisible transition cannot steal hover on the next frame.
            Dictionary<IntPtr,float[]> saved=MorphHitBounds();
            try {tick(Player);}
            finally {RestoreMorphHitBounds(saved);}
            if(soundCompatibility!=null)soundCompatibility.AfterTick();
        }
        public bool Frame(bool advance) {
            if (Player == IntPtr.Zero) return false;
            UpdateClock();
            LastAdvancedFrames=0;
            // DispatchEventRS handles input synchronously. A tick advances all
            // timelines even after IggyPlayerPause, so never tick just for input.
            if(imageLoader!=null)imageLoader.PumpMovies();
            if(advance) {
                // Simulation runs at the movie's clock even when software
                // rendering is slower. The public no-render update resets
                // the DLL's three-tick render guard without dropping time.
                long start=Stopwatch.GetTimestamp();updateTickWithoutRender(Player);
                for(int steps=0;steps<60&&ready(Player)!=0;steps++) {
                    TickWithHitBounds();updateTickWithoutRender(Player);
                    LastAdvancedFrames++;
                    if(Stopwatch.GetTimestamp()-start>Stopwatch.Frequency/100)break;
                }
            }
            float* color=stackalloc float[3];backgroundColor(Player,(IntPtr)color);
            Renderer.Background=unchecked((int)0xff000000)|((int)(color[0]*255+0.5f)<<16)|((int)(color[1]*255+0.5f)<<8)|(int)(color[2]*255+0.5f);
            Renderer.BeginMorphFrame();
            if(Renderer.CorrectFocalMorphs) {
                HashSet<IntPtr> seen=new HashSet<IntPtr>();Stack<IntPtr> pending=new Stack<IntPtr>();
                pending.Push(IntPtr.Add(Player,rootInstanceOffset));pending.Push(IntPtr.Add(Player,stageInstanceOffset));
                while(pending.Count>0) {
                    IntPtr item=pending.Pop();if(item==IntPtr.Zero||!seen.Add(item)||seen.Count>100000)continue;
                    pending.Push(Marshal.ReadIntPtr(item,16));pending.Push(Marshal.ReadIntPtr(item,8));
                    if(Marshal.ReadByte(item,3)==2)Renderer.AddMorphInstance(Marshal.ReadIntPtr(item,168),*((float*)((byte*)item+268)));
                }
            }
            draw(Player);
            if(cursorCompatibility!=null)cursorCompatibility.Prune();
            if(imageLoader!=null)imageLoader.Prune();
            if(Activity!=null) {
                Activity.AfterTick();
                if(Activity.CollectRequested){Activity.CollectRequested=false;Bind<VoidPI>("IggyPlayerGarbageCollect")(Player,100);if(imageLoader!=null)imageLoader.Prune();}
                Activity.Prune();
            }
            return valid(Player) != 0;
        }
        void Send(Action<IntPtr> make) {
            if (Player == IntPtr.Zero) return;
            UpdateClock();
            byte* ev = stackalloc byte[64];
            for (int i = 0; i < 64; i++) ev[i] = 0;
            make((IntPtr)ev);Dictionary<IntPtr,float[]> saved=MorphHitBounds();
            try {dispatch(Player, (IntPtr)ev, IntPtr.Zero);}
            finally {RestoreMorphHitBounds(saved);}
        }
        static void RestoreMorphHitBounds(Dictionary<IntPtr,float[]> saved) {
            foreach(KeyValuePair<IntPtr,float[]> pair in saved)Marshal.Copy(pair.Value,0,pair.Key,4);
        }
        Dictionary<IntPtr,float[]> MorphHitBounds() {
            // Input and ticks recompute instance bounds from the definition. Limit its
            // broad start/end union to the ratios of the live instances while
            // hit test; definitions remain unchanged for rendering.
            Dictionary<IntPtr,float[]> active=new Dictionary<IntPtr,float[]>(),saved=new Dictionary<IntPtr,float[]>();
            Stack<IntPtr> pending=new Stack<IntPtr>();HashSet<IntPtr> seen=new HashSet<IntPtr>();
            pending.Push(IntPtr.Add(Player,stageInstanceOffset));
            while(pending.Count>0) {
                IntPtr item=pending.Pop();if(item==IntPtr.Zero||!seen.Add(item)||seen.Count>100000)continue;
                pending.Push(Marshal.ReadIntPtr(item,16));pending.Push(Marshal.ReadIntPtr(item,8));
                if(Marshal.ReadByte(item,3)==7) {
                    IntPtr states=Marshal.ReadIntPtr(item,72);
                    for(int i=0;i<4;i++)pending.Push(Marshal.ReadIntPtr(states,8*i));
                }
                if(Marshal.ReadByte(item,3)!=2)continue;
                IntPtr definition=Marshal.ReadIntPtr(item,168);if(definition==IntPtr.Zero)continue;
                float ratio=*((float*)((byte*)item+268));if(float.IsNaN(ratio)||ratio<0||ratio>1)continue;
                float[] bounds=new float[4],other;
                for(int i=0;i<4;i++){float start=*((float*)((byte*)definition+32+4*i)),end=*((float*)((byte*)definition+112+4*i));bounds[i]=start+(end-start)*ratio;}
                if(active.TryGetValue(definition,out other))for(int i=0;i<4;i++)bounds[i]=i<2?Math.Min(other[i],bounds[i]):Math.Max(other[i],bounds[i]);
                active[definition]=bounds;
            }
            foreach(KeyValuePair<IntPtr,float[]> pair in active) {
                IntPtr field=IntPtr.Add(pair.Key,96);float[] original=new float[4];Marshal.Copy(field,original,0,4);saved[field]=original;
                Marshal.Copy(pair.Value,0,field,4);
            }
            return saved;
        }
        public void MouseMove(int x, int y) { mouseInside=true;Send(p => mouseMove(p, x, y)); }
        public void MouseButton(int button, bool down) { Send(p => mouseButton(p, button * 2 + (down ? 1 : 2))); }
        public void Key(int code,bool down,int location=0) {
            if(Player==IntPtr.Zero||code<0||code>255)return;
            ClearDetachedKeyboardFocus();
            location&=3;
            int identity=(code<<2)|location;
            if(down)pressedKeys[identity]=code;else pressedKeys.Remove(identity);
            // Iggy event 10 is key down, 9 is key up. The fourth argument is
            // AS3 keyLocation (not a scan code or a modifier mask).
            Send(p => key(p,down?10:9,code,location));
        }
        public void ReleaseKeys() {
            int[] held=new int[pressedKeys.Count];pressedKeys.Keys.CopyTo(held,0);
            foreach(int identity in held)Key(identity>>2,false,identity&3);
        }
        public void FocusLost() {ReleaseKeys();Send(p => focusLost(p));}
        void ClearDetachedKeyboardFocus() {
            if(Player==IntPtr.Zero)return;
            // Both supplied runtimes retain focus on removed display objects.
            // Keyboard events then bubble along a detached parent chain and
            // never reach Stage. Keep attached targets (including text input)
            // and let the public focus API deliver the normal focus events.
            IntPtr focused=Marshal.ReadIntPtr(Player,modernDll?10744:10664);
            if(focused==IntPtr.Zero)return;
            IntPtr stage=IntPtr.Add(Player,stageInstanceOffset),item=focused;
            for(int depth=0;item!=IntPtr.Zero&&depth<10000;depth++,item=Marshal.ReadIntPtr(item,128))if(item==stage)return;
            setFocus(Player,IntPtr.Zero,0);
        }
        public void Character(char ch) { ClearDetachedKeyboardFocus();Send(p => character(p, ch)); }
        public void Wheel(int delta) { Send(p => mouseWheel(p, delta)); }
        public void MouseLeave() { mouseInside=false;Send(p => mouseLeave(p)); }
        public void Close() {
            mouseInside=false;
            wallClock.Reset();
            stoppedBySeek=false;
            pressedKeys.Clear();
            // Global TrueType glyphs can reference player-owned tessellation.
            // Flush while the player is alive, before releasing its memory.
            if(sharedObjectStorage!=null){sharedObjectStorage.Detach();sharedObjectStorage=null;}
            if(imageLoader!=null)imageLoader.CancelAll();
            DetachByteArrays();
            if(Activity!=null)Activity.Detach();
            DetachMovieClasses();
            if(compilerLifetime!=null){compilerLifetime.Dispose();compilerLifetime=null;}
            if (Player != IntPtr.Zero) { if(soundCompatibility!=null)soundCompatibility.Detach();flushInstalledFonts();destroy(Player); Player = IntPtr.Zero; }
            ReleaseMovieDefinitions();
            if(imageLoader!=null){imageLoader.Dispose();imageLoader=null;}
            if(Activity!=null){Activity.KeepAlive();Activity=null;}
            GC.KeepAlive(byteArrayFinalizer);GC.KeepAlive(byteArrayConstructor);GC.KeepAlive(amfAnyVectorConstructor);amfAnyVectorConstructor=null;ReleaseByteArrays();
            GC.KeepAlive(soundCompatibility);soundCompatibility=null;
            GC.KeepAlive(loaderInfo);loaderInfo=null;
            GC.KeepAlive(runtimeCompatibility);runtimeCompatibility=null;
            GC.KeepAlive(cursorCompatibility);cursorCompatibility=null;
            runtimeScripts=null;
            for(int i=libraries.Count-1;i>=0;i--)libraryDestroy(libraries[i]);
            libraries.Clear();loadedUrls.Clear();
            ReleaseMovieSymbolNames();
            RemoveDeviceFonts();
            if (Renderer != null) Renderer.ClearResources();
        }
        public void Dispose() {
            if(disposed)return;disposed=true;
            Close();
            if (initialized) { shutdown(); initialized = false; }
            if(Mp3!=null)Mp3.Dispose();
            ReleaseDeviceFonts();
            if (Renderer != null) Renderer.Dispose();
            if(emptyStyles!=IntPtr.Zero){Marshal.FreeHGlobal(emptyStyles);emptyStyles=IntPtr.Zero;}
            FreeLibrary(module);
            GC.KeepAlive(warning); GC.KeepAlive(trace); GC.KeepAlive(bindings);
        }
    }
}
