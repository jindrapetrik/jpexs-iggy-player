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

public class CreateDomainLinkingFixture {
    static SWF movie() {
        SWF swf = new SWF(); swf.version = 15; swf.frameCount = 1; swf.frameRate = 25;
        swf.displayRect = new RECT(0, 11000, 0, 8000);
        FileAttributesTag attrs = new FileAttributesTag(swf); attrs.actionScript3 = true; swf.addTag(attrs);
        swf.addTag(new DoABC2Tag(swf)); return swf;
    }
    static ABC abc(SWF swf) { for (Tag tag : swf.getTags()) if (tag instanceof DoABC2Tag) return ((DoABC2Tag) tag).getABC(); throw new IllegalStateException(); }
    static void compile(SWF swf, ABC dependency, String... sources) throws Exception {
        ABC abc = abc(swf); AbcIndexing index = swf.getAbcIndex(); if (dependency != null) index.addAbc(dependency); index.selectAbc(abc);
        for (String source : sources) { ActionScript3Parser.compile(source, abc, index, "Fixture.as", abc.instance_info.size(), abc.script_info.size(), false, null); index.refreshAbc(abc); }
    }
    static void root(SWF swf, String name) { SymbolClassTag symbols = new SymbolClassTag(swf); symbols.tags.add(0); symbols.names.add(name); swf.addTag(symbols); }
    static void write(SWF swf, Path file) throws Exception {
        swf.addTag(new ShowFrameTag(swf)); swf.addTag(new EndTag(swf));
        try (OutputStream stream = Files.newOutputStream(Path.of(file + ".swf"))) { swf.saveTo(stream); }
        SwfToIggyConvertor.createIggy(swf, IggyFile.Variant.X64, file.getFileName() + ".swf").saveTo(new File(file + ".iggy"));
    }
    static void imports(SWF swf, String url) {
        ImportAssets2Tag tag = new ImportAssets2Tag(swf); tag.url = url;
        swf.addTag(tag);
    }
    public static void main(String[] args) throws Exception {
        Path directory = Path.of(args[0]); ABC primaryLibrary = null;
        for (int variant = 1; variant <= 2; variant++) {
            Path dir = directory.resolve("link" + variant); Files.createDirectories(dir);
            SWF nested = movie(); compile(nested, null, "package linklib { public class NestedState { public static var count:uint = " + (40 + variant) + "; public function NestedState() { } } }");
            write(nested, dir.resolve("nested"));
            SWF library = movie(); compile(library, null,
                    "package linklib { import flash.system.ApplicationDomain; public class ImportedState { public static var count:uint = " + (variant == 1 ? 17 : 29) + "; public static var owner:ApplicationDomain = ApplicationDomain.currentDomain; public function ImportedState() { } public static function current():ApplicationDomain { return ApplicationDomain.currentDomain; } } }",
                    "package linklib { import flash.display.Shape; public class ImportedTile extends Shape { public var mark:uint = " + variant + "; public function ImportedTile() { super(); } } }");
            if (variant == 1) primaryLibrary = abc(library);
            DefineShape3Tag shape = new DefineShape3Tag(library); shape.shapeId = 1; shape.shapeBounds = new RECT(0, 800, 0, 800);
            FILLSTYLE fill = new FILLSTYLE(); fill.fillStyleType = 0; fill.color = variant == 1 ? new RGBA(0, 0, 255, 255) : new RGBA(255, 0, 0, 255);
            shape.shapes.fillStyles.fillStyles = new FILLSTYLE[]{fill}; shape.shapes.numFillBits = 1;
            StyleChangeRecord start = new StyleChangeRecord(); start.stateMoveTo = true; start.stateFillStyle1 = true; start.fillStyle1 = 1; shape.shapes.shapeRecords.add(start);
            for (int[] delta : new int[][]{{800, 0}, {0, 800}, {-800, 0}, {0, -800}}) { StraightEdgeRecord edge = new StraightEdgeRecord(); edge.generalLineFlag = true; edge.deltaX = delta[0]; edge.deltaY = delta[1]; edge.calculateBits(); shape.shapes.shapeRecords.add(edge); }
            shape.shapes.shapeRecords.add(new EndShapeRecord()); library.addTag(shape);
            SymbolClassTag exported = new SymbolClassTag(library); exported.tags.add(1); exported.names.add("linklib.ImportedTile"); library.addTag(exported);
            imports(library, "nested.swf"); write(library, dir.resolve("library"));
            SWF child = movie(); String childName = "DomainLinkChild" + variant;
            String script = Files.readString(directory.resolve("scripts/DomainLinkChild.as")).replace("DomainLinkChild", childName);
            compile(child, abc(library),
                    "package { public class CollisionValue { public static var marker:uint = " + (200 + variant) + "; public function CollisionValue() { } public function label():uint { return marker; } } }",
                    "package { public class DerivedCollision extends CollisionValue { public function DerivedCollision() { super(); } } }",
                    "package { public class DuplicateValue { public static var marker:uint = " + (10 * variant) + "; public function DuplicateValue() { } } }", script);
            root(child, childName); imports(child, "library.swf");
            child.addTag(new PlaceObject3Tag(child, false, 1, "linklib.ImportedTile", -1, new MATRIX(), null, -1, "tile", -1, null, -1, null, -1, null, null, false));
            write(child, dir.resolve("child"));
            if (variant == 1) Files.copy(dir.resolve("child.iggy"), dir.resolve("child-again.iggy"), StandardCopyOption.REPLACE_EXISTING);
        }
        SWF main = movie(); compile(main, null,
                "package { public class CollisionValue { public static var marker:uint = 101; public function CollisionValue() { } public function label():uint { return marker; } } }",
                Files.readString(directory.resolve("scripts/DomainLinkMain.as")));
        root(main, "DomainLinkMain"); write(main, directory.resolve("linking"));
        SWF importedRoot = movie(); compile(importedRoot, primaryLibrary,
                "package { import flash.display.MovieClip; import flash.system.ApplicationDomain; import linklib.ImportedState; public class RootImportCheck extends MovieClip { public var rootImports:Boolean; public var stateCount:uint; public var nestedCount:uint; public var ownerOk:Boolean; public function RootImportCheck() { super(); stop(); var direct:Class = ImportedState; var domain:ApplicationDomain = loaderInfo.applicationDomain; var state:Class = domain.getDefinition('linklib.ImportedState') as Class; var nested:Class = domain.getDefinition('linklib.NestedState') as Class; stateCount = state['count']; nestedCount = nested['count']; ownerOk = state['owner'] === domain; rootImports = stateCount == 17 && nestedCount == 41 && direct === state && domain.getQualifiedDefinitionNames().indexOf('linklib::ImportedState') >= 0 && !domain.parentDomain.hasDefinition('linklib.ImportedState'); } } }");
        root(importedRoot, "RootImportCheck"); imports(importedRoot, "link1/library.swf");
        write(importedRoot, directory.resolve("root-imports"));
    }
}
