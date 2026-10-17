# Iggy Player

A standalone Windows x64 player for 64-bit Iggy movies and the supplied
Iggy 1.2.30 and 1.2.56 runtimes. The native DLL loads movies, runs
ActionScript and produces geometry. The application provides a window,
input events and an experimental software implementation of GDraw.
Playback does not require FFDec, Java or conversion to SWF.

## Requirements and DLL setup

Windows x64 and .NET Framework 4.x are required. Place the supported DLL
in the lib directory beside the executable. Automatic lookup uses:

1. lib/iggy_w64_final.dll
2. lib/iggy_w64.dll, only when the first file is absent

Paths are relative to the executable, independently of the current working
directory. Other directories are not searched. To override lookup:

~~~powershell
.\IggyPlayer.exe --dll "C:\path\to\iggy_w64.dll" "test-data\graphics.iggy"
~~~

The binding supports the two analyzed binaries; renaming another DLL does
not make it compatible. DLLs are ignored by Git. The lib/.gitkeep file
preserves the directory in a source checkout.

## Running

Run these examples from the player directory:

~~~powershell
.\IggyPlayer.exe "test-data\graphics.iggy"
~~~

Alternatively, use **File > Open**, drag an .iggy file into the window,
or run OpenGraphics.cmd.

The original SWF reference is test-data/graphics.swf. Tools and fixture
generators read that file; native playback uses its converted counterpart,
test-data/graphics.iggy.

Controls:

- **Play / Pause** starts or pauses playback.
- The previous/next buttons and frame field seek to a frame, numbered from 1.
- **Playback > Reload** reopens the current file.
- **Playback > Correct focal morphs** toggles compensation for the DLL's
  focal morph matrix and focus interpolation.
- **File > Save frame as PNG** saves the rendered frame.
- **File > Open log** opens the executable's diagnostic log.

Click the canvas to give the movie keyboard focus. Arrow keys, Tab, Enter,
modifiers and text input are forwarded to the movie. Editing the frame
field keeps keyboard focus there. Held keys are released on focus loss.

Pausing stops timelines while retaining mouse interaction and text editing.
Input is dispatched directly without advancing a simulation frame. Playback
uses a monotonic clock and catches up due frames after slow rendering.

Buttons and sprites with buttonMode=true display a hand cursor, including
when the pointer is stationary. Disabled buttons and useHandCursor=false
are respected. Mouse.hide() and Mouse.show() control the system cursor over
the canvas so movies can draw their own cursor. Toolbars, menus and other
windows retain a visible cursor.

## Imports, textures and runtime services

Imported libraries are resolved relative to the movie. A URL ending in .swf
also tries the corresponding .iggy. Libraries are registered under the
original URL. Companion textures are searched as NAME.raw.iggytex and
then NAME.iggytex beside the movie and each imported library. Supported
external texture formats are RGBA8 and BC7.

Embedded fonts take precedence over Windows Times New Roman, Arial and
Courier New and their generic aliases. Bitmap font caching is disabled;
the DLL produces vector glyphs.

SharedObject.getLocal uses a private persistent format under sharedobjects,
separated by movie path. Existing Flash .sol files are not imported. The
Loader compatibility layer reports asynchronous IOErrorEvent failures;
network transfers and successful dynamic movie loading are not implemented.
LoaderInfo counters report the original size of the synchronously loaded
Iggy file.

## Building

~~~powershell
.\build.ps1
~~~

The script uses the x64 C# compiler included with Windows .NET Framework.
It compiles src/*.cs into IggyPlayer.exe and embeds runtime/sharedobject.abc
and runtime/loader.abc. Keep these resources in version control. To build
a separate executable while another is open:

~~~powershell
.\build.ps1 -OutputPath "$PWD\IggyPlayer.updated.exe"
~~~

Rebuilding the optional runtime shims requires a compatible FFDec build
and a JDK. The checked-in ABC resources are sufficient for a normal build.

~~~powershell
.\runtime\build.ps1 -FFDecJar "C:\path\to\ffdec.jar" -JavaHome $env:JAVA_HOME
~~~

## Testing

~~~powershell
.\test.ps1
.\test.ps1 -PlayerPath "$PWD\IggyPlayer.updated.exe"
.\test.ps1 -DllPath "$PWD\lib\iggy_w64.dll" -OutputDirectory "$PWD\test-output\legacy"
~~~

The default suite uses local test-data fixtures. It covers all 67 graphics
frames, seeking, gradients, scaling grids, color transforms, filters, text,
AS3, keyboard input, cursor visibility, playback timing, LoaderInfo, runtime
services, resource lifetime and GUI captures. It checks that source movies
remain unchanged. Results and screenshots are written to test-output.
Each executable writes its own .log file.

Additional game-specific regression modes are available for a compatible
external fixture. They depend on its timeline and AS3 object layout and
are skipped by default:

~~~powershell
.\test.ps1 -GameMovie "C:\path\to\game-fixture.iggy"
~~~

See [test-data/README.md](test-data/README.md) for sources and regeneration
commands. Normal native tests do not require Java or FFDec.

## Command-line modes

~~~text
IggyPlayer.exe --render input.iggy output.png [width height [draws [frame]]]
IggyPlayer.exe --render-all input.iggy output-directory [width height]
IggyPlayer.exe --self-test test-data/graphics.iggy output-directory
IggyPlayer.exe --self-test-seek test-data/graphics.iggy output-directory
IggyPlayer.exe --self-test-as3 test-data/as3.iggy output-directory
IggyPlayer.exe --self-test-filters test-data/filters.iggy output-directory
IggyPlayer.exe --self-test-keyboard test-data/keyboard.iggy output-directory
IggyPlayer.exe --self-test-cursor test-data/cursor.iggy output-directory
IggyPlayer.exe --self-test-loaderinfo test-data/loaderinfo.iggy output-directory
IggyPlayer.exe --self-test-runtime test-data/runtime.iggy output-directory
IggyPlayer.exe --self-test-zero-scale test-data/zero-scale.iggy output-directory
IggyPlayer.exe --self-test-clock test-data/graphics.iggy output-directory
IggyPlayer.exe --self-test-focal-instances test-data/focal-instances.iggy output-directory
IggyPlayer.exe --ui-test test-data/graphics.iggy window.png 47
IggyPlayer.exe --native-focal --render test-data/graphics.iggy native63.png 960 640 1 63
~~~

Optional fixture-specific modes are --self-test-fixups,
--self-test-game-playback, --self-test-play-response and
--self-test-level-exit. Each takes an input movie and output directory.
The --dll PATH option overrides automatic DLL lookup for any mode.
Noninteractive modes return 0 on success and 1 on failure without error
dialogs.

## Compatibility and limitations

Only 64-bit containers are supported; 32 legacy and 32 files are rejected.
The renderer implements vector shapes, morphs, bitmaps, gradients, masks,
blending, color matrices, blur, shadow/glow and bevel filters, including
gradient and knockout variants. Antialiasing and blur can differ from the
original renderer. Native morph gradient palette caching can also produce
differences between instances sharing a definition.

Compatibility changes are applied in memory: relocation programs are
compacted for the DLL's 64 KiB buffer, empty style tables are normalized,
text-only movies receive a minimal AS3 context when needed, and inherited
placement matrices are prepared for native seeking. VM wrappers handle
unsupported or incorrect runtime properties. Source movies and DLL files
are not rewritten. Successful playback does not prove that an unmodified
DLL host can load the original container without these fixes.

Audio output, video and IME are not connected. The older runtime lacks some
3D APIs. Arbitrary seeking does not reproduce all AS3 side effects of
sequential playback. Movies can require host services and resources beyond
the implemented compatibility layer. The BC7 decoder retains its original
MIT license and author attribution in the source.
