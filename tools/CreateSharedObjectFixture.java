import com.jpexs.decompiler.flash.SWF;
import com.jpexs.decompiler.flash.tags.SymbolClassTag;
import java.nio.file.*;

public class CreateSharedObjectFixture {
    public static void main(String[] args) throws Exception {
        Path output = Path.of(args[0]); SWF movie = CreateBytesLoaderFixture.movie();
        for (String name : new String[]{"SavedRecord", "SavedExternal", "StorageMain"}) CreateBytesLoaderFixture.compile(movie, null, Files.readString(output.resolveSibling("sharedobject/" + name + ".as")));
        SymbolClassTag symbols = new SymbolClassTag(movie); symbols.tags.add(0); symbols.names.add("StorageMain");
        CreateBytesLoaderFixture.write(movie, output, symbols);
        SWF only = CreateBytesLoaderFixture.movie();
        CreateBytesLoaderFixture.compile(only, null, Files.readString(output.resolveSibling("sharedobject/SharedOnly.as")));
        SymbolClassTag onlySymbols = new SymbolClassTag(only); onlySymbols.tags.add(0); onlySymbols.names.add("SharedOnly");
        CreateBytesLoaderFixture.write(only, output.resolveSibling("sharedobject/only"), onlySymbols);
    }
}
