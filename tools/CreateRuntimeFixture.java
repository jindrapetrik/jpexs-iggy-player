import com.jpexs.decompiler.flash.SWF;
import com.jpexs.decompiler.flash.abc.ABC;
import com.jpexs.decompiler.flash.abc.avm2.parser.script.AbcIndexing;
import com.jpexs.decompiler.flash.abc.avm2.parser.script.ActionScript3Parser;
import com.jpexs.decompiler.flash.iggy.IggyFile;
import com.jpexs.decompiler.flash.iggy.conversion.SwfToIggyConvertor;
import com.jpexs.decompiler.flash.tags.DoABC2Tag;
import com.jpexs.decompiler.flash.tags.Tag;
import java.io.*;
import java.nio.file.*;
import java.util.*;

// Compile the fixture's subclass before its document class.
public class CreateRuntimeFixture {
    public static void main(String[] args) throws Exception {
        SWF swf;
        try (InputStream input = new FileInputStream(args[0])) { swf = new SWF(input, false); }
        List<Tag> original = new ArrayList<>(swf.getTags().toArrayList());
        int position = -1;
        for (Tag tag : original) {
            if (!(tag instanceof DoABC2Tag)) continue;
            if (position < 0) position = original.indexOf(tag);
            swf.removeTag(tag);
        }
        DoABC2Tag tag = new DoABC2Tag(swf);
        swf.addTag(position < 0 ? 0 : position, tag);
        ABC abc = tag.getABC();
        AbcIndexing index = swf.getAbcIndex();
        index.selectAbc(abc);
        for (String name : new String[]{"FilePicker", "Main"}) {
            Path path = Path.of(args[1], "scripts", name + ".as");
            ActionScript3Parser.compile(Files.readString(path), abc, index, path.toString(),
                    abc.instance_info.size(), abc.script_info.size(), false, null);
            index.refreshAbc(abc);
        }
        try (OutputStream output = new FileOutputStream(args[1] + ".swf")) { swf.saveTo(output); }
        SwfToIggyConvertor.createIggy(swf, IggyFile.Variant.X64, "runtime.swf")
                .saveTo(new File(args[1] + ".iggy"));
    }
}
