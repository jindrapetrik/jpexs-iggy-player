import com.jpexs.decompiler.flash.SWF;
import com.jpexs.decompiler.flash.tags.*;
import com.jpexs.helpers.ByteArrayRange;
import java.io.ByteArrayOutputStream;
import java.nio.file.*;
import java.util.zip.DeflaterOutputStream;
public class CreateBitmapDataFixture {
    public static void main(String[] args) throws Exception {
        Path output=Path.of(args[0]);SWF movie=CreateBytesLoaderFixture.movie();
        if(args.length>1 && args[1].equals("--unmultiply-probe")) {
            CreateBytesLoaderFixture.compile(movie,null,Files.readString(Path.of("tools/MeasureBitmapUnmultiply.as")));
            SymbolClassTag symbols=new SymbolClassTag(movie);symbols.tags.add(0);symbols.names.add("MeasureBitmapUnmultiply");CreateBytesLoaderFixture.write(movie,output,symbols);return;
        }
        if(args.length>1 && args[1].equals("--copy-reference")) {
            CreateBytesLoaderFixture.compile(movie,null,Files.readString(Path.of("test-data/bitmap-data/CopyPixelCases.as")));
            CreateBytesLoaderFixture.compile(movie,null,"package { public class BitmapCopyReference extends flash.display.MovieClip { public function BitmapCopyReference(){ var request:flash.net.URLRequest=new flash.net.URLRequest(\"/result\");request.method=\"POST\";request.data=CopyPixelCases.run();new flash.net.URLLoader().load(request);stop();} } }");
            SymbolClassTag symbols=new SymbolClassTag(movie);symbols.tags.add(0);symbols.names.add("BitmapCopyReference");CreateBytesLoaderFixture.write(movie,output,symbols);return;
        }
        CreateBytesLoaderFixture.compile(movie,null,"package { import flash.display.BitmapData; public class EmbeddedPixels extends BitmapData { public function EmbeddedPixels(w:int,h:int){super(w,h);} } }");
        CreateBytesLoaderFixture.compile(movie,null,"package { import flash.utils.ByteArray; public class PixelBytes extends ByteArray { public function PixelBytes(){super();} } }");
        CreateBytesLoaderFixture.compile(movie,null,Files.readString(output.resolveSibling("bitmap-data/CopyPixelCases.as")));
        CreateBytesLoaderFixture.compile(movie,null,Files.readString(output.resolveSibling("bitmap-data/Main.as")));
        DefineBitsLossless2Tag pixels=new DefineBitsLossless2Tag(movie);pixels.characterID=1;pixels.bitmapFormat=5;pixels.bitmapWidth=4;pixels.bitmapHeight=4;
        ByteArrayOutputStream buffer=new ByteArrayOutputStream();try(DeflaterOutputStream zip=new DeflaterOutputStream(buffer)){for(int i=0;i<16;i++){zip.write(255);zip.write(255);zip.write(0);zip.write(0);}}
        pixels.zlibBitmapData=new ByteArrayRange(buffer.toByteArray());movie.addTag(pixels);
        SymbolClassTag symbols=new SymbolClassTag(movie);symbols.tags.add(0);symbols.names.add("BitmapDataMain");symbols.tags.add(1);symbols.names.add("EmbeddedPixels");
        DefineBinaryDataTag binary=new DefineBinaryDataTag(movie);binary.tag=2;binary.binaryData=new ByteArrayRange(new byte[]{(byte)255,0x44,0x55,0x66});movie.addTag(binary);symbols.tags.add(2);symbols.names.add("PixelBytes");
        CreateBytesLoaderFixture.write(movie,output,symbols);
    }
}
