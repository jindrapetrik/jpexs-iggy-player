# Iggy Player

A standalone Windows x64 player for 64-bit Iggy movies and the supplied
Iggy 1.2.30 and 1.2.56 runtimes. The native DLL loads movies, runs
ActionScript and produces geometry. The application provides a window,
input events and OpenGL and software implementations of GDraw. Hardware
OpenGL rendering is selected automatically when a compatible driver is
available; otherwise the player uses the software renderer.
Playback does not require FFDec, Java or conversion to SWF.

## License

The player source code is licensed under the [MIT License](LICENSE.md).
The BC7 decoder retains its original MIT copyright and license notice in
[src/Bc7Decoder.cs](src/Bc7Decoder.cs). The separately supplied proprietary
Iggy runtime DLLs are not covered by this license.

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
- **Playback > Mute sound** silences or restores audio.
- **Playback > Renderer** switches between OpenGL and software rendering.
- **Playback > Correct focal morphs** toggles compensation for the DLL's
  focal morph matrix and focus interpolation.
- **File > Save frame as PNG** saves the rendered frame.
- **File > Open log** opens the executable's diagnostic log.

Click the canvas to give the movie keyboard focus. Arrow keys, Tab, Enter,
modifiers and text input are forwarded to the movie. Editing the frame
field keeps keyboard focus there. Held keys are released on focus loss.
Before dispatching keys or text, the host clears native focus retained by
a removed display object so events reach Stage after scene transitions.
Attached text fields and reattached subtrees retain their focus.

Pausing stops timelines and active audio channels while retaining mouse interaction and text editing.
Input is dispatched directly without advancing a simulation frame. Playback
uses a monotonic clock and catches up due frames after slow rendering.

Buttons and sprites with buttonMode=true display a hand cursor, including
when the pointer is stationary. Disabled buttons and useHandCursor=false
are respected. Mouse.hide() and Mouse.show() control the system cursor over
the canvas so movies can draw their own cursor. Toolbars, menus and other
windows retain a visible cursor.

## Audio

Normal playback enables the DLL's mixer through Windows WaveOut. Embedded
DefineSound assets support PCM (8/16 bit), SWF ADPCM and MP3, triggered by
timeline StartSound/StartSound2 commands or AS3 Sound.play(). The native
mixer handles concurrent channels, volume/panning, looping and stopping.
MP3 uses a host bridge to the Windows ACM decoder; a separate Iggy MP3 DLL
is not required. A missing ACM codec or invalid MP3 is reported in the log
without aborting the movie. MP3 is decoded to PCM when a channel opens,
with limits of 32 MiB encoded and 256 MiB decoded data per channel.

Playback pause/resume and the Mute sound menu also apply to audio. Closing
or reloading a movie releases its channels. PNG export and noninteractive
render modes remain silent. SoundStreamHead/Head2/Block, external sound
loading, Nellymoser and Speex are not implemented.

AS3 sound behavior otherwise follows the supplied DLL: Sound.play's loop
argument counts total iterations (0 and 1 both play once), and natural
completion does not dispatch SOUND_COMPLETE. These runtime differences
from Flash Player are not emulated by the audio output bridge.

## Hardware acceleration

The OpenGL backend draws geometry, bitmaps, gradients, stencil masks,
blend modes and filters on the GPU. Intermediate surfaces stay in GPU
memory, and the canvas presents the final texture directly. PNG export
and screenshot capture read the resulting pixels back only when requested.
Large blur radii use the software filter as a fallback.

Install the GPU manufacturer's OpenGL driver. The backend requires shader
and framebuffer support, textures of at least 8192 pixels and renderbuffers
of at least 4096 pixels. Software OpenGL drivers are rejected. The selected
backend appears in the status bar; the log records the GPU and driver.
To select a backend explicitly:

~~~powershell
.\IggyPlayer.exe --renderer opengl "test-data\graphics.iggy"
.\IggyPlayer.exe --renderer software "test-data\graphics.iggy"
~~~

The default is --renderer auto. An explicit opengl request reports an
initialization error if acceleration is unavailable. The renderer menu
also allows switching back to software while a movie is open.

See [OPENGL.md](OPENGL.md) for implementation details, comparison tests
and measured performance.

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
.\test.ps1 -TestOpenGl
.\test.ps1 -TestAudio
.\test.ps1 -DllPath "$PWD\lib\iggy_w64.dll" -OutputDirectory "$PWD\test-output\legacy"
~~~

The default suite uses local test-data fixtures. It covers all 67 graphics
frames, seeking, gradients, scaling grids, color transforms, filters, text,
AS3, keyboard input, cursor visibility, playback timing, LoaderInfo, runtime
services, resource lifetime and GUI captures. It checks that source movies
remain unchanged. Results and screenshots are written to test-output.
Each executable writes its own .log file.

The optional -TestOpenGl suite requires hardware OpenGL. It compares all
67 graphics frames against the software renderer, checks alpha filters,
direct presentation, backend switching, resizing and resource lifetime,
and measures both backends at 1600 x 1000.

The optional -TestAudio suite produces short audible tones and requires a
working Windows audio device and MP3 ACM codec. It verifies actual mixed
PCM output and pitch for PCM8/PCM16/ADPCM and mono/stereo MP3, timeline/AS3
triggers, looping, pause/resume, stop/stopAll, mute, GC and close/reopen.

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
IggyPlayer.exe --self-test-audio test-data/audio.iggy output-directory
IggyPlayer.exe --self-test-fixup-partitions test-data/graphics.iggy output-directory
IggyPlayer.exe --self-test-zero-scale test-data/zero-scale.iggy output-directory
IggyPlayer.exe --self-test-clock test-data/graphics.iggy output-directory
IggyPlayer.exe --self-test-opengl test-data/graphics.iggy output-directory
IggyPlayer.exe --self-test-focal-instances test-data/focal-instances.iggy output-directory
IggyPlayer.exe --ui-test test-data/graphics.iggy window.png 47
IggyPlayer.exe --native-focal --render test-data/graphics.iggy native63.png 960 640 1 63
~~~

Optional fixture-specific modes are --self-test-fixups,
--self-test-game-playback, --self-test-game-keyboard, --self-test-play-response and
--self-test-level-exit. Each takes an input movie and output directory.
The --dll PATH option overrides automatic DLL lookup for any mode.
The --renderer auto|opengl|software option selects the backend for normal
playback, --render, --render-all and --ui-test. Regression modes select
their own backends to keep reference tests deterministic.
--self-test-game-keyboard uses the selected renderer and checks the actual
preloader/menu button transition, first WASD/arrow input and player movement.
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
compacted and split into consecutive entries for the same data block,
each fitting the DLL's 64 KiB buffer. Template headers, absolute cursor
positions and operation order are preserved, and the directory is rebuilt.
Empty style tables are normalized,
text-only movies receive a minimal AS3 context when needed, and inherited
placement matrices are prepared for native seeking. VM wrappers handle
unsupported or incorrect runtime properties. Source movies and DLL files
are not rewritten. Successful playback does not prove that an unmodified
DLL host can load the original container without these fixes.

FileReferenceList subclasses can be initialized through a runtime shim.
The host has no file picker: browse requests asynchronously dispatch CANCEL,
keep an empty file list, and never report selected files.

Video and IME are not connected. The older runtime lacks some
3D APIs. Arbitrary seeking does not reproduce all AS3 side effects of
sequential playback. Movies can require host services and resources beyond
the implemented compatibility layer. The BC7 decoder retains its original
MIT license and author attribution in the source.
