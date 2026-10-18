# Audio fixture

These files are generated test tones, not third-party recordings.
The audio.swf/audio.iggy files one directory above contain six embedded
Sound subclasses and a timeline StartSound. scripts/Main.as defines the
keyboard controls shown on the canvas:

- 1: PCM16, 22050 Hz; 2: PCM8, 11025 Hz; 3: SWF ADPCM, 22050 Hz.
- 4: stereo MP3, 44100 Hz; 5: mono MP3, 22050 Hz.
- 6: format-0 PCM16, 44100 Hz (serialized big endian).
- L: play MP3 twice; O: overlap stereo and mono MP3.
- P: stop the channels; S: SoundMixer.stopAll().
- C: chain three sounds through SOUND_COMPLETE; D: play PCM three times.
- V: fractional start time, dynamic numeric arguments and SoundTransform.
- R: stop the previous channel twice; N: frame-2 timeline stop for PCM16.

Embedded tones last 0.4 seconds, at low amplitude. tone-vbr.mp3 is a
one-second standalone decoder regression for variable bitrate and positive/
negative seek prefixes. The MP3 fixture includes a 1105-sample seek
prefix for encoder/decoder delay. The ADPCM payload repacks the independent
packets of an FFmpeg SWF ADPCM WAV into one continuous DefineSound bitstream:
one code-size field, no packet padding, and exactly 8820 samples. The fixture
builder checks the decoded amplitude, DC offset and pitch across the entire
tone, including block boundaries and the tail. PCM samples are generated in Java.

Regenerate the compressed tone inputs from the player directory with
FFmpeg on PATH (needed only for regeneration):

~~~powershell
.\tools\CreateAudioTones.ps1
~~~

Then rebuild the movie with a compatible FFDec library and JDK:

~~~powershell
$classpath = 'PATH_TO_FFDEC_JAR;PATH_TO_FFDEC_LIB/*'
javac -encoding UTF-8 -cp $classpath tools/CreateAudioFixture.java
java '-Djava.awt.headless=true' -cp "tools;$classpath" CreateAudioFixture test-data/audio
~~~

Run the optional audible regression test with:

~~~powershell
.\IggyPlayer.exe --self-test-audio test-data/audio.iggy test-output/audio
~~~

The test checks the PCM blocks submitted to WaveOut, including amplitude
and pitch, channel state/position, MP3 decoder lifetime and unchanged input.
It verifies one SOUND_COMPLETE per natural completion, event targets/flags,
loop counts, chained listeners, argument conversions, native/managed GC,
stopped-channel slot reuse and cancellation without completion events.
It needs an audio device and the Windows MP3 ACM codec. Default regression
tests and image export do not open an audio output device.

## External MP3 loading

sound-loader.swf/sound-loader.iggy and audio/sound-child exercise Sound.load()
and the Sound(URLRequest, SoundLoaderContext) constructor. The quiet tones above
are reused as local, HTTP and HTTPS mono/stereo/VBR inputs. Tests cover async
OPEN/PROGRESS/COMPLETE and error events, encoded-byte counters, length, POST data,
request headers, redirects, invalid/oversize/truncated payloads, untrusted TLS,
close() from an OPEN listener, stalled sockets and shutdown. A remotely loaded
child starts a delayed relative sound load and is stopped with unloadAndStop().
Native mixer output, volume, looping and SOUND_COMPLETE are checked, including
GC of a Sound while its channel still plays and PCM collection after playback.

The first implementation downloads and decodes the whole MP3 before play() can
return a channel. Earlier calls to play() return null. SoundLoaderContext is
accepted; streaming bufferTime and Flash policy files are not emulated in this
desktop player. ID3 metadata and the ID3 event are not implemented yet.
A Sound accepts one load, following the
[AS3 Sound API](https://airsdk.dev/reference/actionscript/3.0/flash/media/Sound.html).
Encoded downloads are limited to 32 MiB, decoded PCM to 64 MiB per sound, and
pending/loaded PCM storage to 128 MiB. No audio files are changed or cached on disk.

Regenerate with a compatible FFDec library and JDK:

~~~powershell
$classpath = 'tools;PATH_TO_FFDEC_JAR;PATH_TO_FFDEC_LIB/*'
javac -encoding UTF-8 -cp $classpath tools/CreateBytesLoaderFixture.java tools/CreateSoundLoaderFixture.java
java -cp $classpath CreateSoundLoaderFixture test-data/sound-loader
~~~

Run --self-test-sound-loader test-data/sound-loader.iggy test-output/sound-loader,
or use test.ps1 -TestAudio. The test uses loopback HTTP/HTTPS only and the existing
scoped test certificate exception; production retains system certificate checks.
