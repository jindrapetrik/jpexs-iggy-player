using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace IggyViewer {
    internal static partial class SelfTest {
        static IntPtr GameKeyboardFocus(Native native) {
            IntPtr current=Marshal.AllocHGlobal(8),length=Marshal.AllocHGlobal(4);
            try {
                Check(native.Bind<FocusableObjects>("IggyPlayerGetFocusableObjects")(native.Player,current,IntPtr.Zero,0,length)!=0,"Read native keyboard focus");
                return Marshal.ReadIntPtr(current);
            } finally {Marshal.FreeHGlobal(current);Marshal.FreeHGlobal(length);}
        }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int FocusableObjects(IntPtr player,IntPtr focus,IntPtr objects,int capacity,IntPtr count);
        public static void RunGameKeyboard(string dll,string movie,string output,Log log,string renderer) {
            Directory.CreateDirectory(output);string hash=Hash(File.ReadAllBytes(movie));
            using(Native native=new Native(dll,log,renderer:renderer)) {
                native.StorageDirectory=Path.Combine(Path.GetFullPath(output),"storage-"+Guid.NewGuid().ToString("N"));
                using(PlayerForm form=new PlayerForm(native,"",log)) {
                    form.ShowInTaskbar=false;form.Opacity=0;form.Show();Application.DoEvents();form.OpenFile(movie,false);
                    Canvas canvas=null;foreach(Control control in form.Controls)if(control is Canvas)canvas=(Canvas)control;
                    for(int i=0;i<240&&ReadValue(native,"sec_count")<=3;i++){Thread.Sleep(25);native.TickOnce();}
                    Check(ReadValue(native,"sec_count")>3,"Preloader ready");
                    native.MouseMove(270*canvas.Width/550,420*canvas.Height/550);native.MouseButton(0,true);native.MouseButton(0,false);
                    IntPtr preloaderFocus=GameKeyboardFocus(native);
                    Check(preloaderFocus!=IntPtr.Zero,"Actual preloader button click gives the native button focus");
                    for(int i=0;i<1400&&native.CurrentFrame!=3;i++){Thread.Sleep(25);native.TickOnce();}
                    Check(native.CurrentFrame==3,"Preloader reaches menu");
                    for(int i=0;i<160;i++)native.TickOnce();
                    native.MouseMove((int)((Number(native,"playGame.x")+20)*canvas.Width/550),(int)((Number(native,"playGame.y")+10)*canvas.Height/550));
                    native.MouseButton(0,true);native.MouseButton(0,false);
                    for(int i=0;i<180&&native.CurrentFrame!=4;i++)native.TickOnce();
                    Check(native.CurrentFrame==4,"Menu mouse click enters gameplay");
                    for(int i=0;i<60;i++)native.TickOnce();
                    form.TestRenderFrame();Check(canvas.Focused,"Canvas retains Windows keyboard focus throughout menu and level transitions");
                    Check(GameKeyboardFocus(native)==preloaderFocus&&Marshal.ReadIntPtr(preloaderFocus,128)==IntPtr.Zero,"Regression setup retains the detached preloader button's native focus");
                    foreach(int code in new int[]{65,87,83,68,37,38,40,39}) {
                        string field=(code==65||code==37)?"aDown":(code==87||code==38)?"wDown":(code==83||code==40)?"sDown":"dDown";
                        WindowKey(canvas,code,true);
                        Check(KeyFlag(native,"stages.current_stage.player."+field),"First key "+code+" reaches the level without another mouse click");
                        WindowKey(canvas,code,false);
                        Check(!KeyFlag(native,"stages.current_stage.player."+field),"Level receives key up "+code);
                    }
                    Check(GameKeyboardFocus(native)==IntPtr.Zero,"Removed preloader button no longer retains native keyboard focus");
                    double before=Number(native,"stages.current_stage.player.x");WindowKey(canvas,68,true);
                    for(int i=0;i<8;i++)native.TickOnce();WindowKey(canvas,68,false);
                    Check(Number(native,"stages.current_stage.player.x")>before+1,"D moves the tutorial player immediately after entering the level");
                    native.Frame(false);native.Renderer.FinishHardware();
                    using(Bitmap image=native.Renderer.Snapshot())image.Save(Path.Combine(output,"level.png"),ImageFormat.Png);
                }
            }
            foreach(string message in log.Messages)Check(!message.Contains("Uncaught exception"),"No AS3 exceptions in menu-to-level keyboard transition");
            Check(Hash(File.ReadAllBytes(movie))==hash,"Keyboard transition test leaves source movie unchanged");
            log.Write("PASS: actual preloader and menu mouse clicks, detached native button focus, first WASD/arrow key press and release, immediate player movement, unchanged source.");
        }
    }
}
