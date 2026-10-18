import com.jpexs.decompiler.flash.SWF;
import com.jpexs.decompiler.flash.tags.*;
import com.jpexs.helpers.ByteArrayRange;
import java.nio.file.*;
public class CreateSoundLoaderFixture {
    public static void main(String[] args) throws Exception {
        Path output=Path.of(args[0]);SWF movie=CreateBytesLoaderFixture.movie();
        CreateBytesLoaderFixture.compile(movie,null,"package { import flash.media.Sound; public class EmbeddedTone extends Sound { public function EmbeddedTone(){super();} } }");
        CreateBytesLoaderFixture.compile(movie,null,Files.readString(output.resolveSibling("audio/scripts/SoundLoaderMain.as")));
        DefineSoundTag sound=new DefineSoundTag(movie);sound.soundId=10;sound.soundFormat=3;sound.soundRate=2;sound.soundSize=true;sound.soundSampleCount=2205;
        byte[] pcm=new byte[4410];for(int i=0;i<2205;i++){int value=(int)(Math.sin(i*2*Math.PI*440/22050)*180);pcm[i*2]=(byte)value;pcm[i*2+1]=(byte)(value>>8);}sound.soundData=new ByteArrayRange(pcm);movie.addTag(sound);
        SymbolClassTag symbols=new SymbolClassTag(movie);symbols.tags.add(0);symbols.names.add("SoundLoaderMain");symbols.tags.add(10);symbols.names.add("EmbeddedTone");
        CreateBytesLoaderFixture.write(movie,output,symbols);
        SWF child=CreateBytesLoaderFixture.movie();CreateBytesLoaderFixture.compile(child,null,Files.readString(output.resolveSibling("audio/scripts/SoundLoaderChild.as")));
        SymbolClassTag childSymbols=new SymbolClassTag(child);childSymbols.tags.add(0);childSymbols.names.add("SoundLoaderChild");
        CreateBytesLoaderFixture.write(child,output.resolveSibling("audio/sound-child"),childSymbols);
    }
}
