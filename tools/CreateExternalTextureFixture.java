import com.jpexs.decompiler.flash.SWF;
import static com.jpexs.decompiler.flash.iggy.IggyTextureDecoder.*;
import com.jpexs.decompiler.flash.tags.*;
import com.jpexs.decompiler.flash.tags.profiles.iggy.IggyExternalImage;
import com.jpexs.decompiler.flash.types.*;
import com.jpexs.decompiler.flash.types.shaperecords.*;
import java.io.ByteArrayOutputStream;
import java.nio.file.*;

/** Reproducible raw-resource format/mipmap fixture; no proprietary pixel data. */
public class CreateExternalTextureFixture {
    static byte[] hex(String text) {
        byte[] result=new byte[text.length()/2];
        for(int i=0;i<result.length;i++)result[i]=(byte)Integer.parseInt(text.substring(i*2,i*2+2),16);
        return result;
    }
    public static void main(String[] args) throws Exception {
        Path output=Paths.get(args[0]);SWF movie=CreateBytesLoaderFixture.movie();
        movie.displayRect=new RECT(0,7000,0,2000);
        int[] formats={FORMAT_RGBA_8888,FORMAT_RGBA_4444_LE,FORMAT_RGBA_5551_LE,FORMAT_LA_88,FORMAT_LA_44,FORMAT_I_8,FORMAT_I_4,FORMAT_L_8,FORMAT_L_4,FORMAT_DXT1,FORMAT_DXT3,FORMAT_DXT5,FORMAT_BC7};
        String[] texels={"ff0000ff","f0ff","1f80","80ff","f5","80","77","66","77",
            "00f8000000000000","ffffffffffffffff1f00000000000000","ff00000000000000e007000000000000",
            "20ff3f000000fcff0300000000000000"};
        ByteArrayOutputStream resources=new ByteArrayOutputStream();resources.write(new byte[13]);
        for(int i=0;i<texels.length;i++) {
            int format=formats[i],id=i+1,offset=resources.size();byte[] texel=hex(texels[i]);
            // Two contiguous mip levels. The compressed 2x2 level still occupies a full block.
            for(int level=0;level<2;level++)for(int n=0;n<(format==FORMAT_DXT1||format==FORMAT_DXT3||format==FORMAT_DXT5||format==FORMAT_BC7?1:level==0?16:4);n++)resources.write(texel);
            IggyExternalImage image=new IggyExternalImage(movie);image.characterID=id;image.width=image.height=4;
            image.fileName=output.getFileName()+".raw.iggytex";image.resourceType=8;image.resourceOffset=offset;
            image.resourceFormat=0x200|format;image.resourceWidth=image.resourceHeight=4;movie.addTag(image);
            DefineShape3Tag shape=new DefineShape3Tag(movie);shape.shapeId=101+i;shape.shapeBounds=new RECT(0,800,0,800);
            FILLSTYLE fill=new FILLSTYLE();fill.fillStyleType=0x41;fill.bitmapId=id;fill.bitmapMatrix=new MATRIX();
            fill.bitmapMatrix.hasScale=true;fill.bitmapMatrix.scaleX=fill.bitmapMatrix.scaleY=200;
            shape.shapes.fillStyles.fillStyles=new FILLSTYLE[]{fill};shape.shapes.numFillBits=1;
            StyleChangeRecord start=new StyleChangeRecord();start.stateMoveTo=true;start.stateFillStyle1=true;start.fillStyle1=1;shape.shapes.shapeRecords.add(start);
            for(int[] delta:new int[][]{{800,0},{0,800},{-800,0},{0,-800}}){StraightEdgeRecord edge=new StraightEdgeRecord();edge.generalLineFlag=true;edge.deltaX=delta[0];edge.deltaY=delta[1];edge.calculateBits();shape.shapes.shapeRecords.add(edge);}
            shape.shapes.shapeRecords.add(new EndShapeRecord());movie.addTag(shape);
            MATRIX matrix=new MATRIX();matrix.translateX=(10+i%7*50)*20;matrix.translateY=(10+i/7*50)*20;
            movie.addTag(new PlaceObject2Tag(movie,false,id,shape.shapeId,matrix,null,-1,null,-1,null));
        }
        Files.write(Paths.get(output+".raw.iggytex"),resources.toByteArray());
        CreateBytesLoaderFixture.write(movie,output,new SymbolClassTag(movie));
    }
}
