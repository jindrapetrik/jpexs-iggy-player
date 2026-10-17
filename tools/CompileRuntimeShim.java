import com.jpexs.decompiler.flash.SWF;
import com.jpexs.decompiler.flash.abc.ABC;
import com.jpexs.decompiler.flash.abc.avm2.parser.script.AbcIndexing;
import com.jpexs.decompiler.flash.abc.avm2.parser.script.ActionScript3Parser;
import com.jpexs.decompiler.flash.tags.DoABC2Tag;
import java.io.*;
import java.nio.file.*;

public class CompileRuntimeShim {
    public static void main(String[] args) throws Exception {
        SWF swf;
        try(InputStream in=new FileInputStream(args[0])) {swf=new SWF(in,false);}
        DoABC2Tag tag=new DoABC2Tag(swf);
        swf.addTag(tag);
        ABC abc=tag.getABC();
        AbcIndexing index=swf.getAbcIndex();
        index.selectAbc(abc);
        for(int i=2;i<args.length;i++) {
            ActionScript3Parser.compile(Files.readString(Path.of(args[i])),abc,index,args[i],abc.instance_info.size(),abc.script_info.size(),false,null);
            index.refreshAbc(abc);
        }
        try(OutputStream out=new FileOutputStream(args[1])){abc.saveToStream(out);}
    }
}
