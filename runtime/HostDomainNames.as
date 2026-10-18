package com.jpexs.iggy.runtime {
    public class HostDomainNames {
        public static function original(name:String):String {
            if (name == "Vector") return "__AS3__.vec::Vector";
            if (name == "com.jpexs.iggy.runtime::HostApplicationDomain") return "flash.system::ApplicationDomain";
            if (name == "com.jpexs.iggy.runtime::HostLoaderContext") return "flash.system::LoaderContext";
            if (name == "com.jpexs.iggy.runtime::HostEndian") return "flash.utils::Endian";
            if (name == "com.jpexs.iggy.runtime::HostCompressionAlgorithm") return "flash.utils::CompressionAlgorithm";
            if (name == "com.jpexs.iggy.runtime::HostObjectEncoding") return "flash.net::ObjectEncoding";
            if (name == "com.jpexs.iggy.runtime::HostURLLoader") return "flash.net::URLLoader";
            if (name == "com.jpexs.iggy.runtime::HostURLLoaderDataFormat") return "flash.net::URLLoaderDataFormat";
            if (name == "com.jpexs.iggy.runtime::HostURLVariables") return "flash.net::URLVariables";
            if (name == "com.jpexs.iggy.runtime::HostHTTPStatusEvent") return "flash.events::HTTPStatusEvent";
            if (name == "com.jpexs.iggy.runtime::HostSecurityErrorEvent") return "flash.events::SecurityErrorEvent";
            var prefix:String = "com.jpexs.iggy.domain.d";
            if (name.indexOf(prefix) != 0) return name;
            var dot:int = name.indexOf(".", prefix.length);
            var separator:int = name.indexOf("::", prefix.length);
            if (separator < 0) return name;
            return dot < 0 || separator < dot ? name.substr(separator + 2) : name.substr(dot + 1);
        }
    }
}
