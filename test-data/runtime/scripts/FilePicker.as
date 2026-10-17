package {
    import flash.net.FileReferenceList;

    public class FilePicker extends FileReferenceList {
        public var inherited:Boolean;
        public function FilePicker() {
            super();
            inherited = this is FileReferenceList;
        }
    }
}
