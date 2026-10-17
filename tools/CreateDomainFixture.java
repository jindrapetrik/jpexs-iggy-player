import com.jpexs.decompiler.flash.SWF;
import com.jpexs.decompiler.flash.abc.ABC;
import com.jpexs.decompiler.flash.abc.avm2.parser.script.AbcIndexing;
import com.jpexs.decompiler.flash.abc.avm2.parser.script.ActionScript3Parser;
import com.jpexs.decompiler.flash.iggy.IggyFile;
import com.jpexs.decompiler.flash.iggy.conversion.SwfToIggyConvertor;
import com.jpexs.decompiler.flash.tags.*;
import com.jpexs.decompiler.flash.types.RECT;
import java.io.*;
import java.nio.file.*;

public class CreateDomainFixture {
    static void create(Path output, String root, String... sources) throws Exception {
        SWF swf = new SWF(); swf.version = 15; swf.frameCount = 1; swf.frameRate = 25;
        swf.displayRect = new RECT(0, 11000, 0, 8000);
        FileAttributesTag attributes = new FileAttributesTag(swf); attributes.actionScript3 = true; swf.addTag(attributes);
        DoABC2Tag tag = new DoABC2Tag(swf); swf.addTag(tag);
        ABC abc = tag.getABC(); AbcIndexing index = swf.getAbcIndex(); index.selectAbc(abc);
        for (String source : sources) {
            ActionScript3Parser.compile(source, abc, index, root + ".as", abc.instance_info.size(), abc.script_info.size(), false, null);
            index.refreshAbc(abc);
        }
        SymbolClassTag symbols = new SymbolClassTag(swf); symbols.tags.add(0); symbols.names.add(root); swf.addTag(symbols);
        swf.addTag(new ShowFrameTag(swf)); swf.addTag(new EndTag(swf));
        try (OutputStream stream = new FileOutputStream(output + ".swf")) { swf.saveTo(stream); }
        SwfToIggyConvertor.createIggy(swf, IggyFile.Variant.X64, output.getFileName() + ".swf").saveTo(new File(output + ".iggy"));
    }
    public static void main(String[] args) throws Exception {
        Path directory = Paths.get(args[0]);
        create(directory.resolve("child"), "DomainChild",
                "package sample { public class DomainValue { public static var count:uint; public function DomainValue() { } } }",
                "package { public class PropertyContainer { public var DomainChild:uint = 7; public function PropertyContainer() { } } }",
                "package { public class MethodContainer { public function MethodContainer() { } public function DomainChild():uint { return 11; } } }",
                Files.readString(directory.resolve("scripts/DomainChild.as")));
        create(directory.resolve("into"), "IntoDomainChild",
                "package { public class IntoDomainValue { public function IntoDomainValue() { } } }",
                "package { import flash.display.MovieClip; import flash.system.ApplicationDomain; public class IntoDomainChild extends MovieClip { public var domain:ApplicationDomain; public function IntoDomainChild() { super(); stop(); domain = ApplicationDomain.currentDomain; } } }");
        create(directory.resolve("info-only"), "DomainInfoOnly",
                "package { import flash.display.MovieClip; public class DomainInfoOnly extends MovieClip { public var infoDomain:Boolean; public function DomainInfoOnly() { super(); stop(); infoDomain = loaderInfo.applicationDomain != null && loaderInfo.applicationDomain.hasDefinition('DomainInfoOnly'); } } }");
        create(directory, "DomainMain",
                "package { public class ParentOnly { public function ParentOnly() { } } }",
                Files.readString(directory.resolve("scripts/DomainMain.as")));
    }
}
