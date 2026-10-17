import com.jpexs.decompiler.flash.SWF;
import com.jpexs.decompiler.flash.tags.*;
import com.jpexs.decompiler.flash.tags.base.PlaceObjectTypeTag;
import java.io.*;
public class DumpEarlyFrames {
    public static void main(String[] args) throws Exception {
        SWF swf;try(InputStream in=new FileInputStream(args[0])){swf=new SWF(in,false,"UTF-8");}
        int frame=1;
        for(Tag tag:swf.getTags()) {
            if(frame>6)break;
            if(tag instanceof PlaceObjectTypeTag){PlaceObjectTypeTag p=(PlaceObjectTypeTag)tag;System.out.println("Frame "+frame+": "+tag+", depth="+p.getDepth()+", move="+p.flagMove()+", matrix="+p.getMatrix());}
            if(tag instanceof ShowFrameTag)frame++;
        }
    }
}
