import com.jpexs.decompiler.flash.SWF;
import com.jpexs.decompiler.flash.abc.ABC;
import com.jpexs.decompiler.flash.abc.avm2.parser.script.AbcIndexing;
import com.jpexs.decompiler.flash.abc.avm2.parser.script.ActionScript3Parser;
import com.jpexs.decompiler.flash.iggy.IggyFile;
import com.jpexs.decompiler.flash.iggy.conversion.SwfToIggyConvertor;
import com.jpexs.decompiler.flash.tags.*;
import com.jpexs.decompiler.flash.types.*;
import com.jpexs.decompiler.flash.types.shaperecords.*;
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
        Path source = Path.of(output, "scripts", "Main.as");
        ActionScript3Parser.compile(Files.readString(source), abc, index, source.toString(), 0, 0, false, null);
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
    }
}
