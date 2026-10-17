import com.jpexs.decompiler.flash.SWF;
import com.jpexs.decompiler.flash.tags.DefineBinaryDataTag;
import com.jpexs.decompiler.flash.tags.SymbolClassTag;
import com.jpexs.helpers.ByteArrayRange;
import java.io.*;
import java.nio.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.zip.Deflater;

public class CreateByteArrayFixture {
    static byte[] numeric(ByteOrder order) {
        ByteBuffer bytes = ByteBuffer.allocate(80).order(order);
        bytes.put((byte) 1).put((byte) 0).put((byte) 128).putShort((short) -12345).putInt(-123456789).putInt(0xfedcba98);
        bytes.putFloat(1.5f).putDouble(-Math.PI).putFloat(Float.POSITIVE_INFINITY).putDouble(Double.NaN);
        bytes.putInt(0x80000000).putLong(0x8000000000000000L).putFloat(Float.MIN_VALUE).putDouble(Double.MIN_VALUE);
        byte[] result = new byte[bytes.position()]; bytes.flip(); bytes.get(result); return result;
    }
    static byte[] compress(byte[] source, boolean raw, int level) throws IOException {
        Deflater encoder = new Deflater(level, raw); encoder.setInput(source); encoder.finish();
        ByteArrayOutputStream output = new ByteArrayOutputStream(); byte[] buffer = new byte[4096];
        while (!encoder.finished()) { int size = encoder.deflate(buffer); output.write(buffer, 0, size); }
        encoder.end(); return output.toByteArray();
    }
    public static void main(String[] args) throws Exception {
        Path output = Path.of(args[0]), source = output.resolveSibling("bytearray/ByteArrayMain.as");
        byte[] text = "Příliš žluťoučký kůň 🐕\n".repeat(2000).getBytes(StandardCharsets.UTF_8);
        byte[] png = Files.readAllBytes(output.resolveSibling("loader/colors.png"));
        String[] names = {"NumericBig", "NumericLittle", "ZlibText", "RawText", "StoredText", "FixedText", "CompressedPng"};
        byte[][] data = {numeric(ByteOrder.BIG_ENDIAN), numeric(ByteOrder.LITTLE_ENDIAN), compress(text, false, 6),
            compress(text, true, 6), compress(text, true, 0), new byte[]{0x73, 0x74, 0x72, 0x06, 0}, compress(png, false, 6)};
        SWF movie = CreateBytesLoaderFixture.movie(); SymbolClassTag symbols = new SymbolClassTag(movie);
        symbols.tags.add(0); symbols.names.add("ByteArrayMain");
        for (int i = 0; i < names.length; i++) {
            CreateBytesLoaderFixture.compile(movie, null, "package { import flash.utils.ByteArray; public class " + names[i] + " extends ByteArray { public function " + names[i] + "() { super(); } } }");
            DefineBinaryDataTag tag = new DefineBinaryDataTag(movie); tag.tag = 10 + i; tag.binaryData = new ByteArrayRange(data[i]); movie.addTag(tag);
            symbols.tags.add(tag.tag); symbols.names.add(names[i]);
        }
        CreateBytesLoaderFixture.compile(movie, null, Files.readString(source)); CreateBytesLoaderFixture.write(movie, output, symbols);
        SWF only = CreateBytesLoaderFixture.movie();
        CreateBytesLoaderFixture.compile(only, null, "package { import flash.display.MovieClip; import flash.utils.ByteArray; import flash.utils.Endian; public class ByteArrayOnly extends MovieClip { public var success:Boolean; public function ByteArrayOnly() { super(); stop(); var bytes:ByteArray = new ByteArray(); bytes.endian = Endian.LITTLE_ENDIAN; bytes.writeFloat(1.5); bytes.position = 0; success = bytes.readFloat() == 1.5; } } }");
        SymbolClassTag onlySymbols = new SymbolClassTag(only); onlySymbols.tags.add(0); onlySymbols.names.add("ByteArrayOnly");
        CreateBytesLoaderFixture.write(only, output.resolveSibling("bytearray/only"), onlySymbols);
    }
}
