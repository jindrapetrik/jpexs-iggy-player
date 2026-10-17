package com.jpexs.iggy.runtime {
    import flash.system.ApplicationDomain;
    import flash.utils.getQualifiedClassName;
    public class HostAmfAliases {
        private static var aliases:Array = [];
        public static function register(alias:String, type:Class):void {
            if (alias == null || type == null) throw new TypeError("Class alias and class must be non-null");
            var domain:uint = ApplicationDomain.currentDomain.__iggyId;
            for each (var item:Object in aliases) if (item.domain == domain && item.alias == alias) { item.type = type; item.name = getQualifiedClassName(type); return; }
            if (aliases.length >= 100000) throw new RangeError("Class alias limit exceeded");
            aliases.push({alias:alias, type:type, name:getQualifiedClassName(type), domain:domain});
        }
        public static function find(alias:String):Class {
            if (alias == null) throw new TypeError("Class alias is null");
            var domain:ApplicationDomain = ApplicationDomain.currentDomain;
            while (domain != null) {
                for each (var item:Object in aliases) if (item.domain == domain.__iggyId && item.alias == alias) return item.type;
                domain = domain.parentDomain;
            }
            return null;
        }
        public static function name(value:*):String {
            var qualified:String = getQualifiedClassName(value);
            var domain:ApplicationDomain = ApplicationDomain.currentDomain;
            while (domain != null) {
                for (var i:int = aliases.length - 1; i >= 0; i--) { var item:Object = aliases[i]; if (item.domain == domain.__iggyId && item.name == qualified) return item.alias; }
                domain = domain.parentDomain;
            }
            return "";
        }
    }
}
