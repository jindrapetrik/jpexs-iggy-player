# Runtime API fixture

The scripts/Main.as source tests SoundChannel, persistent SharedObject data
and asynchronous Loader errors. The test uses a separate storage directory
under test-output and checks reopening, a new IggyInit/IggyShutdown cycle
and another movie path. Its final run deliberately simulates a storage
write failure and catches it in AS3. Non-ASCII literals are intentional
Unicode test data.

Regenerate from the player directory:

~~~powershell
java -jar PATH_TO_FFDEC_CLI_JAR -importScript test-data/as3.swf test-data/runtime.swf test-data/runtime/scripts
java -jar PATH_TO_FFDEC_CLI_JAR -swf2iggy test-data/runtime.swf test-data/runtime.iggy 64
~~~

Run the native test with --self-test-runtime or as part of test.ps1. Normal
testing does not require Java.
