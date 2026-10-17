import com.jpexs.decompiler.flash.SWF;
import com.jpexs.decompiler.flash.tags.SymbolClassTag;
import com.jpexs.decompiler.flash.tags.ImportAssets2Tag;
import java.nio.file.*;

public class CreateAmfFixture {
    public static void main(String[] args) throws Exception {
        Path output = Path.of(args[0]); SWF movie = CreateBytesLoaderFixture.movie();
        for (String name : new String[]{"AmfBase", "AmfRecord", "AmfSealed"}) CreateBytesLoaderFixture.compile(movie, null, Files.readString(output.resolveSibling("amf/" + name + ".as")));
        CreateBytesLoaderFixture.compile(movie, null, Files.readString(output.resolveSibling("amf/AmfMain.as")));
        SymbolClassTag symbols = new SymbolClassTag(movie); symbols.tags.add(0); symbols.names.add("AmfMain");
        CreateBytesLoaderFixture.write(movie, output, symbols);
        SWF library = CreateBytesLoaderFixture.movie();
        CreateBytesLoaderFixture.compile(library, null, Files.readString(output.resolveSibling("amf/ImportedRecord.as")));
        CreateBytesLoaderFixture.write(library, output.resolveSibling("amf/library"), new SymbolClassTag(library));
        SWF child = CreateBytesLoaderFixture.movie();
        CreateBytesLoaderFixture.compile(child, CreateBytesLoaderFixture.abc(library), Files.readString(output.resolveSibling("amf/AmfChild.as")));
        ImportAssets2Tag imports = new ImportAssets2Tag(child); imports.url = "library.swf"; child.addTag(imports);
        SymbolClassTag childSymbols = new SymbolClassTag(child); childSymbols.tags.add(0); childSymbols.names.add("AmfChild");
        CreateBytesLoaderFixture.write(child, output.resolveSibling("amf/child"), childSymbols);
    }
}
