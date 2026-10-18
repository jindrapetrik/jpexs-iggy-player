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

## Local Loader movies

movie-loader.swf and movie-loader.iggy load two instances of movie-loader/child.iggy,
one through a .swf URL, and a script-free plain.iggy. Parent and child both define
Main with different static fields; the parent explicitly loads each child in a
domain with the system parent so those definitions remain separate.
The child contains a three-frame timeline,
device text, ENTER_FRAME code and a nested PNG request relative to its own file.
Its named shape is imported from shapes.iggy and has an AS3 LoadedSquare class.

The fixture checks rendering, class separation, imported classes, LoaderInfo,
root/parent identity, events, parent-to-child calls, missing/corrupt/32-bit errors,
cancellation, GC, retained roots after unload, reuse and closing pending work.
unsupported32.iggy is deliberately a 64-bit fixture with an altered architecture
header, and invalid.iggy has an incomplete directory; neither is a valid movie.
The default suite runs these checks in software; -TestOpenGl also uses the GPU.

~~~powershell
$classpath = 'PATH_TO_FFDEC_JAR;PATH_TO_FFDEC_LIB/*'
javac -encoding UTF-8 -cp $classpath tools/CreateMovieLoaderFixture.java
java -cp "tools;$classpath" CreateMovieLoaderFixture test-data/as3.swf test-data/movie-loader
~~~

Regenerate the Loader image fixture first if loader/colors.png is absent.

The same fixture now tests unloadAndStop with constructor and callback-created
timers, setInterval/setTimeout and their clear functions, arguments, bound and
anonymous Stage/frame listeners, nested and detached clips, recursive Loader
cleanup, GC hints and pixel reclamation. A timer callback can unload its own
movie without interrupting the parent or restarting background activity.
The optional -TestAudio suite also checks separate parent/child AS3 channels
and looping timeline voices; unloading one instance keeps the others playing.

## Audio

The audio.swf and audio.iggy fixtures contain generated sine tones in PCM8,
PCM16, SWF ADPCM and mono/stereo MP3. They exercise native timeline sounds,
AS3 Sound.play(), loops, concurrent channels, pause, mute, stop and resource
lifetime. See [audio/README.md](audio/README.md) for regeneration and controls.

## Loader application domains

domains.swf/domains.iggy and the movies under domains/ test LoaderContext with
default child domains, a shared explicit domain, the current domain and a domain
with only the system parent. The same child class has independent static fields
in separate domains and shared fields when its file is reused in one domain.
The fixture checks class lookup with both name separators, missing-definition
ReferenceError, LoaderInfo and parameters in constructors, timer callback domains,
public class names, GC/reopen and allowCodeImport=false for movies versus images.
info-only.iggy verifies the main LoaderInfo getter before any explicit domain
reference. The document constructor keeps its original try/catch behavior after
the in-memory bootstrap, and dynamic property names are not renamed as classes.
Run it with --self-test-domains test-data/domains.iggy test-output/domains.

Regenerate with a compatible FFDec library and its dependencies:

~~~powershell
$classpath = 'PATH_TO_FFDEC_JAR;PATH_TO_FFDEC_LIB/*'
javac -encoding UTF-8 -cp $classpath tools/CreateDomainFixture.java
java -cp "tools;$classpath" CreateDomainFixture test-data/domains
javac -encoding UTF-8 -cp $classpath tools/CreateDomainLinkingFixture.java
java -cp "tools;$classpath" CreateDomainLinkingFixture test-data/domains
~~~

domains/linking.iggy loads nested libraries from link1/ and link2/ using identical
relative URLs. It checks isolated imported static fields, parent-first class
references, first-definition reuse across different files, static initializer and
method domains, cached imports, GC and reopen. Class-name timeline placements
draw blue or red tiles; the shared domain retains the first blue asset binding.
The library dependencies use zero-symbol ImportAssets2 records; the tile is
resolved through its AS3 SymbolClass, rather than an ExportAssets table.
root-imports.iggy also covers imports at startup and delayed registration of user
classes in the main domain rather than the system domain.
Main and nested imported static initializers must observe the main domain before
the document constructor. Counters and mutated static fields verify initialization
occurs once, survives reflection and GC, and starts fresh when the movie reopens.
The same fixtures cover global functions, live variables, constants and namespaces
in main, imported, shared and isolated domains. They check parent precedence,
first-definition reuse, function callback domains and stable reflected references,
and definitions with null, undefined, zero and false values. getDefinition returns
Object, so an undefined value becomes null; hasDefinition must still return true.
A side-effect counter checks that hasDefinition leaves a lazy global uninitialized
and getDefinition initializes it only once, preserving its object through GC.

## Loader byte arrays

bytes-loader.swf/bytes-loader.iggy embed PNG, JPEG and compiled IGGY movies as
DefineBinaryData ByteArray subclasses. The test also constructs writable arrays,
checks read/write copying and embedded copy-on-write, and mutates the input after
loadBytes returns to verify snapshot ownership. It covers domains and static
fields, constructor LoaderInfo and parameters, imported timeline resources,
relative nested image loads, event order, invalid/raw SWF/32-bit input, both code
execution flags, close, unload during INIT, unloadAndStop, GC and repeated opens.
The same test runs with software and OpenGL rendering.

Regenerate the loader image and domains fixtures first, then run:

~~~powershell
$classpath = 'PATH_TO_FFDEC_JAR;PATH_TO_FFDEC_LIB/*'
javac -encoding UTF-8 -cp $classpath tools/CreateBytesLoaderFixture.java
java -cp "tools;$classpath" CreateBytesLoaderFixture test-data/bytes-loader
~~~

The files under bytes-loader/scripts are the fixture's AS3 sources. The child
container is embedded in the parent; no memory-load temporary files are written.

## ByteArray binary data

bytearray.swf/bytearray.iggy test numeric data against Java ByteBuffer fixtures
in both byte orders, including IEEE negative zero, subnormals, infinities and NaN.
They cover UTF-8 byte-length prefixes, BOMs/NULs, strings over 65535 encoded bytes,
Windows-1250 and Shift-JIS, typed EOF and argument errors, moving GC and reopen.
Java Deflater fixtures cover dynamic, fixed and stored DEFLATE blocks and zlib;
the test also decompresses an embedded PNG and loads it through Loader.loadBytes.
Malformed blocks, truncation, checksums and bounded output are verified. The
bytearray/only movie checks activation without any Loader references.

Regenerate after the loader image fixture, using the existing helper:

~~~powershell
$classpath = 'tools;PATH_TO_FFDEC_JAR;PATH_TO_FFDEC_LIB/*'
javac -encoding UTF-8 -cp $classpath tools/CreateBytesLoaderFixture.java tools/CreateByteArrayFixture.java
java -cp $classpath CreateByteArrayFixture test-data/bytearray
~~~

The main AS3 source is bytearray/ByteArrayMain.as. Run --self-test-bytearray
test-data/bytearray.iggy test-output/bytearray, or use test.ps1 for regression.

## ByteArray objects

amf.swf/amf.iggy exercise AMF0 and AMF3 serialization with exact wire vectors,
integer boundaries, special doubles and UTF-8 (including NUL and supplementary
characters). They cover cyclic/shared references, associative arrays, dates,
nested ByteArrays, typed classes, inherited fields, accessors, [Transient],
alias remapping and fallback for unknown aliases. Malformed input, resource
limits, atomic failed writes, encoding defaults, moving GC and reopen are checked.
The extended cases cover IExternalizable private state, shared enclosing reference
tables and cycles, mixed endian primitives, callback errors and expired streams.
ByteArray is checked through IDataInput/IDataOutput. Vector int/uint/Number/object
wire values, fixed lengths, wildcard/custom/string/nested element types and
Dictionary object keys, cycles and weak-key flags are included. XML/XMLDocument
cases cover UTF-8, namespaces, comments, references, malformed input and DTD
rejection. Retained values are checked again after moving garbage collection.
The amf/only movie verifies activation without ByteArray or Loader references.
Two loaded child movies import a typed class and verify alias isolation between
inherited and isolated application domains. The sources are under amf/.

Regenerate with the existing fixture helper and a compatible FFDec library:

~~~powershell
$classpath = 'tools;PATH_TO_FFDEC_JAR;PATH_TO_FFDEC_LIB/*'
javac -encoding UTF-8 -cp $classpath tools/CreateBytesLoaderFixture.java tools/CreateAmfFixture.java
java -cp $classpath CreateAmfFixture test-data/amf
~~~

Run --self-test-amf test-data/amf.iggy test-output/amf, or use test.ps1.
The regression suite also runs it with OpenGL when -TestOpenGl is selected.

## SharedObject AMF storage

sharedobject.swf/sharedobject.iggy exercise AMF0/AMF3 local persistence:
cycles and shared references, Unicode including NUL and supplementary characters,
typed aliases, IExternalizable callbacks, ByteArrays, dates, vectors, dictionaries
and associative arrays. The host test seeds legacy IGGY1 files, verifies migration
on flush, rejects malformed saves without replacing them, retries a repaired save,
and checks failed serialization, clear() reference identity and 2 MiB binary data.
It reopens movies across native VM shutdown, forces moving GC, checks movie-path
isolation and releases byte storage. sharedobject/only.iggy references SharedObject
without Loader or ByteArray to test automatic activation of the AMF runtime.

sharedobject-auto.swf/sharedobject-auto.iggy and sharedobject/auto-child exercise
automatic saving on close, replacement (including a failed next open), disposal
and native VM shutdown. They check nested and binary mutations without flush,
AMF0, cyclic typed/externalizable graphs, reentrant getLocal from writeExternal,
isolated/stopped loaded movies, unchanged legacy files, clear through held data
references, temporary files and continuation after serialization or disk errors.
Run --self-test-sharedobject-autosave with test-data/sharedobject-auto.iggy and
test-output/sharedobject-autosave as its two arguments, or use test.ps1.

The AS3 sources are in sharedobject/. Regenerate with a compatible FFDec library:

~~~powershell
$classpath = 'tools;PATH_TO_FFDEC_JAR;PATH_TO_FFDEC_LIB/*'
javac -encoding UTF-8 -cp $classpath tools/CreateBytesLoaderFixture.java tools/CreateSharedObjectFixture.java
java -cp $classpath CreateSharedObjectFixture test-data/sharedobject
~~~

Run --self-test-sharedobject test-data/sharedobject.iggy test-output/sharedobject,
or use test.ps1, which also selects OpenGL when -TestOpenGl is supplied.

## URLLoader local data

url-loader.swf/url-loader.iggy cover text/binary loads, UTF-8 and UTF-16 BOMs,
NUL and supplementary Unicode, escaped paths, empty and 2 MiB files, exact .swf
reads, constructor loading, counters and event clones/order. Tests exercise close,
replacement and format changes inside progress callbacks, missing/oversize files,
network security errors, forced GC before completion, resource collection and
immediate shutdown. url-data/sub
checks relative paths in a loaded movie's delayed callback and unloadAndStop.
url-data/only tests activation without display Loader or ByteArray references.
The .swf/.iggy files named raw are data inputs, not movies to open in the player.

Regenerate with a compatible FFDec library and its dependencies:

~~~powershell
$classpath = 'tools;PATH_TO_FFDEC_JAR;PATH_TO_FFDEC_LIB/*'
javac -encoding UTF-8 -cp $classpath tools/CreateBytesLoaderFixture.java tools/CreateDataLoaderFixture.java
java -cp $classpath CreateDataLoaderFixture test-data/url-loader
~~~

Run --self-test-url-loader test-data/url-loader.iggy test-output/url-loader,
or use test.ps1 with optional -TestOpenGl. Oversized test files are generated in
the output directory rather than stored with the fixtures.
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
