# Native player regression fixtures

Run commands in this document from the player directory. The default
test.ps1 suite uses files in test-data and the runtime DLL in lib. Java
and FFDec are needed only when regenerating fixtures.

## Graphics and AS3 references

The original graphics reference is test-data/graphics.swf. Tools and
generators read it here; native playback uses graphics.iggy, its 64-bit
conversion. The movie has 67 frames and a 500 x 400 stage. Regenerate the
converted reference with a compatible FFDec CLI:

~~~powershell
java -jar PATH_TO_FFDEC_CLI_JAR -swf2iggy test-data/graphics.swf test-data/graphics.iggy 64
~~~

The as3.swf template provides a Main document class and stage settings for
script import tests. The as3.iggy movie exercises device-font text created
by AS3.

## AS3 API fixtures

The cursor, keyboard, loaderinfo, runtime and zero-scale directories contain
scripts/Main.as. Their .swf and .iggy files are kept alongside the graphics
references. Regenerate a fixture with:

~~~powershell
java -jar PATH_TO_FFDEC_CLI_JAR -importScript test-data/as3.swf test-data/cursor.swf test-data/cursor/scripts
java -jar PATH_TO_FFDEC_CLI_JAR -swf2iggy test-data/cursor.swf test-data/cursor.iggy 64
~~~

Replace cursor with another fixture name as needed, except runtime, whose
additional subclass requires the generator in its runtime API notes.
Tests cover hand cursors
and Mouse.hide/show, keyboard events, LoaderInfo counters, SharedObject
persistence, Loader errors and assignments to a zero-length scaleY axis.
The keyboard fixture also removes a focused subtree and text field, checks
that the next input reaches Stage, and verifies focus remains intact for
attached or reattached objects. It runs with software and automatic rendering.
Non-ASCII strings are intentional Unicode persistence test data. See the
[LoaderInfo notes](loaderinfo/README.md) and
[runtime API notes](runtime/README.md).

## Local Loader images

loader.swf and loader.iggy use loader/scripts/Main.as and the small generated
images in loader/. The fixture checks asynchronous PNG/JPEG loads, URL-escaped
Unicode paths and queries, native colors and alpha, content and LoaderInfo,
event order, missing files, replacement requests, close/unload during callbacks,
clone lifetime, pixel reclamation and loading again after unload. The default
suite uses software rendering; -TestOpenGl also runs it on the GPU.

Regenerate the SWF and IGGY with a compatible FFDec library:

~~~powershell
$classpath = 'PATH_TO_FFDEC_JAR;PATH_TO_FFDEC_LIB/*'
.\tools\CreateLoaderImages.ps1
javac -encoding UTF-8 -cp $classpath tools/CreateLoaderFixture.java
java -cp "tools;$classpath" CreateLoaderFixture test-data/as3.swf test-data/loader
~~~

The PNG has red/green/blue stripes and a half-transparent red stripe; the
JPEG is cyan. The second PNG is an identical copy under a Unicode filename.

## Audio

The audio.swf and audio.iggy fixtures contain generated sine tones in PCM8,
PCM16, SWF ADPCM and mono/stereo MP3. They exercise native timeline sounds,
AS3 Sound.play(), loops, concurrent channels, pause, mute, stop and resource
lifetime. See [audio/README.md](audio/README.md) for regeneration and controls.

## Filters

The filters.swf and filters.iggy fixtures exercise translucent color
matrices, including alpha offsets and chaining, blur quality 1/3, and
gradient glow knockout with half-transparent blue palette stops. The native
filter test runs at 500 x 400 and 1000 x 800.

Regenerate with a compatible FFDec Java library and its dependencies:

~~~powershell
$classpath = 'PATH_TO_FFDEC_JAR;PATH_TO_FFDEC_LIB/*'
javac -encoding UTF-8 -cp $classpath tools/CreateFilterFixture.java
java -cp "tools;$classpath" CreateFilterFixture test-data/filters
~~~

## Focal morph

The focal-instances.iggy fixture contains morph 174 from graphics.swf with
two simultaneous instances. Frame 1 uses ratios 0 and 65535; frame 2 uses
21845 and 43691. It includes no scripts or imports. This is generated test
data, not an official compiler reference. Regenerate it with:

~~~powershell
$classpath = 'PATH_TO_FFDEC_JAR;PATH_TO_FFDEC_LIB/*'
javac -encoding UTF-8 -cp $classpath tools/CreateFocalInstances.java
java -cp "tools;$classpath" CreateFocalInstances test-data/graphics.swf test-data/focal-instances
~~~

The shared-instance test checks the green channel to isolate matrix/focus
interpolation from the supplied runtime's separate morph palette cache issue.
