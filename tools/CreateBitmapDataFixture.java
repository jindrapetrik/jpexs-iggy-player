import com.jpexs.decompiler.flash.SWF;
import com.jpexs.decompiler.flash.tags.*;
import com.jpexs.helpers.ByteArrayRange;
import java.io.ByteArrayOutputStream;
import java.nio.file.*;
import java.util.zip.DeflaterOutputStream;
public class CreateBitmapDataFixture {
    public static void main(String[] args) throws Exception {
        Path output=Path.of(args[0]);SWF movie=CreateBytesLoaderFixture.movie();
        CreateBytesLoaderFixture.compile(movie,null,"package { import flash.display.BitmapData; public class EmbeddedPixels extends BitmapData { public function EmbeddedPixels(w:int,h:int){super(w,h);} } }");
        CreateBytesLoaderFixture.compile(movie,null,Files.readString(output.resolveSibling("bitmap-data/Main.as")));
        DefineBitsLossless2Tag pixels=new DefineBitsLossless2Tag(movie);pixels.characterID=1;pixels.bitmapFormat=5;pixels.bitmapWidth=4;pixels.bitmapHeight=4;
        ByteArrayOutputStream buffer=new ByteArrayOutputStream();try(DeflaterOutputStream zip=new DeflaterOutputStream(buffer)){for(int i=0;i<16;i++){zip.write(255);zip.write(255);zip.write(0);zip.write(0);}}
        pixels.zlibBitmapData=new ByteArrayRange(buffer.toByteArray());movie.addTag(pixels);
        SymbolClassTag symbols=new SymbolClassTag(movie);symbols.tags.add(0);symbols.names.add("BitmapDataMain");symbols.tags.add(1);symbols.names.add("EmbeddedPixels");
        CreateBytesLoaderFixture.write(movie,output,symbols);
    }
}
