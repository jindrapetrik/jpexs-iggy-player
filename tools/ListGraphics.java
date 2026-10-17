import com.jpexs.decompiler.flash.*;
import com.jpexs.decompiler.flash.tags.*;
import com.jpexs.decompiler.flash.tags.base.*;
import java.io.*;
public class ListGraphics {
 public static void main(String[] args)throws Exception {
  File toolsDirectory=new File(ListGraphics.class.getProtectionDomain().getCodeSource().getLocation().toURI());
  File movie=args.length>0?new File(args[0]):new File(toolsDirectory.getParentFile(),"test-data/graphics.swf");
  SWF swf;try(InputStream in=new FileInputStream(movie)){swf=new SWF(in,false,"UTF-8");}
  int frame=1;System.out.println("Frames="+swf.frameCount);
  for(Tag tag:swf.getTags()) {
   if(tag instanceof FrameLabelTag)System.out.println(frame+" "+((FrameLabelTag)tag).name);
   if(tag instanceof PlaceObjectTypeTag)System.out.println(frame+" place="+((PlaceObjectTypeTag)tag).getCharacterId());
   if(tag instanceof ShowFrameTag)frame++;
  }
 }
}
