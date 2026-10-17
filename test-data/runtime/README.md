# Runtime API fixture

The scripts/Main.as source tests SoundChannel, persistent SharedObject data
and asynchronous Loader errors. A FileReferenceList subclass checks
inheritance, asynchronous CANCEL events, event targets, an empty file list
and repeated browse requests without a false SELECT event.
The test uses a separate storage directory
under test-output and checks reopening, a new IggyInit/IggyShutdown cycle
and another movie path. Its final run deliberately simulates a storage
write failure and catches it in AS3. Non-ASCII literals are intentional
Unicode test data.

Regenerate from the player directory:

~~~powershell
$classpath = 'PATH_TO_FFDEC_JAR;PATH_TO_FFDEC_LIB/*'
javac -encoding UTF-8 -cp $classpath tools/CreateRuntimeFixture.java
java -cp "tools;$classpath" CreateRuntimeFixture test-data/as3.swf test-data/runtime
~~~

Run the native test with --self-test-runtime or as part of test.ps1. Normal
testing does not require Java.

The generator compiles FilePicker before Main so that the subclass is
available while compiling the document class. Runtime shims remain separate
and are injected only into the player's in-memory copy.
