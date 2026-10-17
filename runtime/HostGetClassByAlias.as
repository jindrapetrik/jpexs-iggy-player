package com.jpexs.iggy.runtime {
    public function HostGetClassByAlias(aliasName:String):Class {
        var type:Class = HostAmfAliases.find(aliasName);
        if (type == null) throw new ReferenceError("Class alias was not registered: " + aliasName);
        return type;
    }
}
