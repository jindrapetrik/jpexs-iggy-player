import com.jpexs.decompiler.flash.SWF;
import com.jpexs.decompiler.flash.tags.*;
import com.jpexs.decompiler.flash.types.*;
import com.jpexs.decompiler.flash.types.filters.*;
import com.jpexs.decompiler.flash.types.shaperecords.*;
import com.jpexs.decompiler.flash.iggy.IggyFile;
import com.jpexs.decompiler.flash.iggy.conversion.SwfToIggyConvertor;
import java.io.*;
import java.util.*;

// Optional fixture regeneration; the native regression test needs no Java.
public class CreateFilterFixture {
    static void rectangle(SWF swf, int id, RGBA color) {
        DefineShape3Tag shape = new DefineShape3Tag(swf);
        shape.shapeId = id;
        shape.shapeBounds = new RECT(0, 1500, 0, 1000);
        FILLSTYLE fill = new FILLSTYLE();
        fill.fillStyleType = 0;
        fill.color = color;
        shape.shapes.fillStyles.fillStyles = new FILLSTYLE[]{fill};
        shape.shapes.shapeRecords = new ArrayList<>();
        StyleChangeRecord style = new StyleChangeRecord();
        style.stateMoveTo = true;
        style.stateFillStyle1 = true;
        style.fillStyle1 = 1;
        shape.shapes.shapeRecords.add(style);
        for (int[] delta : new int[][]{{1500, 0}, {0, 1000}, {-1500, 0}, {0, -1000}}) {
            StraightEdgeRecord edge = new StraightEdgeRecord();
            edge.generalLineFlag = true;
            edge.deltaX = delta[0];
            edge.deltaY = delta[1];
            edge.calculateBits();
            shape.shapes.shapeRecords.add(edge);
        }
        shape.shapes.numFillBits = 1;
        shape.shapes.shapeRecords.add(new EndShapeRecord());
        swf.addTag(shape);
    }
    static void place(SWF swf, int depth, int character, int x, int y, FILTER... filters) {
        MATRIX matrix = new MATRIX();
        matrix.translateX = x * 20;
        matrix.translateY = y * 20;
        swf.addTag(new PlaceObject3Tag(swf, false, depth, null, character, matrix,
                null, -1, null, -1, Arrays.asList(filters), -1, null, -1, null, null, false));
    }
    public static void main(String[] args) throws Exception {
        SWF swf = new SWF();
        swf.version = 15;
        swf.displayRect = new RECT(0, 10000, 0, 8000);
        swf.frameCount = 1;
        swf.frameRate = 24;
        swf.addTag(new SetBackgroundColorTag(swf, new RGB(255, 255, 255)));
        rectangle(swf, 1, new RGBA(0, 255, 0, 255));
        rectangle(swf, 2, new RGBA(64, 128, 192, 128));
        COLORMATRIXFILTER matrix = new COLORMATRIXFILTER();
        matrix.matrix = new float[]{0, 0, 1, 0, 10, 1, 0, 0, 0, 20,
            0, 1, 0, 0, 30, 0, 0, 0, .5f, 40};
        place(swf, 1, 2, 30, 30, matrix);
        COLORMATRIXFILTER chained = new COLORMATRIXFILTER();
        chained.matrix = new float[]{0, 1, 0, 0, 0, 0, 0, 1, 0, 0,
            1, 0, 0, 0, 0, 0, 0, 0, 1, 0};
        place(swf, 2, 2, 150, 30, matrix, chained);
        for (int i = 0; i < 2; i++) {
            BLURFILTER blur = new BLURFILTER();
            blur.blurX = 15;
            blur.blurY = 15;
            blur.passes = i == 0 ? 1 : 3;
            place(swf, 3 + i, 1, 30 + i * 120, 140, blur);
        }
        GRADIENTGLOWFILTER glow = new GRADIENTGLOWFILTER();
        glow.gradientColors = new RGBA[]{new RGBA(0, 0, 255, 128), new RGBA(0, 0, 255, 128)};
        glow.gradientRatio = new int[]{0, 255};
        glow.blurX = 15;
        glow.blurY = 15;
        glow.strength = 1;
        glow.knockout = true;
        glow.compositeSource = true;
        glow.passes = 1;
        place(swf, 5, 1, 30, 250, glow);
        swf.addTag(new ShowFrameTag(swf));
        swf.addTag(new EndTag(swf));
        swf.resetTimeline();
        try (OutputStream output = new FileOutputStream(args[0] + ".swf")) {
            swf.saveTo(output);
        }
        SwfToIggyConvertor.createIggy(swf, IggyFile.Variant.X64, "filters.swf")
                .saveTo(new File(args[0] + ".iggy"));
    }
}
