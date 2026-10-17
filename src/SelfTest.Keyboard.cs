using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace IggyViewer {
    internal static partial class SelfTest {
        [DllImport("user32",CharSet=CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wparam,IntPtr lparam);
        static void WindowKey(Canvas canvas,int code,bool down,int scan=0,bool extended=false,bool system=false) {
            int flags=1|(scan<<16)|(extended?0x1000000:0)|(down?0:unchecked((int)0xc0000000));
            Message message=Message.Create(canvas.Handle,system?(down?0x104:0x105):(down?0x100:0x101),(IntPtr)code,(IntPtr)flags);
            if(down)Check(canvas.PreProcessControlMessage(ref message)==PreProcessControlState.MessageNeeded,"WinForms must dispatch movie key down "+code);
            SendMessage(canvas.Handle,message.Msg,message.WParam,message.LParam);
        }
        static bool KeyFlag(Native native,string field) {return ReadValue(native,field,"IggyValueGetBooleanRS")!=0;}
        public static void RunKeyboard(string dll,string fixture,string output,Log log) {
            Directory.CreateDirectory(output);string hash=Hash(File.ReadAllBytes(fixture));
            using(Native native=new Native(dll,log)) {
                native.Open(fixture,550,400);
                native.Key(65,true);
                Check(ReadValue(native,"downs")==1&&ReadValue(native,"ups")==0,"Key press dispatches KEY_DOWN, not KEY_UP");
                Check(ReadValue(native,"lastCode")==65,"AS3 receives keyCode A");
                native.Key(65,false);
                Check(ReadValue(native,"ups")==1,"Key release dispatches KEY_UP");
                native.Key(16,true,1);native.Key(66,true);
                Check(KeyFlag(native,"shifted"),"Left Shift held during B key down");
                native.Key(66,false);native.Key(16,false,1);
                Check(!KeyFlag(native,"shifted"),"Shift clears on key up");
                native.Key(16,true,2);native.Key(17,true,1);native.Key(18,true,2);
                native.Key(65,true);
                Check(KeyFlag(native,"shifted")&&KeyFlag(native,"controlled")&&KeyFlag(native,"alternate"),"AS3 receives combined modifiers");
                native.FocusLost();uint ups=ReadValue(native,"ups");
                native.ReleaseKeys();Check(ReadValue(native,"ups")==ups,"Focus loss releases each held key once");
                native.Key(65,true);
                Check(!KeyFlag(native,"shifted")&&!KeyFlag(native,"controlled")&&!KeyFlag(native,"alternate"),"Focus loss cannot leave modifiers stuck");
                native.Key(65,false);native.Key(-1,true);native.Key(256,true);
                native.Key(65,true);native.Close();native.Open(fixture,550,400);native.ReleaseKeys();
                Check(ReadValue(native,"downs")==0&&ReadValue(native,"ups")==0,"Reload does not send held keys to the new movie");
                using(PlayerForm form=new PlayerForm(native,"",log)) {
                    form.ShowInTaskbar=false;form.Opacity=0;form.Show();Application.DoEvents();
                    form.OpenFile(fixture,false);form.TestFrame(1);
                    Canvas canvas=null;FlowLayoutPanel transport=null;
                    foreach(Control control in form.Controls) {if(control is Canvas)canvas=(Canvas)control;if(control is FlowLayoutPanel)transport=(FlowLayoutPanel)control;}
                    Check(canvas!=null&&transport!=null&&canvas.Focused,"Open movie focuses the canvas");
                    uint ticks=ReadValue(native,"ticks");
                    foreach(int code in new int[]{37,38,39,40,9,13,32,65}) {
                        uint downs=ReadValue(native,"downs");ups=ReadValue(native,"ups");
                        WindowKey(canvas,code,true,0,code>=37&&code<=40);
                        Check(ReadValue(native,"downs")==downs+1&&ReadValue(native,"lastCode")==code,"GUI key down reaches AS3: "+code);
                        WindowKey(canvas,code,false,0,code>=37&&code<=40);
                        Check(ReadValue(native,"ups")==ups+1&&canvas.Focused,"GUI key up and canvas focus: "+code);
                    }
                    WindowKey(canvas,16,true,0x36);
                    Check(ReadValue(native,"location")==2&&KeyFlag(native,"shifted"),"Right Shift location from scan code");
                    WindowKey(canvas,16,false,0x36);
                    WindowKey(canvas,17,true,0x1d,true);
                    Check(ReadValue(native,"location")==2&&KeyFlag(native,"controlled"),"Right Ctrl location from extended bit");
                    WindowKey(canvas,17,false,0x1d,true);
                    WindowKey(canvas,18,true,0x38,true,true);
                    Check(ReadValue(native,"location")==2&&KeyFlag(native,"alternate"),"System key Alt reaches AS3");
                    WindowKey(canvas,18,false,0x38,true,true);
                    WindowKey(canvas,96,true,0x52);Check(ReadValue(native,"location")==3,"Numpad location");WindowKey(canvas,96,false,0x52);
                    uint repeats=ReadValue(native,"downs");
                    WindowKey(canvas,65,true);WindowKey(canvas,65,true);
                    Check(ReadValue(native,"downs")==repeats+2,"Repeated key down remains available to AS3");
                    WindowKey(canvas,65,false); // Repeated WM_KEYDOWN is intentional.
                    Check(native.TextFieldFocused,"Fixture input has native focus");
                    foreach(char ch in "ab") {
                        WindowKey(canvas,char.ToUpperInvariant(ch),true);
                        Message character=Message.Create(canvas.Handle,0x102,(IntPtr)ch,IntPtr.Zero);
                        Check(canvas.PreProcessControlMessage(ref character)==PreProcessControlState.MessageNeeded,"WinForms dispatches text input");
                        SendMessage(canvas.Handle,character.Msg,character.WParam,character.LParam);
                        WindowKey(canvas,char.ToUpperInvariant(ch),false);
                    }
                    Check(ReadString(native,"inputText")=="ab","WM_CHAR inserts text once");
                    WindowKey(canvas,8,true);WindowKey(canvas,8,false);
                    Check(ReadString(native,"inputText")=="a","Backspace is handled on key down");
                    WindowKey(canvas,16,true,0x2a);ups=ReadValue(native,"ups");
                    foreach(Control control in transport.Controls)if(control is NumericUpDown)control.Focus();
                    Check(!canvas.Focused&&ReadValue(native,"ups")==ups+1,"Toolbar focus releases held keys");
                    ((Button)transport.Controls[0]).PerformClick(); // Return focus after Play/Pause.
                    Check(canvas.Focused,"Playback button returns keyboard focus to movie");
                    WindowKey(canvas,65,true);Check(!KeyFlag(native,"shifted"),"No stuck Shift after toolbar focus");WindowKey(canvas,65,false);
                    Check(ReadValue(native,"ticks")==ticks,"Keyboard input does not advance paused timelines");
                    Check(native.Frame(false),"Draw after keyboard events");
                    using(Bitmap image=native.Renderer.Snapshot())image.Save(Path.Combine(output,"keyboard.png"),ImageFormat.Png);
                }
            }
            foreach(string message in log.Messages)Check(message.IndexOf("Uncaught exception",StringComparison.Ordinal)<0,"No AS3 keyboard exceptions");
            Check(Hash(File.ReadAllBytes(fixture))==hash,"Keyboard fixture unchanged");
            log.Write("PASS: AS3 KEY_DOWN/KEY_UP, WinForms navigation and system keys, locations/modifiers, text input/backspace, auto-repeat, canvas/toolbar focus, release on focus loss/reload, paused timelines.");
        }
    }
}
