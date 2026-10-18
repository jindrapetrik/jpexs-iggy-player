import com.jpexs.decompiler.flash.SWF;
import com.jpexs.decompiler.flash.tags.SymbolClassTag;
import java.nio.file.*;
import java.nio.charset.StandardCharsets;
import java.util.Arrays;

public class CreateDataLoaderFixture {
    public static void main(String[] args) throws Exception {
        Path output=Path.of(args[0]), data=output.resolveSibling("url-data");Files.createDirectories(data.resolve("sub"));
        String text="žluťoučký🙂\u0000tail";Files.writeString(data.resolve("utf8.txt"),text,StandardCharsets.UTF_8);
        Files.write(data.resolve("utf16.txt"),("\ufeff"+text).getBytes(StandardCharsets.UTF_16LE));
        Files.writeString(data.resolve("žluťoučký🙂.txt"),"unicode");Files.write(data.resolve("empty"),new byte[0]);
        byte[] bytes=new byte[2097152];for(int i=0;i<bytes.length;i++)bytes[i]=(byte)i;Files.write(data.resolve("bytes.bin"),bytes);
        Arrays.fill(bytes,(byte)'A');Files.write(data.resolve("large.txt"),bytes);
        Files.write(data.resolve("raw.swf"),new byte[]{70,87,83,8});Files.copy(output.resolveSibling("as3.iggy"),data.resolve("raw.iggy"),StandardCopyOption.REPLACE_EXISTING);
        Files.writeString(data.resolve("sub/local.txt"),"child-relative");
        for(String main:new String[]{"DataMain","DataChild","DataOnly"}) {
            SWF movie=CreateBytesLoaderFixture.movie();CreateBytesLoaderFixture.compile(movie,null,Files.readString(data.resolve(main+".as")));
            SymbolClassTag symbols=new SymbolClassTag(movie);symbols.tags.add(0);symbols.names.add(main);
            CreateBytesLoaderFixture.write(movie,main.equals("DataMain")?output:data.resolve(main.equals("DataChild")?"sub/child":"only"),symbols);
        }
    }
}
