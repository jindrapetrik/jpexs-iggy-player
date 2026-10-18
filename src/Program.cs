using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace IggyViewer {
    internal static class Program {
        internal static string FindDll(string directory) {
            foreach(string name in new string[]{"iggy_w64_final.dll","iggy_w64.dll"}) {
                string candidate=Path.Combine(directory,"lib",name);
                if(File.Exists(candidate))return candidate;
            }
            throw new FileNotFoundException("Cannot find lib/iggy_w64_final.dll or lib/iggy_w64.dll beside the player. Supply the runtime DLL or use --dll PATH.");
        }
        [STAThread] static int Main(string[] args) {
            Native.SetErrorMode(3);
            string directory=AppDomain.CurrentDomain.BaseDirectory;
            string logPath=Path.ChangeExtension(Application.ExecutablePath,".log");
            Log log=new Log(logPath);
            try {
                string renderer="auto";int rendererOption=Array.IndexOf(args,"--renderer");
                if(rendererOption>=0) {
                    if(rendererOption+1>=args.Length)throw new ArgumentException("--renderer requires auto, opengl or software.");
                    renderer=args[rendererOption+1].ToLowerInvariant();
                    if(renderer!="auto"&&renderer!="opengl"&&renderer!="software")throw new ArgumentException("Renderer must be auto, opengl or software.");
                    System.Collections.Generic.List<string> remaining=new System.Collections.Generic.List<string>(args);remaining.RemoveRange(rendererOption,2);args=remaining.ToArray();
                }
                string dll=null;int dllOption=Array.IndexOf(args,"--dll");
                if(dllOption>=0) {
                    if(dllOption+1>=args.Length)throw new ArgumentException("--dll requires a DLL path.");
                    dll=Path.GetFullPath(args[dllOption+1]);
                    System.Collections.Generic.List<string> remaining=new System.Collections.Generic.List<string>(args);
                    remaining.RemoveRange(dllOption,2);args=remaining.ToArray();
                } else {
                    dll=FindDll(directory);
                }
                bool correctFocal=true;int nativeFocal=Array.IndexOf(args,"--native-focal");
                if(nativeFocal>=0){System.Collections.Generic.List<string> remaining=new System.Collections.Generic.List<string>(args);remaining.RemoveAt(nativeFocal);args=remaining.ToArray();correctFocal=false;}
                if(args.Length>=3&&args[0]=="--self-test") {SelfTest.Run(dll,args[1],args[2],log);return 0;}
                if(args.Length>=3&&args[0]=="--self-test-seek") {SelfTest.RunSeek(dll,args[1],args[2],log);return 0;}
                if(args.Length>=3&&args[0]=="--self-test-filters") {SelfTest.RunFilters(dll,args[1],args[2],log);return 0;}
                if(args.Length>=3&&args[0]=="--self-test-as3") {SelfTest.RunAs3(dll,args[1],args[2],log);return 0;}
                if(args.Length>=3&&args[0]=="--self-test-fixups") {SelfTest.RunFixups(dll,args[1],args[2],log);return 0;}
                if(args.Length>=3&&args[0]=="--self-test-fixup-partitions") {SelfTest.RunFixupPartitions(dll,args[1],args[2],log);return 0;}
                if(args.Length>=3&&args[0]=="--self-test-game-playback") {SelfTest.RunGamePlayback(dll,args[1],args[2],log);return 0;}
                if(args.Length>=3&&args[0]=="--self-test-level-exit") {SelfTest.RunLevelExit(dll,args[1],args[2],log);return 0;}
                if(args.Length>=3&&args[0]=="--self-test-play-response") {SelfTest.RunPlayResponse(dll,args[1],args[2],log);return 0;}
                if(args.Length>=3&&args[0]=="--self-test-clock") {SelfTest.RunClock(dll,args[1],args[2],log);return 0;}
                if(args.Length>=3&&args[0]=="--self-test-zero-scale") {SelfTest.RunZeroScale(dll,args[1],args[2],log);return 0;}
                if(args.Length>=3&&args[0]=="--self-test-loaderinfo") {SelfTest.RunLoaderInfo(dll,args[1],args[2],log);return 0;}
                if(args.Length>=3&&args[0]=="--self-test-runtime") {SelfTest.RunRuntime(dll,args[1],args[2],log);return 0;}
                if(args.Length>=3&&args[0]=="--self-test-sharedobject") {SelfTest.RunSharedObject(dll,args[1],args[2],log,renderer);return 0;}
                if(args.Length>=3&&args[0]=="--self-test-sharedobject-autosave") {SelfTest.RunSharedObjectAutoSave(dll,args[1],args[2],log,renderer);return 0;}
                if(args.Length>=3&&args[0]=="--self-test-url-loader") {SelfTest.RunDataLoader(dll,args[1],args[2],log,renderer);return 0;}
                if(args.Length>=3&&args[0]=="--self-test-loader") {SelfTest.RunLoader(dll,args[1],args[2],log,renderer);return 0;}
                if(args.Length>=3&&args[0]=="--self-test-loader-bytes") {SelfTest.RunLoaderBytes(dll,args[1],args[2],log,renderer);return 0;}
                if(args.Length>=3&&args[0]=="--self-test-bytearray") {SelfTest.RunByteArray(dll,args[1],args[2],log,renderer);return 0;}
                if(args.Length>=3&&args[0]=="--self-test-amf") {SelfTest.RunAmf(dll,args[1],args[2],log,renderer);return 0;}
                if(args.Length>=3&&args[0]=="--self-test-movie-loader") {SelfTest.RunMovieLoader(dll,args[1],args[2],log,renderer);return 0;}
                if(args.Length>=3&&args[0]=="--self-test-domains") {SelfTest.RunMovieDomains(dll,args[1],args[2],log);return 0;}
                if(args.Length>=3&&args[0]=="--self-test-unload") {SelfTest.RunMovieUnload(dll,args[1],args[2],log,renderer);return 0;}
                if(args.Length>=3&&args[0]=="--self-test-unload-audio") {SelfTest.RunMovieUnload(dll,args[1],args[2],log,renderer,true);return 0;}
                if(args.Length>=3&&args[0]=="--self-test-audio") {SelfTest.RunAudio(dll,args[1],args[2],log);return 0;}
                if(args.Length>=3&&args[0]=="--self-test-focal-instances") {SelfTest.RunFocalInstances(dll,args[1],args[2],log);return 0;}
                if(args.Length>=3&&args[0]=="--self-test-opengl") {SelfTest.RunOpenGl(dll,args[1],args[2],log);return 0;}
                if(args.Length>=3&&args[0]=="--render-all") {
                    int w=args.Length>=5?int.Parse(args[3]):960,h=args.Length>=5?int.Parse(args[4]):640;
                    Directory.CreateDirectory(args[2]);
                    using(Native native=new Native(dll,log,correctFocal,renderer:renderer)) {
                        native.Open(args[1],w,h);
                        for(int frame=1;frame<=native.TotalFrames;frame++) {
                            native.Seek(frame);
                            if(!native.Frame(false))throw new InvalidOperationException("Native playback failed at frame "+frame);
                            using(Bitmap image=native.Renderer.Snapshot())image.Save(Path.Combine(args[2],frame.ToString("D3")+".png"),ImageFormat.Png);
                            log.Write("FRAME "+frame+" / "+native.TotalFrames+" triangles="+native.Renderer.Triangles);
                        }
                    }
                    return 0;
                }
                if(args.Length>=3&&args[0]=="--render") {
                    int w=960,h=540,frames=1;
                    if(args.Length>=5) {w=int.Parse(args[3]);h=int.Parse(args[4]);}
                    if(args.Length>=6)frames=int.Parse(args[5]);
                    using(Native native=new Native(dll,log,correctFocal,renderer:renderer)) {
                        native.Open(args[1],w,h);
                        if(args.Length>=7)native.Seek(int.Parse(args[6]));
                        for(int i=0;i<frames;i++) {
                            if(i>0)Thread.Sleep(45);
                            if(!native.Frame(i>0))throw new InvalidOperationException("Native playback failed.");
                        }
                        using(Bitmap image=native.Renderer.Snapshot())image.Save(args[2],ImageFormat.Png);
                        log.Write("Saved "+args[2]+", triangles="+native.Renderer.Triangles);
                    }
                    return 0;
                }
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                if(args.Length>=3&&args[0]=="--self-test-game-keyboard") {SelfTest.RunGameKeyboard(dll,args[1],args[2],log,renderer);return 0;}
                if(args.Length>=3&&args[0]=="--self-test-keyboard") {SelfTest.RunKeyboard(dll,args[1],args[2],log);return 0;}
                if(args.Length>=3&&args[0]=="--self-test-cursor") {SelfTest.RunCursor(dll,args[1],args[2],log);return 0;}
                using(Native native=new Native(dll,log,correctFocal,renderer:renderer,audio:args.Length==0||!args[0].StartsWith("--")))using(PlayerForm form=new PlayerForm(native,logPath,log)) {
                    if(args.Length>=3&&args[0]=="--ui-test") {
                        form.ShowInTaskbar=false;form.Opacity=0;form.Show();Application.DoEvents();
                        form.OpenFile(args[1],false);
                        foreach(string targetFrame in (args.Length>=4?args[3]:"1").Split(','))form.TestFrame(int.Parse(targetFrame));
                        Application.DoEvents();
                        using(Bitmap image=new Bitmap(form.Width,form.Height)) {form.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(args[2],ImageFormat.Png);}
                        log.Write("UI test saved "+args[2]);return 0;
                    }
                    if(args.Length>0)form.Shown+=delegate {form.OpenFile(args[0]);};
                    Application.Run(form);
                }
                return 0;
            } catch(Exception ex) {
                log.Write(ex.ToString());
                if(args.Length==0||!args[0].StartsWith("--"))MessageBox.Show(ex.Message+"\n\nLog: "+logPath,"Iggy Player",MessageBoxButtons.OK,MessageBoxIcon.Error);
                return 1;
            }
        }
    }
    internal sealed class Canvas : Control {
        SoftwareRenderer renderer;
        bool printing,hardwareCanvas;
        internal Action<Exception> RenderFailed;
        internal void SetRenderer(SoftwareRenderer value){
            renderer=value;bool hardware=value!=null&&value.HardwareAccelerated;
            if(hardwareCanvas!=hardware){hardwareCanvas=hardware;DoubleBuffered=!hardware;if(IsHandleCreated)RecreateHandle();}
        }
        bool HardwareFrame {get{return renderer!=null&&renderer.HardwareAccelerated&&HasMovie!=null&&HasMovie();}}
        protected override CreateParams CreateParams {get{CreateParams value=base.CreateParams;value.ClassStyle|=0x20;return value;}}
        [System.Runtime.InteropServices.DllImport("user32")] static extern IntPtr SetCursor(IntPtr cursor);
        [System.Runtime.InteropServices.DllImport("user32")] static extern IntPtr WindowFromPoint(Point point);
        internal bool MovieCursorHidden {get;private set;}
        internal void SetMovieCursor(bool hidden,Cursor cursor) {
            MovieCursorHidden=hidden;Cursor=hidden?Cursors.Default:cursor;
            // Also update a stationary pointer when AS3 changes visibility in
            // a frame script. Do not alter the cursor over another window.
            if(IsHandleCreated&&WindowFromPoint(System.Windows.Forms.Cursor.Position)==Handle)
                SetCursor(hidden?IntPtr.Zero:Cursor.Handle);
        }
        public Bitmap Frame;
        internal Func<bool> HasMovie;
        internal Action<int,bool,int> KeyInput;
        internal Action<char> TextInput;
        public Canvas() {DoubleBuffered=true;TabStop=true;BackColor=Color.FromArgb(179,223,255);}
        protected override bool IsInputKey(Keys keyData) {
            if(HasMovie!=null&&HasMovie()&&keyData!=(Keys.Alt|Keys.F4))return true;
            return base.IsInputKey(keyData);
        }
        protected override bool IsInputChar(char charCode) {return HasMovie!=null&&HasMovie()||base.IsInputChar(charCode);}
        bool MovieKey(Message message) {
            return HasMovie!=null&&HasMovie()&&message.Msg>=0x100&&message.Msg<=0x106
                // Keep the standard Windows close shortcut.
                &&!((message.Msg==0x104||message.Msg==0x105)&&message.WParam.ToInt32()==(int)Keys.F4);
        }
        public override bool PreProcessMessage(ref Message message) {
            // Dialog preprocessing otherwise consumes arrows, Tab, Enter and
            // shortcuts before they ever reach this control's window procedure.
            if(MovieKey(message))return false;
            return base.PreProcessMessage(ref message);
        }
        protected override void WndProc(ref Message message) {
            if(message.Msg==0x317||message.Msg==0x318){bool previous=printing;printing=true;try{base.WndProc(ref message);}finally{printing=previous;}return;}
            // A null cursor here is local to this client area. ShowCursor's
            // global counter would also hide it over toolbars and dialogs.
            if(message.Msg==0x20&&message.WParam==Handle&&(message.LParam.ToInt64()&0xffff)==1&&MovieCursorHidden) {
                SetCursor(IntPtr.Zero);message.Result=(IntPtr)1;return;
            }
            if(MovieKey(message)) {
                if(message.Msg==0x100||message.Msg==0x101||message.Msg==0x104||message.Msg==0x105) {
                    int code=message.WParam.ToInt32(),location=0;
                    long flags=message.LParam.ToInt64();bool extended=(flags&0x1000000)!=0;
                    if(code==(int)Keys.ShiftKey)location=((flags>>16)&255)==0x36?2:1;
                    else if(code==(int)Keys.ControlKey||code==(int)Keys.Menu)location=extended?2:1;
                    else if((code>=96&&code<=111)||(code==13&&extended)
                        ||((code>=33&&code<=40||code==45||code==46)&&!extended))location=3;
                    if(KeyInput!=null)KeyInput(code,message.Msg==0x100||message.Msg==0x104,location);
                    message.Result=IntPtr.Zero;return;
                }
                if(message.Msg==0x102||message.Msg==0x106) {
                    if(TextInput!=null)TextInput((char)message.WParam.ToInt32());
                    message.Result=IntPtr.Zero;return;
                }
            }
            base.WndProc(ref message);
        }
        protected override void OnPaintBackground(PaintEventArgs e){if(!HardwareFrame||printing)base.OnPaintBackground(e);}
        protected override void OnPaint(PaintEventArgs e) {
            if(HardwareFrame) {
                if(printing){using(Bitmap image=renderer.Snapshot())e.Graphics.DrawImageUnscaled(image,0,0);}
                else {try{renderer.Present(Handle,ClientSize.Width,ClientSize.Height);}catch(Exception ex){if(RenderFailed!=null)RenderFailed(ex);else throw;}}
                return;
            }
            base.OnPaint(e);if(Frame!=null)e.Graphics.DrawImageUnscaled(Frame,0,0);
        }
        protected override void Dispose(bool disposing) {if(disposing){SetMovieCursor(false,Cursors.Default);if(Frame!=null)Frame.Dispose();}base.Dispose(disposing);}
    }
    internal sealed class PlayerForm : Form {
        readonly Native native;
        readonly Log log;
        readonly Icon windowIcon;
        readonly Canvas canvas=new Canvas();
        readonly ToolStripStatusLabel status=new ToolStripStatusLabel("Open an .iggy file");
        readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
        bool paused;
        bool cursorActive=true;
        readonly NumericUpDown frame=new NumericUpDown {Minimum=1,Maximum=1,Width=85};
        readonly Label frames=new Label {AutoSize=true,Text="/ 1",Padding=new Padding(2,6,0,0)};
        bool updatingFrame;
        readonly ToolStripMenuItem pause=new ToolStripMenuItem("Pause") {CheckOnClick=true};
        public PlayerForm(Native native,string logPath,Log log) {
            this.native=native;this.log=log;
            windowIcon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);Icon=windowIcon;
            canvas.RenderFailed=delegate(Exception ex){timer.Stop();log.Write(ex.ToString());status.Text="OpenGL display failed. Select Playback > Renderer > Software.";};
            Text="Iggy Player — native 64-bit runtime";Width=1040;Height=680;
            MenuStrip menu=new MenuStrip();
            ToolStripMenuItem file=new ToolStripMenuItem("File");
            file.DropDownItems.Add("Open…",null,delegate {using(OpenFileDialog dialog=new OpenFileDialog {Filter="Iggy movies (*.iggy)|*.iggy|All files (*.*)|*.*"})if(dialog.ShowDialog(this)==DialogResult.OK)OpenFile(dialog.FileName);});
            file.DropDownItems.Add("Save frame as PNG…",null,delegate {using(SaveFileDialog dialog=new SaveFileDialog {Filter="PNG image|*.png",DefaultExt="png"})if(dialog.ShowDialog(this)==DialogResult.OK)using(Bitmap image=native.Renderer.Snapshot())image.Save(dialog.FileName,ImageFormat.Png);});
            file.DropDownItems.Add("Open log",null,delegate {System.Diagnostics.Process.Start("notepad.exe",logPath);});
            file.DropDownItems.Add("Exit",null,delegate {Close();});
            ToolStripMenuItem playback=new ToolStripMenuItem("Playback");
            pause.CheckedChanged+=delegate {paused=pause.Checked;native.SetPaused(paused);};playback.DropDownItems.Add(pause);
            playback.DropDownItems.Add("Reload",null,delegate {if(native.FileName!=null)OpenFile(native.FileName);});
            ToolStripMenuItem mute=new ToolStripMenuItem("Mute sound") {CheckOnClick=true,Checked=native.Muted};
            mute.CheckedChanged+=delegate {native.SetMuted(mute.Checked);};playback.DropDownItems.Add(mute);
            ToolStripMenuItem focal=new ToolStripMenuItem("Correct focal morphs") {CheckOnClick=true,Checked=native.Renderer.CorrectFocalMorphs};
            focal.CheckedChanged+=delegate {
                int current=native.CurrentFrame;bool wasPaused=paused;
                native.Renderer.CorrectFocalMorphs=focal.Checked;
                if(native.FileName!=null){OpenFile(native.FileName);Seek(current);pause.Checked=wasPaused;}
            };
            playback.DropDownItems.Add(focal);
            ToolStripMenuItem rendering=new ToolStripMenuItem("Renderer");
            ToolStripMenuItem hardware=new ToolStripMenuItem("OpenGL"),software=new ToolStripMenuItem("Software");
            Action updateRendering=delegate {hardware.Checked=native.Renderer.HardwareAccelerated;software.Checked=!hardware.Checked;};
            hardware.Click+=delegate {try{native.Renderer.SetBackend("opengl");updateRendering();RenderFrame();}catch(Exception ex){log.Write(ex.ToString());MessageBox.Show(this,ex.Message,"OpenGL unavailable",MessageBoxButtons.OK,MessageBoxIcon.Error);}};
            software.Click+=delegate {native.Renderer.SetBackend("software");updateRendering();RenderFrame();};
            rendering.DropDownItems.Add(hardware);rendering.DropDownItems.Add(software);playback.DropDownItems.Add(rendering);updateRendering();
            menu.Items.Add(file);menu.Items.Add(playback);MainMenuStrip=menu;
            FlowLayoutPanel transport=new FlowLayoutPanel {Dock=DockStyle.Top,Height=34,Padding=new Padding(4,2,0,0)};
            Button toggle=new Button {Text="Play / Pause",AutoSize=true};toggle.Click+=delegate {pause.Checked=!pause.Checked;canvas.Focus();};
            Button previous=new Button {Text="◀",Width=36},next=new Button {Text="▶",Width=36};
            previous.Click+=delegate {Seek(native.CurrentFrame-1);canvas.Focus();};next.Click+=delegate {Seek(native.CurrentFrame+1);canvas.Focus();};
            frame.ValueChanged+=delegate {if(!updatingFrame)Seek((int)frame.Value);};
            transport.Controls.Add(toggle);transport.Controls.Add(previous);transport.Controls.Add(next);transport.Controls.Add(frame);transport.Controls.Add(frames);
            StatusStrip bar=new StatusStrip();bar.Items.Add(status);
            canvas.Dock=DockStyle.Fill;Controls.Add(canvas);Controls.Add(transport);Controls.Add(bar);Controls.Add(menu);
            canvas.Resize+=delegate {if(canvas.Width>0&&canvas.Height>0)native.Resize(canvas.Width,canvas.Height);};
            canvas.MouseMove+=delegate(object s,MouseEventArgs e){native.MouseMove(e.X,e.Y);UpdateCursor();};
            canvas.MouseDown+=delegate(object s,MouseEventArgs e){canvas.Focus();native.MouseButton(e.Button==MouseButtons.Right?1:e.Button==MouseButtons.Middle?2:0,true);UpdateCursor();};
            canvas.MouseUp+=delegate(object s,MouseEventArgs e){native.MouseButton(e.Button==MouseButtons.Right?1:e.Button==MouseButtons.Middle?2:0,false);UpdateCursor();};
            canvas.MouseLeave+=delegate {native.MouseLeave();UpdateCursor();};
            canvas.MouseWheel+=delegate(object s,MouseEventArgs e){native.Wheel(e.Delta/120);UpdateCursor();};
            canvas.HasMovie=delegate {return native.Player!=IntPtr.Zero;};
            canvas.KeyInput=delegate(int code,bool down,int location){native.Key(code,down,location);UpdateCursor();};
            canvas.TextInput=delegate(char value){native.Character(value);UpdateCursor();};
            canvas.LostFocus+=delegate {native.ReleaseKeys();};
            Activated+=delegate {cursorActive=true;UpdateCursor();};
            Deactivate+=delegate {cursorActive=false;native.FocusLost();UpdateCursor();};
            AllowDrop=true;DragEnter+=delegate(object s,DragEventArgs e){if(e.Data.GetDataPresent(DataFormats.FileDrop))e.Effect=DragDropEffects.Copy;};
            DragDrop+=delegate(object s,DragEventArgs e){string[] names=(string[])e.Data.GetData(DataFormats.FileDrop);if(names.Length>0)OpenFile(names[0]);};
            timer.Interval=30;timer.Tick+=delegate {RenderFrame();};timer.Start();
        }
        public void OpenFile(string file,bool showError=true) {
            timer.Stop();
            canvas.SetMovieCursor(false,Cursors.Default);
            try {
                native.Open(file,Math.Max(1,canvas.Width),Math.Max(1,canvas.Height));native.SetPaused(paused);
                updatingFrame=true;frame.Maximum=native.TotalFrames;frame.Value=1;updatingFrame=false;
                frames.Text="/ "+native.TotalFrames;Text=Path.GetFileName(file)+" — Iggy Player";
                UpdateStatus();timer.Start();RenderFrame();canvas.Focus();
            }
            catch(Exception ex) {
                log.Write(ex.ToString());status.Text=ex.Message;
                if(canvas.Frame!=null){canvas.Frame.Dispose();canvas.Frame=null;canvas.Invalidate();}
                if(!showError)throw;
                MessageBox.Show(this,ex.Message,"Cannot open movie",MessageBoxButtons.OK,MessageBoxIcon.Error);
            }
        }
        internal void TestFrame(int value){Seek(value);}
        internal void TestRenderFrame(){RenderFrame();}
        void UpdateStatus(){status.Text="Native DLL: "+native.StageWidth+" × "+native.StageHeight+" | "+native.Renderer.BackendName+" | File → Open log";}
        void UpdateCursor() {canvas.SetMovieCursor(cursorActive&&native.HideSystemCursor,cursorActive&&native.HandCursor?Cursors.Hand:Cursors.Default);}
        void Seek(int value) {pause.Checked=true;native.Seek(value);RenderFrame();}
        void RenderFrame() {
            if(native.Player==IntPtr.Zero){canvas.SetMovieCursor(false,Cursors.Default);return;}
            try {
                if(!native.Frame(!paused)) {timer.Stop();canvas.SetMovieCursor(false,Cursors.Default);status.Text="Native playback failed — see log";return;}
                UpdateCursor();
                canvas.SetRenderer(native.Renderer);
                Bitmap old=canvas.Frame;canvas.Frame=native.Renderer.HardwareAccelerated?null:native.Renderer.Snapshot();if(old!=null)old.Dispose();canvas.Invalidate();UpdateStatus();
                updatingFrame=true;frame.Value=Math.Min(frame.Maximum,Math.Max(1,native.CurrentFrame));updatingFrame=false;
            } catch(Exception ex) {timer.Stop();canvas.SetMovieCursor(false,Cursors.Default);log.Write(ex.ToString());status.Text=ex.Message;}
        }
        protected override void Dispose(bool disposing) {if(disposing)timer.Dispose();base.Dispose(disposing);if(disposing&&windowIcon!=null)windowIcon.Dispose();}
    }
}
