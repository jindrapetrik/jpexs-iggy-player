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
        Files.writeString(data.resolve("form.txt"),"name=Jindra+Pet%C5%99%C3%ADk&name=JPEXS&%C5%BE=%F0%9F%99%82&plus=%2B+%2b",StandardCharsets.UTF_8);
        Files.writeString(data.resolve("invalid-form.txt"),"missing-value",StandardCharsets.UTF_8);
        for(String main:new String[]{"DataMain","DataChild","DataOnly","DataVariablesOnly"}) {
            SWF movie=CreateBytesLoaderFixture.movie();CreateBytesLoaderFixture.compile(movie,null,Files.readString(data.resolve(main+".as")));
            SymbolClassTag symbols=new SymbolClassTag(movie);symbols.tags.add(0);symbols.names.add(main);
            CreateBytesLoaderFixture.write(movie,main.equals("DataMain")?output:data.resolve(main.equals("DataChild")?"sub/child":main.equals("DataOnly")?"only":"variables-only"),symbols);
        }
        SWF http=CreateBytesLoaderFixture.movie();CreateBytesLoaderFixture.compile(http,null,Files.readString(data.resolve("HttpMain.as")));
        SymbolClassTag symbols=new SymbolClassTag(http);symbols.tags.add(0);symbols.names.add("HttpMain");
        CreateBytesLoaderFixture.write(http,output.resolveSibling("http-loader"),symbols);
        SWF only=CreateBytesLoaderFixture.movie();CreateBytesLoaderFixture.compile(only,null,Files.readString(data.resolve("RequestOnly.as")));
        SymbolClassTag requestSymbols=new SymbolClassTag(only);requestSymbols.tags.add(0);requestSymbols.names.add("RequestOnly");
        CreateBytesLoaderFixture.write(only,data.resolve("request-only"),requestSymbols);
        for(String main:new String[]{"NetworkDisplayChild","NetworkDisplayMain"}) {
            SWF display=CreateBytesLoaderFixture.movie();CreateBytesLoaderFixture.compile(display,null,Files.readString(data.resolve(main+".as")));
            SymbolClassTag displaySymbols=new SymbolClassTag(display);displaySymbols.tags.add(0);displaySymbols.names.add(main);
            CreateBytesLoaderFixture.write(display,main.equals("NetworkDisplayMain")?output.resolveSibling("network-display"):data.resolve("network-child"),displaySymbols);
        }
    }
}
