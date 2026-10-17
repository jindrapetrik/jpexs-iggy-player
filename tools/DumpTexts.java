import com.jpexs.decompiler.flash.*;
import com.jpexs.decompiler.flash.iggy.*;
import com.jpexs.decompiler.flash.iggy.conversion.*;
import com.jpexs.decompiler.flash.tags.*;
import com.jpexs.decompiler.flash.tags.base.*;
import java.io.*;
public class DumpTexts {
 public static void main(String[] args)throws Exception {
  SWF swf;if(args[0].endsWith(".iggy"))swf=IggyToSwfConvertor.getSwf(new IggyFile(args[0]));else try(InputStream in=new FileInputStream(args[0])){swf=new SWF(in,false,"UTF-8");}
  for(Tag t:swf.getTags())if(t instanceof DefineEditTextTag){DefineEditTextTag e=(DefineEditTextTag)t;
   if(e.characterID<176)continue;
   System.out.println("Text "+e.characterID+" hasText="+e.hasText+" html="+e.html+" hasFont="+e.hasFont+" font="+e.fontId+" fontClass="+e.fontClass+" outlines="+e.useOutlines+" height="+e.fontHeight+" color="+e.textColor+" flags readOnly="+e.readOnly+" multiline="+e.multiline+" wrap="+e.wordWrap);
   System.out.println("Content="+e.initialText);
  }
 }
}
