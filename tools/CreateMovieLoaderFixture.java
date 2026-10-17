import com.jpexs.decompiler.flash.SWF;
import com.jpexs.decompiler.flash.abc.ABC;
import com.jpexs.decompiler.flash.abc.avm2.parser.script.AbcIndexing;
import com.jpexs.decompiler.flash.abc.avm2.parser.script.ActionScript3Parser;
import com.jpexs.decompiler.flash.iggy.IggyFile;
import com.jpexs.decompiler.flash.iggy.conversion.SwfToIggyConvertor;
import com.jpexs.decompiler.flash.tags.*;
import com.jpexs.decompiler.flash.types.*;
import com.jpexs.decompiler.flash.types.shaperecords.*;
import com.jpexs.helpers.ByteArrayRange;
import java.io.*;
import java.nio.file.*;
import java.util.*;

public class CreateMovieLoaderFixture {
    static void create(String template, String output, boolean child) throws Exception {
        SWF swf;
        try (InputStream input = new FileInputStream(template)) { swf = new SWF(input, false); }
        int position = -1;
        List<Tag> tags = new ArrayList<>(swf.getTags().toArrayList());
        for (Tag tag : tags) {
            if (!(tag instanceof DoABC2Tag)) continue;
            if (position < 0) position = tags.indexOf(tag);
            swf.removeTag(tag);
        }
        DoABC2Tag code = new DoABC2Tag(swf); swf.addTag(position, code);
        ABC abc = code.getABC(); AbcIndexing index = swf.getAbcIndex(); index.selectAbc(abc);
        ActionScript3Parser.compile("package { import flash.media.Sound; public class UnloadTone extends Sound { public function UnloadTone() { super(); } } }", abc, index, "UnloadTone.as", 0, 0, false, null);
        index.refreshAbc(abc);
        DefineSoundTag tone = new DefineSoundTag(swf); tone.soundId = 3; tone.soundFormat = 3;
        tone.soundRate = 2; tone.soundSize = true; tone.soundSampleCount = 8820;
        byte[] samples = new byte[17640];
        for (int i = 0; i < 8820; i++) {
            int sample = (int) Math.round(Math.sin(i * 2 * Math.PI * 440 / 22050) * 200);
            samples[2 * i] = (byte) sample; samples[2 * i + 1] = (byte) (sample >> 8);
        }
        tone.soundData = new ByteArrayRange(samples); swf.addTag(0, tone);
        for (Tag tag : swf.getTags()) if (tag instanceof SymbolClassTag) {
            ((SymbolClassTag) tag).tags.add(3); ((SymbolClassTag) tag).names.add("UnloadTone");
        }
        if (child) {
            ActionScript3Parser.compile("package { import flash.display.MovieClip; public class NestedTimeline extends MovieClip { public function NestedTimeline() { super(); } } }", abc, index, "NestedTimeline.as", abc.instance_info.size(), abc.script_info.size(), false, null);
            index.refreshAbc(abc);
            DefineSpriteTag sprite = new DefineSpriteTag(swf); sprite.spriteId = 2; sprite.frameCount = 3;
            for (int i = 0; i < 3; i++) sprite.addTag(new ShowFrameTag(swf)); sprite.addTag(new EndTag(swf)); swf.addTag(0, sprite);
            for (Tag tag : swf.getTags()) if (tag instanceof SymbolClassTag) {
                ((SymbolClassTag) tag).tags.add(2); ((SymbolClassTag) tag).names.add("NestedTimeline");
            }
            ActionScript3Parser.compile("package { import flash.display.MovieClip; public class SoundTimeline extends MovieClip { public function SoundTimeline() { super(); } } }", abc, index, "SoundTimeline.as", abc.instance_info.size(), abc.script_info.size(), false, null);
            index.refreshAbc(abc);
            DefineSpriteTag soundSprite = new DefineSpriteTag(swf); soundSprite.spriteId = 4; soundSprite.frameCount = 1;
            StartSoundTag start = new StartSoundTag(swf); start.soundId = 3; start.soundInfo = new SOUNDINFO();
            start.soundInfo.hasLoops = true; start.soundInfo.loopCount = 1000; soundSprite.addTag(start);
            soundSprite.addTag(new ShowFrameTag(swf)); soundSprite.addTag(new EndTag(swf)); swf.addTag(0, soundSprite);
            for (Tag tag : swf.getTags()) if (tag instanceof SymbolClassTag) {
                ((SymbolClassTag) tag).tags.add(4); ((SymbolClassTag) tag).names.add("SoundTimeline");
            }
        }
        Path source = Path.of(output, "scripts", "Main.as");
        ActionScript3Parser.compile(Files.readString(source), abc, index, source.toString(), abc.instance_info.size(), abc.script_info.size(), false, null);
        if (child) {
            swf.displayRect = new RECT(0, 4000, 0, 2400); swf.frameCount = 3;
            DefineShape3Tag shape = new DefineShape3Tag(swf); shape.shapeId = 1;
            shape.shapeBounds = new RECT(0, 1500, 0, 1000);
            FILLSTYLE fill = new FILLSTYLE(); fill.fillStyleType = 0; fill.color = new RGBA(0, 0, 255, 255);
            shape.shapes.fillStyles.fillStyles = new FILLSTYLE[]{fill};
            shape.shapes.shapeRecords = new ArrayList<>();
            StyleChangeRecord start = new StyleChangeRecord(); start.stateMoveTo = true; start.stateFillStyle1 = true; start.fillStyle1 = 1;
            shape.shapes.shapeRecords.add(start);
            for (int[] delta : new int[][]{{1500, 0}, {0, 1000}, {-1500, 0}, {0, -1000}}) {
                StraightEdgeRecord edge = new StraightEdgeRecord(); edge.generalLineFlag = true;
                edge.deltaX = delta[0]; edge.deltaY = delta[1]; edge.calculateBits(); shape.shapes.shapeRecords.add(edge);
            }
            shape.shapes.numFillBits = 1; shape.shapes.shapeRecords.add(new EndShapeRecord());
            SWF library = new SWF(); library.version = swf.version;
            library.displayRect = swf.displayRect; library.frameRate = swf.frameRate; library.frameCount = 1;
            FileAttributesTag attributes = new FileAttributesTag(library); attributes.actionScript3 = true; library.addTag(attributes);
            DoABC2Tag libraryCode = new DoABC2Tag(library); library.addTag(libraryCode);
            ABC libraryAbc = libraryCode.getABC(); AbcIndexing libraryIndex = library.getAbcIndex(); libraryIndex.selectAbc(libraryAbc);
            ActionScript3Parser.compile("package { import flash.display.Shape; public class LoadedSquare extends Shape { public var importedMark:uint = 23; public function LoadedSquare() { super(); } } }", libraryAbc, libraryIndex, "LoadedSquare.as", 0, 0, false, null);
            library.addTag(shape);
            SymbolClassTag exported = new SymbolClassTag(library);
            exported.tags.add(1); exported.names.add("LoadedSquare"); library.addTag(exported);
            library.addTag(new ShowFrameTag(library)); library.addTag(new EndTag(library));
            String libraryOutput = Path.of(output).getParent().resolve("shapes").toString();
            try (OutputStream stream = new FileOutputStream(libraryOutput + ".swf")) { library.saveTo(stream); }
            SwfToIggyConvertor.createIggy(library, IggyFile.Variant.X64, "shapes.swf").saveTo(new File(libraryOutput + ".iggy"));
            ImportAssets2Tag imported = new ImportAssets2Tag(swf); imported.url = "shapes.swf";
            imported.tags.add(1); imported.names.add("LoadedSquare"); swf.addTag(0, imported);
            int show = 0;
            List<Tag> current = swf.getTags().toArrayList();
            while (!(current.get(show) instanceof ShowFrameTag)) show++;
            swf.addTag(show, new PlaceObject3Tag(swf, false, 1, "LoadedSquare", -1, new MATRIX(), null, -1, "importedSquare", -1, null, -1, null, -1, null, null, false));
            int end = swf.getTags().size() - 1;
            swf.addTag(end, new ShowFrameTag(swf)); swf.addTag(end + 1, new ShowFrameTag(swf));
        }
        swf.resetTimeline();
        try (OutputStream stream = new FileOutputStream(output + ".swf")) { swf.saveTo(stream); }
        SwfToIggyConvertor.createIggy(swf, IggyFile.Variant.X64, Path.of(output).getFileName() + ".swf").saveTo(new File(output + ".iggy"));
    }
    public static void main(String[] args) throws Exception {
        create(args[0], args[1] + "/child", true); create(args[0], args[1], false);
        SWF plain;
        try (InputStream input = new FileInputStream(args[1] + "/child.swf")) { plain = new SWF(input, false); }
        for (Tag tag : new ArrayList<>(plain.getTags().toArrayList())) if (tag instanceof DoABC2Tag || tag instanceof SymbolClassTag) plain.removeTag(tag);
        try (OutputStream stream = new FileOutputStream(args[1] + "/plain.swf")) { plain.saveTo(stream); }
        SwfToIggyConvertor.createIggy(plain, IggyFile.Variant.X64, "plain.swf").saveTo(new File(args[1] + "/plain.iggy"));
        byte[] x86 = Files.readAllBytes(Path.of(args[1], "plain.iggy")); x86[9] = 32;
        Files.write(Path.of(args[1], "unsupported32.iggy"), x86);
        byte[] invalid = new byte[32]; invalid[0] = 0x49; invalid[1] = 0x67; invalid[2] = 0x0a; invalid[3] = (byte)0xed; invalid[8] = 1; invalid[9] = 64;
        Files.write(Path.of(args[1], "invalid.iggy"), invalid);
        SWF timers = new SWF(); timers.version = 15; timers.frameRate = 24; timers.frameCount = 1; timers.displayRect = new RECT(0, 4000, 0, 2400);
        FileAttributesTag attributes = new FileAttributesTag(timers); attributes.actionScript3 = true; timers.addTag(attributes);
        DoABC2Tag code = new DoABC2Tag(timers); timers.addTag(code); ABC abc = code.getABC(); AbcIndexing index = timers.getAbcIndex(); index.selectAbc(abc);
        String source = "package { import flash.display.MovieClip; import flash.utils.setInterval; import flash.utils.setTimeout; import flash.utils.clearInterval; import flash.utils.clearTimeout; public class Main extends MovieClip { public var ticks:uint; public var done:uint; public var cancelled:uint; public var argsOk:Boolean; private var id:uint; public function Main() { super(); stop(); id = setInterval(tick, 5, 7, 'ok'); var cancel:uint = setTimeout(function():void { cancelled++; }, 10); clearTimeout(cancel); setTimeout(function():void { done++; }, 30); } private function tick(n:uint, s:String):void { ticks++; argsOk = n == 7 && s == 'ok'; if (ticks == 3) clearInterval(id); } } }";
        ActionScript3Parser.compile(source, abc, index, "Main.as", 0, 0, false, null);
        SymbolClassTag symbols = new SymbolClassTag(timers); symbols.tags.add(0); symbols.names.add("Main"); timers.addTag(symbols);
        timers.addTag(new ShowFrameTag(timers)); timers.addTag(new EndTag(timers));
        String timerOutput = Path.of(args[1], "timer-functions").toString();
        try (OutputStream stream = new FileOutputStream(timerOutput + ".swf")) { timers.saveTo(stream); }
        SwfToIggyConvertor.createIggy(timers, IggyFile.Variant.X64, "timer-functions.swf").saveTo(new File(timerOutput + ".iggy"));
    }
}
