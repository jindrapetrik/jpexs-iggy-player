package com.jpexs.iggy.runtime {
    import flash.system.ApplicationDomain;
    public class HostDomainFactory {
        // Keep construction outside ApplicationDomain's static initialization.
        public static function create(id:uint):ApplicationDomain { return new ApplicationDomain(null, int(id)); }
        public static function main():ApplicationDomain { return ApplicationDomain.__iggyDomain(1); }
    }
}
