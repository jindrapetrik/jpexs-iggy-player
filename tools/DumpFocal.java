import com.jpexs.decompiler.flash.*;
import com.jpexs.decompiler.flash.tags.*;
import com.jpexs.decompiler.flash.tags.base.*;
import com.jpexs.decompiler.flash.types.*;
import java.io.*;
public class DumpFocal {
 public static void main(String[] args)throws Exception {
  SWF swf;try(InputStream in=new FileInputStream(args[0])){swf=new SWF(in,false,"UTF-8");}
  int frame=1;
  for(Tag t:swf.getTags()){
   if(t instanceof MorphShapeTag){MorphShapeTag m=(MorphShapeTag)t;
    for(MORPHFILLSTYLE f:m.morphFillStyles.fillStyles)if(f.fillStyleType==19){
     System.out.println("MORPH "+m.characterId+" bounds="+m.startBounds+" endBounds="+m.endBounds+" start="+f.startGradientMatrix+" end="+f.endGradientMatrix+" gradient="+f.gradient);
     for(MORPHGRADRECORD r:f.gradient.gradientRecords)System.out.println("Stop "+r.startRatio+" "+r.startColor+" -> "+r.endRatio+" "+r.endColor);
     if(f.gradient instanceof MORPHFOCALGRADIENT){MORPHFOCALGRADIENT g=(MORPHFOCALGRADIENT)f.gradient;System.out.println("Foci "+g.startFocalPoint+" -> "+g.endFocalPoint);}
    }
   }
   if(t instanceof PlaceObjectTypeTag && frame>=62&&frame<=65){PlaceObjectTypeTag p=(PlaceObjectTypeTag)t;System.out.println("Frame "+frame+" place="+p.getCharacterId()+" ratio="+p.getRatio()+" matrix="+p.getMatrix());}
   if(t instanceof ShowFrameTag)frame++;
  }
 }
}
