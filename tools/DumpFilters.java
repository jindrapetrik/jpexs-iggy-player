import com.jpexs.decompiler.flash.SWF;
import com.jpexs.decompiler.flash.tags.*;
import com.jpexs.decompiler.flash.tags.base.*;
import com.jpexs.decompiler.flash.types.filters.FILTER;
import java.io.*;
import java.lang.reflect.*;
import java.util.Arrays;

public class DumpFilters {
    public static void main(String[] args) throws Exception {
        SWF swf;
        try (InputStream input = new FileInputStream(args[0])) {
            swf = new SWF(input, false, "UTF-8");
        }
        int frame = 1;
        for (Tag tag : swf.getTags()) {
            if (frame == 51 && tag instanceof PlaceObjectTypeTag) {
                PlaceObjectTypeTag place = (PlaceObjectTypeTag) tag;
                System.out.println("Character " + place.getCharacterId() + " matrix=" + place.getMatrix());
                if (place.getFilters() != null) for (FILTER filter : place.getFilters()) {
                    System.out.println("  " + filter.getClass().getSimpleName());
                    for (Field field : filter.getClass().getFields()) {
                        if (Modifier.isStatic(field.getModifiers())) continue;
                        Object value = field.get(filter);
                        if (value instanceof float[]) value = Arrays.toString((float[]) value);
                        if (value instanceof int[]) value = Arrays.toString((int[]) value);
                        if (value instanceof Object[]) value = Arrays.toString((Object[]) value);
                        System.out.println("    " + field.getName() + "=" + value);
                    }
                }
            }
            if (tag instanceof ShowFrameTag) frame++;
        }
    }
}
