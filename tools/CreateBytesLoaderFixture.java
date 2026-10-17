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

public class CreateBytesLoaderFixture {
    static SWF movie() {
        SWF swf = new SWF(); swf.version = 15; swf.frameCount = 1; swf.frameRate = 25;
        swf.displayRect = new RECT(0, 11000, 0, 8000);
        FileAttributesTag attrs = new FileAttributesTag(swf); attrs.actionScript3 = true; swf.addTag(attrs);
        swf.addTag(new DoABC2Tag(swf)); return swf;
    }
    static ABC abc(SWF swf) { for (Tag tag : swf.getTags()) if (tag instanceof DoABC2Tag) return ((DoABC2Tag) tag).getABC(); throw new IllegalStateException(); }
    static void compile(SWF swf, ABC dependency, String source) throws Exception {
        ABC abc = abc(swf); AbcIndexing index = swf.getAbcIndex(); if (dependency != null) index.addAbc(dependency); index.selectAbc(abc);
        ActionScript3Parser.compile(source, abc, index, "BytesFixture.as", abc.instance_info.size(), abc.script_info.size(), false, null);
    }
    static void write(SWF swf, Path output, SymbolClassTag symbols) throws Exception {
        swf.addTag(symbols); swf.addTag(new ShowFrameTag(swf)); swf.addTag(new EndTag(swf));
        try (OutputStream stream = Files.newOutputStream(Path.of(output + ".swf"))) { swf.saveTo(stream); }
        SwfToIggyConvertor.createIggy(swf, IggyFile.Variant.X64, output.getFileName() + ".swf").saveTo(new File(output + ".iggy"));
    }
    public static void main(String[] args) throws Exception {
        Path output = Path.of(args[0]), directory = output.resolveSibling("bytes-loader"); Files.createDirectories(directory);
        SWF library; try (InputStream input = Files.newInputStream(output.resolveSibling("domains/link1/library.swf"))) { library = new SWF(input, false); }
        SWF child = movie(); compile(child, abc(library), Files.readString(directory.resolve("scripts/BytesChild.as")));
        ImportAssets2Tag imports = new ImportAssets2Tag(child); imports.url = "domains/link1/library.swf"; child.addTag(imports);
        child.addTag(new PlaceObject3Tag(child, false, 1, "linklib.ImportedTile", -1, new MATRIX(), null, -1, "tile", -1, null, -1, null, -1, null, null, false));
        SymbolClassTag childSymbols = new SymbolClassTag(child); childSymbols.tags.add(0); childSymbols.names.add("BytesChild");
        write(child, directory.resolve("child"), childSymbols);
        SWF main = movie(); SymbolClassTag symbols = new SymbolClassTag(main); symbols.tags.add(0); symbols.names.add("BytesMain");
        String[] names = {"PngBytes", "JpegBytes", "MovieBytes", "BadBytes", "SwfBytes", "LegacyBytes"};
        byte[][] data = {Files.readAllBytes(output.resolveSibling("loader/colors.png")), Files.readAllBytes(output.resolveSibling("loader/photo.jpg")),
            Files.readAllBytes(directory.resolve("child.iggy")), new byte[]{1, 2, 3, 4}, Files.readAllBytes(directory.resolve("child.swf")), Files.readAllBytes(directory.resolve("child.iggy"))};
        data[5][9] = 32;
        for (int i = 0; i < names.length; i++) {
            compile(main, null, "package { import flash.utils.ByteArray; public class " + names[i] + " extends ByteArray { public function " + names[i] + "() { super(); } } }");
            DefineBinaryDataTag binary = new DefineBinaryDataTag(main); binary.tag = 10 + i; binary.binaryData = new ByteArrayRange(data[i]); main.addTag(binary);
            symbols.tags.add(binary.tag); symbols.names.add(names[i]);
        }
        compile(main, null, Files.readString(directory.resolve("scripts/BytesMain.as"))); write(main, output, symbols);
    }
}
