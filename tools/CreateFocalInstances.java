import com.jpexs.decompiler.flash.SWF;
import com.jpexs.decompiler.flash.tags.*;
import com.jpexs.decompiler.flash.tags.base.MorphShapeTag;
import com.jpexs.decompiler.flash.types.*;
import com.jpexs.decompiler.flash.iggy.IggyFile;
import com.jpexs.decompiler.flash.iggy.conversion.SwfToIggyConvertor;
import java.io.*;
import java.util.*;

// Two simultaneous instances of graphics.swf morph 174, with distinct ratios.
// Optional regeneration tool; the native-player test itself needs no Java.
public class CreateFocalInstances {
    public static void main(String[] args) throws Exception {
        SWF swf;
        try (InputStream input = new FileInputStream(args[0])) {
            swf = new SWF(input, false, "UTF-8");
        }
        List<Tag> remove = new ArrayList<>();
        boolean found = false;
        for (Tag tag : swf.getTags()) {
            if (tag instanceof MorphShapeTag && ((MorphShapeTag)tag).characterId == 174) {
                found = true;
            } else {
                remove.add(tag);
            }
        }
        if (!found) throw new IllegalArgumentException("Expected morph 174");
        for (Tag tag : remove) swf.removeTag(tag);
        swf.displayRect = new RECT(0, 14000, 0, 6000);
        swf.frameCount = 2;
        swf.frameRate = 24;
        int[][] ratios = {{0, 65535}, {21845, 43691}};
        for (int frame = 0; frame < ratios.length; frame++) {
            for (int instance = 0; instance < 2; instance++) {
                MATRIX matrix = new MATRIX();
                matrix.translateX = -5000 + instance * 6000;
                swf.addTag(new PlaceObject2Tag(swf, frame > 0, instance + 1,
                        frame == 0 ? 174 : -1, matrix, null,
                        ratios[frame][instance], null, -1, null));
            }
            swf.addTag(new ShowFrameTag(swf));
        }
        swf.addTag(new EndTag(swf));
        swf.resetTimeline();
        try (OutputStream output = new FileOutputStream(args[1] + ".swf")) {
            swf.saveTo(output);
        }
        SwfToIggyConvertor.createIggy(swf, IggyFile.Variant.X64, "focal-instances.swf")
                .saveTo(new File(args[1] + ".iggy"));
    }
}
