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

The host corrects the supplied DLLs' AS3 loop counting: Sound.play(0, 0)
plays once, Sound.play(0, 1) plays twice, and so on. Natural completion
dispatches one SOUND_COMPLETE on the original SoundChannel during a movie
tick, after its final iteration. Pause postpones delivery; stop(), stopAll(),
timeline stop commands and closing the movie do not dispatch completion.
Listeners can start or stop other sounds, and completed/stopped channels
cannot accidentally stop a new sound that reuses their native voice slot.

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

SharedObject.getLocal uses a private, versioned AMF format under sharedobjects,
separated by movie path. It persists object graphs, cycles, shared references,
ByteArrays and the other types supported by readObject/writeObject below.
SharedObject.objectEncoding and its independent static defaultObjectEncoding
accept 0 or 3, defaulting to AMF3; the default is captured by each new instance.
Register class aliases before getLocal to restore typed/externalizable values.
Existing IGGY1 text saves remain readable and migrate only after a successful
flush(). Writes atomically replace the previous file; failed reads/writes preserve
it. Corrupt files raise catchable AS3 errors. clear() deletes the file and empties
the same data object, preserving references held by scripts. AMF data is limited
to 16 MiB per save. Large payloads use a direct host bridge to avoid the DLL's
stack-based string conversion. Saving requires an explicit flush(); automatic
saving on shutdown, remote SharedObjects, quotas and exact .sol size reporting
are not implemented. Existing Flash .sol files are not imported.

The Loader compatibility layer loads local PNG/JPEG images and 64-bit IGGY movies
asynchronously. Absolute local paths and file: URLs are also
accepted, including escaped Unicode names and URL queries. Loaded content is
a Bitmap or MovieClip child with LoaderInfo dimensions, MIME type, URL and byte counts.
IGGY movies run their own AS3 and timeline in the parent's VM and display tree;
their imported libraries and companion textures are resolved locally. Requests
for NAME.swf use NAME.iggy when that converted file is present. Raw SWF files
are not converted during playback. Relative URLs use the main movie's directory
or the loaded movie's directory for loaders constructed in its constructor or
attached below its root. Detached loaders created later fall back to the main
movie's directory; use an absolute URL in that case.
loadBytes(ByteArray, LoaderContext) accepts PNG, JPEG and compiled 64-bit IGGY
data. It snapshots the entire array, regardless of position, before returning;
later writes to the caller's array do not affect decoding. Embedded
DefineBinaryData ByteArray subclasses and writable arrays are supported.
ByteArray compatibility implements length, position, bytesAvailable, endian,
readBytes/writeBytes, and Boolean, signed/unsigned integer, Float and Double
reading/writing. Endian defaults to bigEndian for each instance. IEEE values
include negative zero, subnormals, infinities and NaN. readUTF/writeUTF use a
16-bit byte-length prefix in the selected endian; readUTFBytes/writeUTFBytes
handle strings without a prefix. UTF reads skip a UTF-8 BOM and terminate the
returned string at NUL while consuming the full requested range. toString()
also recognizes UTF-16 BOMs and leaves position unchanged. readMultiByte and
writeMultiByte use Windows-supported character sets, falling back to the system
code page for unknown names. Reads past the end raise EOFError; invalid bounds,
endian values and null strings raise the corresponding typed AS3 errors.
compress()/uncompress() implement zlib by default and accept "deflate" for raw
DEFLATE data. Compression covers the whole array and sets position to the end;
decompression sets position to zero. Headers, block structure, termination,
distances and zlib checksums are checked before replacing the input. Failed
decompression leaves bytes and position unchanged; output is limited to 256 MiB.
Endian and CompressionAlgorithm constants are supplied by the host compatibility
layer. These methods also work in movies that do not reference Loader.
readObject/writeObject support AMF0 and AMF3 primitives, UTF-8 strings, objects,
arrays (including associative entries), dates, shared references and cycles.
AMF3 also supports nested ByteArrays, XML/XMLDocument, all four Vector types and
Dictionary entries with object keys. AMF0 supports XMLDocument and uses the AMF3
escape for ByteArrays, vectors, dictionaries and externalizable objects.
objectEncoding and the static defaultObjectEncoding accept 0 or 3; the default
is AMF3 and is captured when an array is constructed. AMF always uses its protocol
byte order, independently of endian. Each call starts fresh reference tables.
registerClassAlias/getClassByAlias support typed objects with inherited public
variables, readable/writable properties and dynamic fields, excluding [Transient]
members. Alias lookup searches the current application domain and its parents;
isolated loaded domains have separate registrations. Unknown aliases decode as
anonymous objects. Failed writes leave the destination unchanged; failed reads
advance position by the consumed bytes. Each operation is limited to 16 MiB,
32 nesting levels and 100000 collection entries. IExternalizable classes require
a registered alias; their readExternal/writeExternal callbacks receive bounded
IDataInput/IDataOutput streams. Nested readObject/writeObject calls share the
enclosing reference tables, preserving cycles and references across callbacks.
Retained streams are invalid after the callback returns. ByteArray also implements
IDataInput/IDataOutput. Vector element types, fixed lengths and Dictionary's
serialized weak-keys flag survive AMF round trips. The native Dictionary still
retains its keys; this does not implement weak-key garbage collection.
XML support currently covers construction from text, toString/toXMLString and
formatting/ignore settings; XMLDocument provides construction, parseXML and
toString. Full DOM/E4X operations are not implemented. XML input is bounded to
16 MiB and 128 nested levels; DTDs are rejected. Native bytecode casts to nested
vector types remain unsupported, although those vectors can be serialized.
Custom dynamic-property writers are not implemented. Unsupported types and
malformed references raise typed AS3 errors.
LZMA and newer ByteArray APIs absent from the DLL (including clear, deflate and
inflate aliases) are not implemented. Use compress("deflate")/uncompress("deflate")
for raw DEFLATE.
Writable buffers detach from embedded data on the first write and are released
by native collection or movie shutdown. Memory-loaded movies have a synthetic
file URL based on their content hash in the loading movie's directory, which
also supplies the base for relative imports and resource requests. They use the
same application domains and lifecycle as file loads. Either allowCodeImport=false
or allowLoadBytesCodeExecution=false rejects IGGY byte loads; images remain
allowed. Pending byte snapshots are limited to 256 MiB per movie, with a maximum
of 256 MiB for each ByteArray. Raw SWF bytes require conversion before loading.
OPEN, PROGRESS, INIT and COMPLETE are dispatched on contentLoaderInfo, in that
order; missing, invalid and unsupported resources dispatch IOErrorEvent.
close() cancels pending work without removing loaded content. unload() and
unloadAndStop() cancel pending work, remove content and dispatch UNLOAD when
content was present; loaded root timelines are stopped. unloadAndStop() also
stops descendant and detached movie clips, owned timers and interval/timeout
callbacks, removes frame/broadcast and Stage listeners, cancels nested pending
loads and stops that instance's AS3 and timeline sounds. Nested Loaders unload
their content as well. Other loaded instances and the parent keep running.
The gc argument requests deferred native collection after AS3 dispatch;
externally retained BitmapData clones keep their pixel storage.
setInterval/setTimeout and their clear functions are implemented through native
Timers, including callback arguments and movies that do not use Loader.
Event listeners may close, unload or replace a request;
stale requests do not dispatch later completion events. Pixel and texture
storage is reclaimed when native GC collects the last BitmapData clone.
Local image limits are 32 MiB encoded data per file, 3840 pixels per dimension,
32 pending requests and 128 MiB of live or pending host pixel storage per movie.
Two worker threads decode files; VM access and event delivery stay on the
movie thread. IGGY files are limited to 256 MiB each. The native runtime permits
32 registered libraries, including imports; loaded definitions remain cached
until the main movie closes. LoaderContext.applicationDomain selects a domain;
omitting it creates a new child of the calling code's domain. Repeated loads of
an unchanged file into the same explicit domain reuse classes and static fields.
Separate domains keep those classes and fields independent. ApplicationDomain
provides currentDomain, parentDomain, hasDefinition and getDefinition for public
classes, with parent lookup before local lookup. LoaderInfo.applicationDomain
and LoaderContext.parameters are available to child constructors. Setting
allowCodeImport=false rejects IGGY movies before linking while allowing images.
getDefinitionByName uses the calling code's domain; getQualifiedClassName and
getQualifiedSuperclassName report original names rather than internal aliases.
Class references reuse definitions from the parent domain before local ones.
Once a class is loaded, another file in the same domain cannot replace it or
reset its static fields. Imported libraries, including nested imports, are cached
by domain, resolved file and contents. Identical import URLs in different folders
can resolve to different libraries. Their AS3 classes, static initializers and
timeline class-name placements use the target domain; already-bound resource
classes retain their original definitions. This import isolation applies to
movies loaded through Loader. Use a domain with the system parent
to load a movie whose document class has the same name as the parent's class.
ApplicationDomain reflection includes user-script functions, variables, constants
and namespaces, including startup and nested imports. Variable lookups read the
current value; hasDefinition checks existence without running the initializer,
including definitions whose values are null, undefined, zero or false.
Scripts with globals use an internal class trait to retain their lazy initializer
in the native VM. These helpers are not listed as movie definitions.
Repeated getDefinition calls retain the same reflected Function object, but its
identity can differ from a separately bound lexical reference in this DLL.
domainMemory and complete enumeration of native system globals are unavailable.
When Loader/domain compatibility is needed, startup class-symbol binding runs
after host compatibility callbacks are installed.
Static initializers in the main movie and its imported libraries can store
ApplicationDomain.currentDomain before the document constructor runs.
Initializers in dynamically loaded libraries run in the selected domain.
getQualifiedDefinitionNames currently returns
an Array of local public definition names, since this DLL does not provide Vector.
Non-null securityDomain and requestedContentParent are rejected explicitly.
checkPolicyFile has no effect for local files. Network transfers,
loadBytes() and GIF images are not implemented. The supplied DLL's other stubbed
BitmapData drawing/pixel-editing methods remain unsupported.
LoaderInfo counters report the original size of the synchronously loaded
Iggy file.

## Building

~~~powershell
.\build.ps1
~~~

The script uses the x64 C# compiler included with Windows .NET Framework.
It compiles src/*.cs into IggyPlayer.exe and embeds runtime/sharedobject.abc,
runtime/loader.abc and runtime/audio.abc. Keep these resources in version control. To build
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
triggers, Flash loop counts, SOUND_COMPLETE targets/flags and chained listeners,
single argument coercion, pause/resume, stop/stopAll and timeline cancellation,
voice-slot reuse, mute, GC and close/reopen.

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
IggyPlayer.exe --self-test-sharedobject test-data/sharedobject.iggy output-directory
IggyPlayer.exe --self-test-loader test-data/loader.iggy output-directory
IggyPlayer.exe --self-test-movie-loader test-data/movie-loader.iggy output-directory
IggyPlayer.exe --self-test-domains test-data/domains.iggy output-directory
IggyPlayer.exe --self-test-loader-bytes test-data/bytes-loader.iggy output-directory
IggyPlayer.exe --self-test-bytearray test-data/bytearray.iggy output-directory
IggyPlayer.exe --self-test-amf test-data/amf.iggy output-directory
IggyPlayer.exe --self-test-unload test-data/movie-loader.iggy output-directory
IggyPlayer.exe --self-test-unload-audio test-data/movie-loader.iggy output-directory
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
