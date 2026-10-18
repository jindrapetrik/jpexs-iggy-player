package com.jpexs.iggy.runtime {
    import flash.utils.ByteArray;
    public class HostURLRequest {
        public var url:String;
        public var data:Object;
        private var verb:String = "GET";
        public var contentType:String = "application/x-www-form-urlencoded";
        public var requestHeaders:Array = [];
        public var digest:String;
        public function HostURLRequest(url:String = null) { this.url = url; }
        public function get method():String { return verb; }
        public function set method(value:String):void {
            if (value != "GET" && value != "POST") throw new ArgumentError("URLRequest supports GET and POST");
            verb = value;
        }
        internal function packet():ByteArray {
            var target:String = url;
            var body:ByteArray;
            var text:String;
            if (data is ByteArray) {
                if (verb == "GET") throw new ArgumentError("GET does not support ByteArray data");
                body = data as ByteArray;
            } else if (data != null) {
                text = String(data);
                if (verb == "GET" && text.length != 0) {
                    var hash:int = target.indexOf("#");
                    var fragment:String = hash < 0 ? "" : target.substr(hash);
                    if (hash >= 0) target = target.substr(0, hash);
                    target += (target.indexOf("?") < 0 ? "?" : (target.charAt(target.length - 1) == "?" || target.charAt(target.length - 1) == "&") ? "" : "&") + text + fragment;
                } else if (verb == "POST") { body = new ByteArray(); body.writeUTFBytes(text); }
            }
            if (body != null && body.length > 33554432) throw new RangeError("URLRequest data exceeds 32 MiB");
            var headers:Array = requestHeaders == null ? [] : requestHeaders;
            if (headers.length > 64) throw new ArgumentError("Too many HTTP request headers");
            var packet:ByteArray = new ByteArray(); packet.endian = "bigEndian";
            packet.writeUTF(target);
            // Flash changes a POST with no body into GET.
            packet.writeUTF(verb == "POST" && (body == null || body.length == 0) ? "GET" : verb);
            var type:String = contentType == null ? "" : contentType;
            headerValue(type); packet.writeUTF(type); packet.writeUnsignedInt(headers.length);
            var total:uint;
            for each (var entry:* in headers) {
                if (!(entry is HostURLRequestHeader)) throw new ArgumentError("Expected URLRequestHeader");
                var header:HostURLRequestHeader = entry as HostURLRequestHeader;
                headerName(header.name); headerValue(header.value);
                total += header.name.length + header.value.length;
                if (total > 32768) throw new ArgumentError("HTTP request headers exceed 32 KiB");
                packet.writeUTF(header.name); packet.writeUTF(header.value);
            }
            if (packet.length > 131072) throw new RangeError("URLRequest metadata exceeds 128 KiB");
            packet.writeUnsignedInt(body == null ? 0 : body.length);
            if (body != null && body.length != 0) packet.writeBytes(body, 0, body.length);
            return packet;
        }
        private static function headerName(value:String):void {
            if (value == null || value.length == 0 || value.length > 256) throw new ArgumentError("Invalid HTTP header name");
            for (var i:int = 0; i < value.length; i++) {
                var code:uint = value.charCodeAt(i);
                if (!((code >= 48 && code <= 57) || (code >= 65 && code <= 90) || (code >= 97 && code <= 122) || "!#$%&'*+-.^_`|~".indexOf(value.charAt(i)) >= 0)) throw new ArgumentError("Invalid HTTP header name");
            }
            var blocked:String = "|accept-charset|accept-encoding|accept-ranges|age|allow|allowed|authorization|charge-to|connect|connection|content-length|content-location|content-range|cookie|date|delete|etag|expect|get|head|host|if-modified-since|keep-alive|last-modified|location|max-forwards|options|origin|post|proxy-authenticate|proxy-authorization|proxy-connection|public|put|range|referer|request-range|retry-after|server|te|trace|trailer|transfer-encoding|upgrade|uri|user-agent|vary|via|warning|www-authenticate|x-flash-version|";
            if (blocked.indexOf("|" + value.toLowerCase().split("_").join("-") + "|") >= 0) throw new ArgumentError("Restricted HTTP request header");
        }
        private static function headerValue(value:String):void {
            if (value == null || value.length > 8192) throw new ArgumentError("Invalid HTTP header value");
            for (var i:int = 0; i < value.length; i++) {
                var code:uint = value.charCodeAt(i);
                if ((code < 32 && code != 9) || code == 127 || code > 255) throw new ArgumentError("Invalid HTTP header value");
            }
        }
    }
}
