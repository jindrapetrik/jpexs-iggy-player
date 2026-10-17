package flash.system {
    import com.jpexs.iggy.runtime.HostDefinitionRequest;
    import com.jpexs.iggy.runtime.HostDomainBinding;
    import com.jpexs.iggy.runtime.HostDomainFactory;
    import flash.external.ExternalInterface;
    public class ApplicationDomain {
        private static var domains:Object = {};
        private static var mainDomain:ApplicationDomain = __iggyDomain(1);
        private var id:uint;
        public function ApplicationDomain(parentDomain:ApplicationDomain = null, existingId:int = -1) {
            var parent:String = parentDomain == null ? "0" : String(parentDomain.id);
            var result:String = String(ExternalInterface.call("__iggy_image_domain_init", parent + "|" + existingId));
            if (result.indexOf("error|") == 0) throw new Error(result);
            id = uint(result);
            domains[id] = this;
            new HostDomainBinding(this, id);
        }
        public static function __iggyDomain(value:uint):ApplicationDomain {
            if (domains[value] != null) return domains[value];
            return HostDomainFactory.create(value);
        }
        public function get __iggyId():uint { return id; }
        public static function get currentDomain():ApplicationDomain {
            return __iggyDomain(uint(ExternalInterface.call("__iggy_image_domain_current", "")));
        }
        public function get parentDomain():ApplicationDomain {
            var parent:int = int(ExternalInterface.call("__iggy_image_domain_parent", String(id)));
            return parent < 0 ? null : __iggyDomain(uint(parent));
        }
        public function hasDefinition(name:String):Boolean {
            if (name == null) throw new TypeError("Definition name is null");
            return new HostDefinitionRequest(id, name).content != null;
        }
        public function getDefinition(name:String):Object {
            if (name == null) throw new TypeError("Definition name is null");
            var result:Object = new HostDefinitionRequest(id, name).content;
            if (result == null) throw new ReferenceError("Definition " + name + " could not be found");
            return result;
        }
        public function getQualifiedDefinitionNames():Array {
            var text:String = String(ExternalInterface.call("__iggy_image_domain_names", String(id)));
            var result:Array = [];
            if (text != "") for each (var name:String in text.split("|")) result.push(name);
            return result;
        }
    }
}
