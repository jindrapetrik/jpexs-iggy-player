package com.jpexs.iggy.runtime {
    import flash.display.LoaderInfo;
    public class HostDefinitionRequest extends LoaderInfo {
        public function HostDefinitionRequest(domain:uint, name:String) {
            super("__iggy_definition", String(domain), name);
        }
    }
}
