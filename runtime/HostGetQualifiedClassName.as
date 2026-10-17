package com.jpexs.iggy.runtime {
    import flash.utils.getQualifiedClassName;
    public function HostGetQualifiedClassName(value:*):String {
        return HostDomainNames.original(getQualifiedClassName(value));
    }
}
