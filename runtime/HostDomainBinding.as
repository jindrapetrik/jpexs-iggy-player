package com.jpexs.iggy.runtime {
    import flash.display.LoaderInfo;
    import flash.system.ApplicationDomain;
    public class HostDomainBinding extends LoaderInfo {
        public function HostDomainBinding(domain:ApplicationDomain, id:uint) {
            super("__iggy_bind_domain", domain, String(id));
        }
    }
}
