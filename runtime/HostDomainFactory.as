package com.jpexs.iggy.runtime {
    import flash.system.ApplicationDomain;
    public class HostDomainFactory {
        private static var xmlInitialized:Boolean;
        // Keep construction outside ApplicationDomain's static initialization.
        public static function create(id:uint):ApplicationDomain { return new ApplicationDomain(null, int(id)); }
        public static function main():ApplicationDomain {
            if (!xmlInitialized) {
                xmlInitialized = true;
                // Assign through the VM after its script scopes exist. Native
                // pre-initialization writes leave invalid GC slots in 1.2.30.
                XML.prettyPrinting = true; XML.prettyIndent = 2; XML.ignoreComments = true;
                XML.ignoreProcessingInstructions = true; XML.ignoreWhitespace = true;
            }
            return ApplicationDomain.__iggyDomain(1);
        }
    }
}
