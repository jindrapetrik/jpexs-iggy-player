package com.jpexs.iggy.runtime {
    import flash.display.Bitmap;
    import flash.display.BitmapData;
    import flash.display.DisplayObject;
    import flash.display.LoaderInfo;
    public class HostLoadedBitmap extends Bitmap {
        private var info:LoaderInfo;
        public function HostLoadedBitmap(data:BitmapData, owner:LoaderInfo) { super(data); info = owner; }
        override public function get loaderInfo():LoaderInfo { return info; }
        override public function get root():DisplayObject { return this; }
    }
}
