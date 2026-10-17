package com.jpexs.iggy.runtime {
    import flash.system.ApplicationDomain;
    public function HostGetDefinitionByName(name:String):Object {
        return ApplicationDomain.currentDomain.getDefinition(name);
    }
}
