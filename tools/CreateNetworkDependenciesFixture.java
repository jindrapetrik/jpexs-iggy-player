import com.jpexs.decompiler.flash.SWF;
import com.jpexs.decompiler.flash.abc.ABC;
import com.jpexs.decompiler.flash.tags.*;
import com.jpexs.decompiler.flash.tags.profiles.iggy.IggyExternalImage;
import com.jpexs.decompiler.flash.types.*;
import com.jpexs.decompiler.flash.types.shaperecords.*;
import java.nio.file.*;

public class CreateNetworkDependenciesFixture {
    static void imports(SWF movie, String url) { ImportAssets2Tag tag=new ImportAssets2Tag(movie);tag.url=url;movie.addTag(tag); }
    static SymbolClassTag symbols(SWF movie, String name, int id) { SymbolClassTag tag=new SymbolClassTag(movie);tag.tags.add(id);tag.names.add(name);return tag; }
    static void image(SWF movie, String name) {
        IggyExternalImage image=new IggyExternalImage(movie);image.characterID=1;image.width=4;image.height=4;
        image.fileName=name+".raw.iggytex";image.resourceType=8;image.resourceOffset=0;image.resourceFormat=0x100;image.resourceWidth=4;image.resourceHeight=4;movie.addTag(image);
        DefineShape3Tag shape=new DefineShape3Tag(movie);shape.shapeId=2;shape.shapeBounds=new RECT(0,800,0,800);
        FILLSTYLE fill=new FILLSTYLE();fill.fillStyleType=0x41;fill.bitmapId=1;fill.bitmapMatrix=new MATRIX();fill.bitmapMatrix.hasScale=true;fill.bitmapMatrix.scaleX=200;fill.bitmapMatrix.scaleY=200;
        shape.shapes.fillStyles.fillStyles=new FILLSTYLE[]{fill};shape.shapes.numFillBits=1;
        StyleChangeRecord start=new StyleChangeRecord();start.stateMoveTo=true;start.stateFillStyle1=true;start.fillStyle1=1;shape.shapes.shapeRecords.add(start);
        for(int[] delta:new int[][]{{800,0},{0,800},{-800,0},{0,-800}}){StraightEdgeRecord edge=new StraightEdgeRecord();edge.generalLineFlag=true;edge.deltaX=delta[0];edge.deltaY=delta[1];edge.calculateBits();shape.shapes.shapeRecords.add(edge);}
        shape.shapes.shapeRecords.add(new EndShapeRecord());movie.addTag(shape);
    }
    public static void main(String[] args) throws Exception {
        Path output=Path.of(args[0]),data=output.resolveSibling("network-dependencies");Files.createDirectories(data);
        SWF leaf=CreateBytesLoaderFixture.movie();CreateBytesLoaderFixture.compile(leaf,null,"package { public class RemoteLeaf { public static var marker:uint=37; public function RemoteLeaf() {} } }");
        CreateBytesLoaderFixture.write(leaf,data.resolve("leaf"),symbols(leaf,"RemoteLeaf",0));
        SWF library=CreateBytesLoaderFixture.movie();CreateBytesLoaderFixture.compile(library,CreateBytesLoaderFixture.abc(leaf),"package { import flash.display.Shape; public class RemoteTile extends Shape { public var marker:uint; public function RemoteTile() {super();marker=RemoteLeaf.marker;} } }");
        imports(library,"nested/leaf.swf");image(library,"library");CreateBytesLoaderFixture.write(library,data.resolve("library"),symbols(library,"RemoteTile",2));
        SWF child=CreateBytesLoaderFixture.movie();CreateBytesLoaderFixture.compile(child,CreateBytesLoaderFixture.abc(library),Files.readString(data.resolve("Child.as")));
        imports(child,"sub folder/library.swf");imports(child,"leaf.swf");image(child,"root");
        child.addTag(new PlaceObject3Tag(child,false,1,"RemoteTile",-1,new MATRIX(),null,-1,"tile",-1,null,-1,null,-1,null,null,false));
        MATRIX moved=new MATRIX();moved.translateX=1200;
        child.addTag(new PlaceObject2Tag(child,false,2,2,moved,null,-1,null,-1,null));
        CreateBytesLoaderFixture.write(child,data.resolve("root"),symbols(child,"RemoteChild",0));
        SWF bytesChild=CreateBytesLoaderFixture.movie();CreateBytesLoaderFixture.compile(bytesChild,CreateBytesLoaderFixture.abc(library),Files.readString(data.resolve("Child.as")));
        imports(bytesChild,"sub folder/library.swf");imports(bytesChild,"leaf.swf");
        bytesChild.addTag(new PlaceObject3Tag(bytesChild,false,1,"RemoteTile",-1,new MATRIX(),null,-1,"tile",-1,null,-1,null,-1,null,null,false));
        CreateBytesLoaderFixture.write(bytesChild,data.resolve("bytes-root"),symbols(bytesChild,"RemoteChild",0));
        SWF bytesParent=CreateBytesLoaderFixture.movie();CreateBytesLoaderFixture.compile(bytesParent,null,Files.readString(data.resolve("BytesParent.as")));
        CreateBytesLoaderFixture.write(bytesParent,data.resolve("bytes-parent"),symbols(bytesParent,"RemoteBytesParent",0));
        for(String dependency:new String[]{"../root.swf","file:///forbidden.swf"}) {
            SWF broken=CreateBytesLoaderFixture.movie();imports(broken,dependency);
            CreateBytesLoaderFixture.write(broken,data.resolve(dependency.startsWith("file")?"unsafe":"cycle"),new SymbolClassTag(broken));
        }
        byte[] cyan=new byte[64],magenta=new byte[64];
        for(int i=0;i<16;i++){cyan[4*i+1]=(byte)255;cyan[4*i+2]=(byte)255;cyan[4*i+3]=(byte)255;magenta[4*i]=(byte)255;magenta[4*i+2]=(byte)255;magenta[4*i+3]=(byte)255;}
        Files.write(data.resolve("library.raw.iggytex"),cyan);Files.write(data.resolve("root.raw.iggytex"),magenta);
        SWF main=CreateBytesLoaderFixture.movie();CreateBytesLoaderFixture.compile(main,null,Files.readString(data.resolve("Main.as")));
        CreateBytesLoaderFixture.write(main,output,symbols(main,"NetworkDependenciesMain",0));
    }
}
