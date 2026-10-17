# AS3 LoaderInfo fixture

The scripts/Main.as source reads counters in the constructor and ENTER_FRAME.
The unmodified DLL fails on the first access through Stage. The compatible
host must return the original Iggy length, preserve Stage/root LoaderInfo
identity and retain the behavior of other receivers. The test also performs
incremental/full native GC and .NET GC.

Regenerate from the player directory with a compatible FFDec CLI:

~~~powershell
java -jar PATH_TO_FFDEC_CLI_JAR -importScript test-data/as3.swf test-data/loaderinfo.swf test-data/loaderinfo/scripts
java -jar PATH_TO_FFDEC_CLI_JAR -swf2iggy test-data/loaderinfo.swf test-data/loaderinfo.iggy 64
~~~

The test-data/as3.swf template supplies the original Main document class and
stage settings. Normal native regression tests do not require Java.
