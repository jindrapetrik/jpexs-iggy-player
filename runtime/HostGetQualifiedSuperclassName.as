package com.jpexs.iggy.runtime {
    import flash.utils.getQualifiedSuperclassName;
    public function HostGetQualifiedSuperclassName(value:*):String {
        var name:String = getQualifiedSuperclassName(value);
        return name == null ? null : HostDomainNames.original(name);
    }
}
