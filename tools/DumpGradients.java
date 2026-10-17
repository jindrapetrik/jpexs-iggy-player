import com.jpexs.decompiler.flash.SWF;
import com.jpexs.decompiler.flash.tags.*;
import com.jpexs.decompiler.flash.tags.base.*;
import com.jpexs.decompiler.flash.types.*;
import java.io.*;

public class DumpGradients {
    public static void main(String[] args) throws Exception {
        SWF swf;
        try (InputStream input = new FileInputStream(args[0])) {
            swf = new SWF(input, false, "UTF-8");
        }
        int frame = 1;
        for (Tag tag : swf.getTags()) {
            if (tag instanceof ShapeTag) {
                ShapeTag shape = (ShapeTag)tag;
                for (FILLSTYLE fill : shape.getShapes().fillStyles.fillStyles) {
                    if (fill.fillStyleType == 16 || fill.fillStyleType == 18 || fill.fillStyleType == 19) {
                        System.out.println("Shape " + shape.getCharacterId() + " type=" + fill.fillStyleType
                                + " spread=" + fill.gradient.spreadMode + " matrix=" + fill.gradientMatrix);
                        for (GRADRECORD stop : fill.gradient.gradientRecords) {
                            System.out.println("  " + stop.ratio + " " + stop.color);
                        }
                    }
                }
            }
            if (tag instanceof PlaceObjectTypeTag && frame >= 10 && frame <= 16) {
                PlaceObjectTypeTag place = (PlaceObjectTypeTag)tag;
                System.out.println("Frame " + frame + ": " + tag + ", matrix=" + place.getMatrix());
            }
            if (tag instanceof ShowFrameTag) frame++;
        }
    }
}
