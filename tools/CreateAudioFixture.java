import com.jpexs.decompiler.flash.SWF;
import com.jpexs.decompiler.flash.abc.ABC;
import com.jpexs.decompiler.flash.abc.avm2.parser.script.AbcIndexing;
import com.jpexs.decompiler.flash.abc.avm2.parser.script.ActionScript3Parser;
import com.jpexs.decompiler.flash.iggy.IggyFile;
import com.jpexs.decompiler.flash.iggy.conversion.SwfToIggyConvertor;
import com.jpexs.decompiler.flash.tags.*;
import com.jpexs.decompiler.flash.types.*;
import com.jpexs.helpers.ByteArrayRange;
import java.io.*;
import java.nio.file.*;

// Optional regeneration; normal playback and tests need neither Java nor FFDec.
public class CreateAudioFixture {
    static byte[] pcm(int rate, int bits, int frequency) throws IOException {
        ByteArrayOutputStream bytes = new ByteArrayOutputStream();
        for (int i = 0; i < rate * 2 / 5; i++) {
            double value = Math.sin(2 * Math.PI * frequency * i / rate) * .02;
            if (bits == 8) bytes.write((int) Math.round(128 + 127 * value));
            else { int sample = (int) Math.round(32767 * value); bytes.write(sample & 255); bytes.write(sample >> 8 & 255); }
        }
        return bytes.toByteArray();
    }
    static void sound(SWF swf, int id, int format, int rate, int bits, boolean stereo, byte[] data) {
        DefineSoundTag sound = new DefineSoundTag(swf);
        sound.soundId = id; sound.soundFormat = format; sound.soundRate = rate;
        sound.soundSize = bits == 16; sound.soundType = stereo;
        sound.soundSampleCount = (rate == 1 ? 11025 : rate == 2 ? 22050 : 44100) * 2 / 5;
        sound.soundData = new ByteArrayRange(data); swf.addTag(sound);
    }
    static byte[] mp3(Path path) throws IOException {
        byte[] input = Files.readAllBytes(path), data = new byte[input.length + 2];
        // LAME encoder/decoder latency, as in a DefineSound MP3 payload.
        data[0] = (byte) 1105; data[1] = 1105 >> 8;
        System.arraycopy(input, 0, data, 2, input.length); return data;
    }
    public static void main(String[] args) throws Exception {
        Path directory = Path.of(args[0]);
        SWF swf = new SWF(); swf.version = 15; swf.frameCount = 1; swf.frameRate = 24;
        swf.displayRect = new RECT(0, 10000, 0, 4000);
        FileAttributesTag attributes = new FileAttributesTag(swf); attributes.actionScript3 = true; swf.addTag(attributes);
        sound(swf, 1, 3, 2, 16, false, pcm(22050, 16, 440));
        sound(swf, 2, 3, 1, 8, false, pcm(11025, 8, 330));
        sound(swf, 3, 1, 2, 16, false, Files.readAllBytes(directory.resolve("tone-adpcm.bin")));
        sound(swf, 4, 2, 3, 16, true, mp3(directory.resolve("tone-stereo.mp3")));
        sound(swf, 5, 2, 2, 16, false, mp3(directory.resolve("tone-mono.mp3")));
        sound(swf, 6, 0, 3, 16, false, pcm(44100, 16, 880));
        DoABC2Tag tag = new DoABC2Tag(swf); swf.addTag(tag); ABC abc = tag.getABC();
        AbcIndexing index = swf.getAbcIndex(); index.selectAbc(abc);
        String[] names = {"Pcm16Sound", "Pcm8Sound", "AdpcmSound", "StereoMp3Sound", "MonoMp3Sound", "NativePcmSound"};
        SymbolClassTag symbols = new SymbolClassTag(swf);
        for (int i = 0; i < names.length; i++) {
            String source = "package { import flash.media.Sound; public class " + names[i] + " extends Sound { public function " + names[i] + "() { super(); } } }";
            ActionScript3Parser.compile(source, abc, index, names[i] + ".as", abc.instance_info.size(), abc.script_info.size(), false, null);
            index.refreshAbc(abc); symbols.tags.add(i + 1); symbols.names.add(names[i]);
        }
        ActionScript3Parser.compile(Files.readString(directory.resolve("scripts/Main.as")), abc, index, "Main.as", abc.instance_info.size(), abc.script_info.size(), false, null);
        symbols.tags.add(0); symbols.names.add("Main"); swf.addTag(symbols);
        StartSoundTag start = new StartSoundTag(swf); start.soundId = 1; start.soundInfo = new SOUNDINFO(); swf.addTag(start);
        swf.addTag(new ShowFrameTag(swf)); swf.addTag(new EndTag(swf));
        try (OutputStream output = new FileOutputStream(args[0] + ".swf")) { swf.saveTo(output); }
        SwfToIggyConvertor.createIggy(swf, IggyFile.Variant.X64, "audio.swf").saveTo(new File(args[0] + ".iggy"));
    }
}
