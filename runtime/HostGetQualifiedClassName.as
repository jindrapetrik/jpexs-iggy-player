package com.jpexs.iggy.runtime {
    import flash.utils.getQualifiedClassName;
    public function HostGetQualifiedClassName(value:*):String {
        var element:* = new HostAmfRequest("vector-type", value).result;
        if (element != null) return "__AS3__.vec::Vector.<" + (element === Vector ? "*" : HostGetQualifiedClassName(element)) + ">";
        return HostDomainNames.original(getQualifiedClassName(value));
    }
}
