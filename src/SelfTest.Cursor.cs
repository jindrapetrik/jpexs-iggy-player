using System;
using System.IO;
using System.Windows.Forms;
namespace IggyViewer {
    internal static partial class SelfTest {
        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)] delegate int CursorRefCount(IntPtr player);
        [System.Runtime.InteropServices.DllImport("user32")] static extern IntPtr GetCursor();
        [System.Runtime.InteropServices.DllImport("user32",EntryPoint="SetCursor")] static extern IntPtr RestoreCursor(IntPtr cursor);
        public static void RunCursor(string dll,string movie,string output,Log log) {
            Directory.CreateDirectory(output);
            using(Native native=new Native(dll,log)) {
                native.Open(movie,550,400);native.SetPaused(true);native.Frame(false);
                Check(!native.HandCursor,"No hover before mouse entry");
                Check(native.HideSystemCursor,"Constructor Mouse.hide takes effect before mouse entry");
                native.MouseMove(40,40);Check(!native.HandCursor&&!KeyFlag(native,"ordinary.buttonMode"),"Ordinary Sprite keeps arrow and defaults buttonMode to false");
                Check(native.HideSystemCursor,"Mouse.hide in the constructor is retained before the mouse enters");
                Call(native,"","hideMouse");Call(native,"","hideMouse");
                Call(native,"","showMouse");Check(!native.HideSystemCursor,"Mouse.show cancels repeated Mouse.hide calls without a visibility counter");
                native.MouseMove(160,40);Check(native.HandCursor,"Sprite buttonMode selects hand");
                SetNumber(native,"hand.useHandCursor",0);Check(!native.HandCursor&&!KeyFlag(native,"hand.useHandCursor"),"useHandCursor false disables hand");
                SetNumber(native,"hand.useHandCursor",1);Check(native.HandCursor&&KeyFlag(native,"hand.useHandCursor"),"useHandCursor true restores hand");
                native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);GC.Collect();GC.WaitForPendingFinalizers();
                Check(native.HandCursor&&KeyFlag(native,"hand.buttonMode"),"Cursor properties survive native and managed collection");
                SetNumber(native,"hand.buttonMode",0);Check(!native.HandCursor,"Changing buttonMode refreshes cursor without moving mouse");
                SetNumber(native,"hand.buttonMode",1);Check(native.HandCursor,"Restoring buttonMode selects hand");
                for(int i=0;i<10;i++){native.TickOnce();native.Frame(false);Check(native.HandCursor,"Sprite hand persists without mouse movement: "+i);}
                SetNumber(native,"hand.x",20);native.TickOnce();Check(!native.HandCursor,"Moving the hovered Sprite away restores arrow without mouse movement");
                SetNumber(native,"hand.x",140);native.TickOnce();Check(native.HandCursor,"Moving the Sprite back under the stationary mouse restores hand");
                native.MouseMove(280,40);Check(native.HandCursor,"Child inherits ancestor button cursor");
                native.MouseMove(400,40);Check(native.HandCursor,"SimpleButton selects hand");
                SetNumber(native,"button.useHandCursor",0);Check(!native.HandCursor,"SimpleButton useHandCursor false disables hand");
                SetNumber(native,"button.useHandCursor",1);
                SetNumber(native,"button.enabled",0);Check(!native.HandCursor,"Disabled SimpleButton keeps arrow");
                SetNumber(native,"button.enabled",1);
                for(int i=0;i<10;i++){native.TickOnce();native.Frame(false);Check(native.HandCursor,"SimpleButton hand persists without mouse movement: "+i);}
                native.MouseMove(160,40);native.MouseButton(0,true);Check(native.HandCursor,"Button press keeps hand");native.MouseButton(0,false);
                native.MouseLeave();Check(!native.HandCursor,"Leaving canvas restores arrow");
                native.MouseMove(160,40);Call(native,"","removeHand");Check(!native.HandCursor,"Removed hovered object restores arrow");
                int references=native.Bind<CursorRefCount>("IggyDebugGetNumValueRef")(native.Player);
                for(int i=0;i<50;i++)Call(native,"","discard");
                // The DLL releases orphan display instances during ticks;
                // subsequent native GC clears their weak AS3 references.
                for(int i=0;i<5;i++){native.TickOnce();native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);native.Frame(false);}
                Check(native.Bind<CursorRefCount>("IggyDebugGetNumValueRef")(native.Player)<=references,"Weak cursor state does not retain discarded AS3 objects or leak ValueRefs");
                Call(native,"","hideMouse");native.Close();Check(!native.HandCursor&&!native.HideSystemCursor,"Close clears hover and script cursor visibility");
                using(PlayerForm form=new PlayerForm(native,"",log)) {
                    form.ShowInTaskbar=false;form.Opacity=0;form.Show();Application.DoEvents();
                    form.OpenFile(movie,false);form.TestFrame(1);
                    Canvas canvas=null;foreach(Control control in form.Controls)if(control is Canvas)canvas=(Canvas)control;
                    Check(canvas!=null,"Canvas exists");uint ticks=ReadValue(native,"ticks");
                    int x=160*canvas.Width/550,y=40*canvas.Height/400;
                    SendMessage(canvas.Handle,0x200,IntPtr.Zero,(IntPtr)(x|(y<<16)));
                    Check(canvas.MovieCursorHidden,"GUI respects constructor Mouse.hide above a hand button");
                    IntPtr previousCursor=GetCursor();
                    try {
                        Check(SendMessage(canvas.Handle,0x20,canvas.Handle,(IntPtr)1)==(IntPtr)1&&GetCursor()==IntPtr.Zero,"WM_SETCURSOR hides the actual system cursor in the canvas");
                        Control toolbar=form.Controls[1];
                        SendMessage(toolbar.Handle,0x20,toolbar.Handle,(IntPtr)1);
                        Check(GetCursor()!=IntPtr.Zero,"Toolbar keeps a visible cursor while the game requests Mouse.hide");
                        for(int i=0;i<5;i++){form.TestRenderFrame();Check(canvas.MovieCursorHidden,"Hidden cursor persists over paused redraw: "+i);}
                        SendMessage(form.Handle,0x6,IntPtr.Zero,IntPtr.Zero);
                        Check(!canvas.MovieCursorHidden,"Deactivation restores the system cursor");
                        SendMessage(form.Handle,0x6,(IntPtr)1,IntPtr.Zero);
                        Check(canvas.MovieCursorHidden,"Activation restores the game's hidden cursor request");
                        SendMessage(canvas.Handle,0x2a3,IntPtr.Zero,IntPtr.Zero);
                        Check(native.HideSystemCursor,"Mouse leave preserves the game's visibility request for reentry");
                        SendMessage(toolbar.Handle,0x20,toolbar.Handle,(IntPtr)1);
                        Check(GetCursor()!=IntPtr.Zero,"Cursor remains visible outside the canvas after mouse leave");
                        SendMessage(canvas.Handle,0x200,IntPtr.Zero,(IntPtr)(x|(y<<16)));
                        Check(canvas.MovieCursorHidden,"Reentering canvas reapplies Mouse.hide");
                        Call(native,"","showMouse");form.TestRenderFrame();
                        SendMessage(canvas.Handle,0x20,canvas.Handle,(IntPtr)1);
                        Check(!canvas.MovieCursorHidden&&GetCursor()!=IntPtr.Zero,"Mouse.show restores the system cursor without moving the mouse");
                    } finally {RestoreCursor(previousCursor);}
                    Check(canvas.Cursor==Cursors.Hand,"WinForms mouse move displays actual system hand cursor");
                    for(int i=0;i<10;i++){form.TestRenderFrame();Check(canvas.Cursor==Cursors.Hand,"WinForms hand persists over paused redraw: "+i);}
                    SendMessage(canvas.Handle,0x200,IntPtr.Zero,(IntPtr)(5|(5<<16)));
                    Check(canvas.Cursor==Cursors.Default,"Empty canvas restores actual arrow");
                    SendMessage(canvas.Handle,0x200,IntPtr.Zero,(IntPtr)(x|(y<<16)));
                    SendMessage(canvas.Handle,0x2a3,IntPtr.Zero,IntPtr.Zero);
                    Check(canvas.Cursor==Cursors.Default,"WinForms mouse leave restores arrow");
                    Check(ReadValue(native,"ticks")==ticks,"Cursor events do not advance paused timelines");
                    Call(native,"","hideMouse");form.TestRenderFrame();
                    form.OpenFile(movie,false);Check(native.HideSystemCursor&&canvas.MovieCursorHidden,"Reopen applies constructor Mouse.hide without requiring mouse movement");
                    native.MouseMove(40,40);Check(native.HideSystemCursor,"Reopened constructor Mouse.hide works");
                    form.OpenFile(Path.Combine(Path.GetDirectoryName(movie),"keyboard.iggy"),false);
                    native.MouseMove(40,40);form.TestRenderFrame();Check(!native.HideSystemCursor&&!canvas.MovieCursorHidden,"Loading another movie clears the previous Mouse.hide request");
                }
            }
            foreach(string message in log.Messages)Check(!message.Contains("Uncaught exception"),"No AS3 cursor exceptions");
            log.Write("PASS: Sprite buttonMode, nested child, SimpleButton enabled, stationary hand, Mouse.hide/show and constructor hide, actual WM_SETCURSOR, toolbar/leave/activation, paused timelines, GC and reload.");
        }
    }
}
