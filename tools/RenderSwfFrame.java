import com.jpexs.decompiler.flash.SWF;
import com.jpexs.decompiler.flash.exporters.commonshape.Matrix;
import java.awt.Color;
import java.io.*;
import javax.imageio.ImageIO;
public class RenderSwfFrame {
    public static void main(String[] args) throws Exception {
        SWF swf;
        try (InputStream input = new FileInputStream(args[0])) {
            swf = new SWF(input, false, "UTF-8");
        }
        ImageIO.write(SWF.frameToImageGet(swf.getTimeline(), Integer.parseInt(args[2]) - 1,
                0, null, 0, swf.displayRect, new Matrix(), null, Color.WHITE, 1,
                true, 1).getBufferedImage(), "png", new File(args[1]));
    }
}
