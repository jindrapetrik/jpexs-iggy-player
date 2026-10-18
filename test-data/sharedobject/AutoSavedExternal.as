package {
    import flash.net.SharedObject;
    import flash.utils.IDataOutput;
    public class AutoSavedExternal extends SavedExternal {
        override public function writeExternal(output:IDataOutput):void {
            SharedObject.getLocal("auto"); super.writeExternal(output);
        }
    }
}
