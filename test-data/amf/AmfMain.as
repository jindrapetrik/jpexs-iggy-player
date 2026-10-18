package {
    import flash.display.MovieClip;
    import flash.display.Loader;
    import flash.display.LoaderInfo;
    import flash.net.URLRequest;
    import flash.system.ApplicationDomain;
    import flash.system.LoaderContext;
    import flash.events.Event;
    import flash.events.IOErrorEvent;
    import flash.utils.ByteArray;
    import flash.net.ObjectEncoding;
    import flash.net.registerClassAlias;
    import flash.net.getClassByAlias;
    import flash.errors.EOFError;
    import flash.utils.Dictionary; import flash.utils.getQualifiedClassName;
    import flash.xml.XMLDocument;
    import flash.utils.IDataInput;
    import flash.utils.IDataOutput;
    public class AmfMain extends MovieClip {
        public var checks:uint;
        public var failures:uint;
        public var failureList:String = "";
        public var complete:Boolean;
        public var retained:Object;
        public var children:Array = [];
        public var loaded:uint;
        public var retainedAdvanced:Array;
        public function AmfMain() {
            super(); stop();
            try { defaults(); primitives(); graphs(3); graphs(0); typed(3); typed(0); malformed(); advanced(); }
            catch (error:Error) { failures++; failureList += "uncaught at " + checks; }
            for (var i:int = 0; i < 2; i++) {
                var loader:Loader = new Loader(); children.push(loader); addChild(loader);
                loader.contentLoaderInfo.addEventListener(Event.COMPLETE, childLoaded);
                loader.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR, childFailed);
                loader.load(new URLRequest("amf/child.iggy"),new LoaderContext(false,i == 0 ? null : new ApplicationDomain(null)));
            }
        }
        private function check(value:Boolean, name:String):void { checks++; if (!value) { failures++; failureList += name + ","; } }
        private function bytes(text:String, encoding:uint = 3):ByteArray {
            var result:ByteArray = new ByteArray(); result.objectEncoding = encoding;
            for (var i:uint = 0; i < text.length; i += 2) result.writeByte(parseInt(text.substr(i, 2), 16));
            result.position = 0; return result;
        }
        private function hex(value:ByteArray):String {
            var position:uint = value.position; value.position = 0; var result:String = "";
            while (value.bytesAvailable != 0) { var next:String = value.readUnsignedByte().toString(16); result += next.length == 1 ? "0" + next : next; }
            value.position = position; return result;
        }
        private function wire(value:*, encoding:uint, expected:String):void {
            var out:ByteArray = new ByteArray(); out.objectEncoding = encoding; out.endian = "littleEndian"; out.writeObject(value);
            check(hex(out) == expected && out.endian == "littleEndian" && out.position == out.length, "wire-" + encoding + "-" + expected);
            out.position = 0; var decoded:* = out.readObject();
            check(typeof decoded == typeof value && (decoded === value || (typeof value == "number" && isNaN(value) && isNaN(decoded))) && out.position == out.length, "primitive-read-" + expected);
        }
        private function defaults():void {
            var before:ByteArray = new ByteArray(); check(before.objectEncoding == 3 && ByteArray.defaultObjectEncoding == ObjectEncoding.DEFAULT, "default");
            ByteArray.defaultObjectEncoding = ObjectEncoding.AMF0; var after:ByteArray = new ByteArray();
            check(before.objectEncoding == 3 && after.objectEncoding == 0, "constructor-default-snapshot");
            ByteArray.defaultObjectEncoding = 3; check(after.objectEncoding == 0, "existing-encoding-retained");
            var caught:Boolean; try { after.objectEncoding = 1; } catch (invalid:ArgumentError) { caught = true; }
            check(caught && after.objectEncoding == 0, "invalid-objectEncoding");
            caught = false; try { ByteArray.defaultObjectEncoding = 2; } catch (badDefault:ArgumentError) { caught = true; }
            check(caught && ByteArray.defaultObjectEncoding == 3, "invalid-defaultEncoding");
        }
        private function primitives():void {
            wire(undefined,3,"00"); wire(null,3,"01"); wire(false,3,"02"); wire(true,3,"03");
            wire(0,3,"0400"); wire(-1,3,"04ffffffff"); wire(128,3,"048100"); wire(16384,3,"04818000"); wire(2097152,3,"0480c08000");
            wire(268435455,3,"04bfffffff"); wire(-268435456,3,"04c0808000"); wire(268435456,3,"0541b0000000000000");
            wire(1.5,3,"053ff8000000000000"); wire("",3,"0601"); wire("🐕",3,"0609f09f9095"); wire("a\u0000b",3,"0607610062");
            wire(undefined,0,"06"); wire(null,0,"05"); wire(true,0,"0101"); wire(false,0,"0100");
            wire(1.5,0,"003ff8000000000000"); wire("🐕",0,"020004f09f9095"); wire("a\u0000b",0,"020003610062");
            for each (var encoding:uint in [0,3]) {
                var out:ByteArray = new ByteArray(); out.objectEncoding = encoding; out.writeObject(-0.0); out.position = 0;
                check(1 / out.readObject() == -Infinity, "negative-zero");
                out.length = 0; out.writeObject(NaN); out.writeObject(Infinity); out.position = 0;
                check(isNaN(out.readObject()) && out.readObject() == Infinity, "nan-infinity");
            }
            var array:ByteArray = new ByteArray(); array.writeObject(["x","x"]); check(hex(array) == "0905010603780600", "string-reference-wire");
            var cycle:Array = []; cycle.push(cycle); array.length = 0; array.writeObject(cycle);
            check(hex(array) == "0903010900","array-cycle-wire");
            var self:Object = {}; self.self = self; array.length = 0; array.writeObject(self);
            check(hex(array) == "0a0b010973656c660a0001","object-cycle-wire");
            var known:* = bytes("0905010a130358037804010a010402").readObject();
            check(known[0].x == 1 && known[1].x == 2 && known[0] !== known[1],"external-trait-reference-vector");
        }
        private function roundtrip(value:*, encoding:uint):* { var out:ByteArray = new ByteArray(); out.objectEncoding = encoding; out.writeObject(value); out.position = 0; return out.readObject(); }
        private function graphs(encoding:uint):void {
            var shared:Object = {text:"hello"}; var root:Object = {left:shared,right:shared}; root.self = root;
            var result:* = roundtrip(root, encoding); check(result.self === result && result.left === result.right && result.left.text == "hello", "object-cycle-" + encoding);
            var array:Array = []; array.push(array); result = roundtrip(array, encoding); check(result[0] === result, "array-cycle-" + encoding);
            array = [1,2]; array["caption"] = "items"; array["owner"] = array;
            result = roundtrip(array, encoding); check(result is Array && result.length == 2 && result[1] == 2 && result.caption == "items" && result.owner === result, "associative-" + encoding + ":" + result.length + "/" + result[1] + "/" + result.caption + "/" + (result.owner === result));
            var buffer:ByteArray = bytes("010203"); buffer.position = 2;
            result = roundtrip([buffer,buffer,new ByteArray()],encoding);
            check(result[0] is ByteArray && result[0] === result[1] && hex(result[0]) == "010203" && result[2].length == 0 && buffer.position == 2, "bytearrays-" + encoding);
            var date:Date = new Date(123456); result = roundtrip([date,date,shared,shared],encoding);
            check(result[0] is Date && result[0].getTime() == 123456 && result[2] === result[3] && (encoding == 0 || result[0] === result[1]), "date-reference-table-" + encoding);
            var longText:String = ""; for (var i:int = 0; i < 17000; i++) longText += "🐕";
            check(roundtrip(longText,encoding) == longText, "long-utf-" + encoding);
            retained = result;
        }
        private function typed(encoding:uint):void {
            registerClassAlias("test.Record",AmfRecord); check(getClassByAlias("test.Record") === AmfRecord,"alias-lookup");
            var source:AmfRecord = new AmfRecord(); source.base = 42; source.title = "typed"; source.amount = 123; source.scratch = 100; source.extra = "dynamic"; source.link = source;
            var count:uint = AmfRecord.created; var result:* = roundtrip([source,new AmfRecord(),source],encoding);
            check(result[0] is AmfRecord && result[0] === result[2] && result[0].link === result[0], "typed-cycle-" + encoding);
            check(result[0].base == 42 && result[0].title == "typed" && result[0].amount == 123 && result[0].extra == "dynamic", "typed-members-" + encoding);
            check(result[0].scratch == 9 && AmfRecord.created == count + 3, "transient-constructors-" + encoding);
            var plain:AmfSealed = new AmfSealed(); plain.value = 77; result = roundtrip(plain,encoding);
            check(!(result is AmfSealed) && result.value == 77 && !result.hasOwnProperty("constant"), "unregistered-anonymous-" + encoding);
        }
        private function reject(text:String, encoding:uint = 3):void {
            var input:ByteArray = bytes(text,encoding); var caught:Boolean;
            try { input.readObject(); } catch (error:Error) { caught = true; }
            check(caught,"reject-" + text);
        }
        private function malformed():void {
            registerClassAlias("replace",AmfRecord); registerClassAlias("replace",AmfSealed); check(getClassByAlias("replace") === AmfSealed,"replace-alias");
            reject("ff"); reject("0902"); reject("0600"); reject("0a01"); reject("06096162"); reject("09ffffffff"); reject("07",0);
            reject("0903010c00"); reject("09010d6c656e677468040101");
            var input:ByteArray = new ByteArray(); var caught:Boolean;
            try { input.readObject(); } catch (eof:EOFError) { caught = true; } check(caught && input.position == 0,"empty-eof");
            input = bytes("060761"); caught = false; try { input.readObject(); } catch (shortText:EOFError) { caught = true; }
            check(caught,"truncated-eof");
            input = bytes("0603780600"); check(input.readObject() == "x","first-read"); caught = false;
            try { input.readObject(); } catch (cross:Error) { caught = true; } check(caught,"reference-reset");
            var bad:Object = {fn:function():void {}}; input = bytes("010203"); input.position = 1; caught = false;
            try { input.writeObject(bad); } catch (unsupported:TypeError) { caught = true; }
            check(caught && input.position == 1 && hex(input) == "010203","failed-write-atomic");
            var deep:Object = {}; var node:Object = deep; for (var i:int = 0; i < 70; i++) { node.next = {}; node = node.next; }
            caught = false; try { input.writeObject(deep); } catch (depth:RangeError) { caught = true; } check(caught,"depth-limit");
            caught = false; try { getClassByAlias("missing"); } catch (missing:ReferenceError) { caught = true; } check(caught,"missing-alias");
            caught = false; try { registerClassAlias(null,AmfRecord); } catch (nullAlias:TypeError) { caught = true; } check(caught,"null-alias");
        }
        private function childLoaded(event:Event):void {
            check(LoaderInfo(event.target).content["passed"],"loaded-import-schema-and-alias");
            loaded++; if (loaded == 2) { check(getClassByAlias("test.Record") === AmfRecord,"child-alias-isolation"); complete = true; }
        }
        private function advanced():void {
            registerClassAlias("test.External", AmfExternal);
            var item:AmfExternal = new AmfExternal(); item.value = -123456; item.link = item; item.payload = {label:"shared"};
            var result:* = roundtrip([item, item.payload, item, new AmfExternal(), 1.5], 3);
            var direct:ByteArray = new ByteArray(); check(direct is IDataInput && direct is IDataOutput,"bytearray-data-interfaces");
            IDataOutput(direct).writeObject({name:"interface"}); direct.position = 0; check(IDataInput(direct).readObject().name == "interface","interface-object-methods");
            check(result[0] is AmfExternal && result[0].link === result[0] && result[0] === result[2], "external-cycle");
            check(result[0].payload === result[1] && result[0].value == -123456 && result[0].text == "custom", "external-private-and-shared");
            check(item.writeCalls == 1 && result[0].readCalls == 1 && result[3].readCalls == 1 && result[4] == 1.5,"external-trait-and-endian");
            var caught:Boolean; try { item.retainedOutput.writeByte(0); } catch (closedOutput:Error) { caught = true; } check(caught,"external-output-closed");
            caught = false; try { result[0].retainedInput.readByte(); } catch (closedInput:Error) { caught = true; } check(caught,"external-input-closed");
            var bad:AmfExternal = new AmfExternal(); bad.failWrite = true;
            var destination:ByteArray = bytes("010203"); destination.position = 1; caught = false;
            try { destination.writeObject(bad); } catch (writeFailure:Error) { caught = true; }
            check(caught && destination.position == 1 && hex(destination) == "010203","external-failed-write-atomic");
            bad.failWrite = false; bad.failRead = true; destination.length = 0; destination.writeObject(bad); destination.position = 0; caught = false;
            try { destination.readObject(); } catch (readFailure:Error) { caught = true; } check(caught && destination.position > 0,"external-failed-read-position");
            result = roundtrip([item,item],0); check(result[0] is AmfExternal && result[0] === result[1] && result[0].link === result[0],"amf0-external-escape");
            var dictionary:Dictionary = new Dictionary(); var key:Object = {name:"object-key"}; dictionary[key] = dictionary; dictionary["name"] = key;
            result = roundtrip([dictionary,key,dictionary],3);
            check(result[0] is Dictionary && result[0] === result[2] && result[0][result[1]] === result[0] && result[0]["name"] === result[1],"dictionary-cycle-and-object-key");
            var out:ByteArray = new ByteArray(); out.writeObject(new Dictionary(true)); check(hex(out) == "110101","weak-dictionary-wire");
            result = bytes("110101").readObject(); out.length = 0; out.writeObject(result); check(hex(out) == "110101","weak-dictionary-roundtrip");
            reject("0d0300"); reject("0e0300"); reject("0f0300"); reject("10030001"); reject("11030001"); reject("0b093c612f");
            reject("0a070f6d697373696e67"); reject("0903010d00");
            retainedAdvanced = [roundtrip(item,3),roundtrip(dictionary,3),roundtrip(new <int>[1,2,3],3),result];
            vectors();
            xmlValues();
        }
        private function vectors():void {
            var signed:Vector.<int> = new <int>[-2147483648,-1,2147483647];
            var unsigned:Vector.<uint> = new <uint>[0,2147483648,4294967295];
            var numbers:Vector.<Number> = new <Number>[1.5,-0.0,NaN,Infinity];
            var result:* = roundtrip([signed,unsigned,numbers,signed],3);
            check(result[0] is Vector.<int> && result[0][0] == -2147483648 && result[0][2] == 2147483647 && result[0] === result[3],"vector-int");
            check(result[1] is Vector.<uint> && result[1][1] == 2147483648 && result[1][2] == 4294967295,"vector-uint");
            check(result[2] is Vector.<Number> && result[2][0] == 1.5 && 1 / result[2][1] == -Infinity && isNaN(result[2][2]) && result[2][3] == Infinity,"vector-double");
            var out:ByteArray = new ByteArray(); out.writeObject(new <int>[1,-1]); check(hex(out) == "0d050000000001ffffffff","vector-int-wire");
            out.length = 0; out.writeObject(new Vector.<uint>(0,true)); check(hex(out) == "0e0101","fixed-vector-wire");
            result = roundtrip(new Vector.<Number>(2,true),3); check(result.fixed && result.length == 2,"fixed-vector-roundtrip");
            var objects:Vector.<Object> = new Vector.<Object>(2); objects[0] = objects; objects[1] = {label:"member"};
            result = roundtrip([objects,objects[1]],3); check(result[0] is Vector.<Object> && result[0][0] === result[0] && result[0][1] === result[1],"object-vector-cycle");
            var any:Vector.<*> = new Vector.<*>(4); any[0] = any; any[1] = undefined; any[2] = "text"; any[3] = null;
            result = roundtrip(any,3); check(result is Vector.<*> && result[0] === result && result[1] === undefined && result[2] == "text" && result[3] === null,"any-vector-cycle");
            var records:Vector.<AmfRecord> = new Vector.<AmfRecord>(2); records[0] = new AmfRecord(); records[0].title = "vector"; records[1] = records[0];
            result = roundtrip(records,3); check(result is Vector.<AmfRecord> && result[0] === result[1] && result[0].title == "vector","aliased-vector");
            var strings:Vector.<String> = new <String>["one","two",null]; result = roundtrip(strings,3);
            check(result is Vector.<String> && result[1] == "two" && result[2] === null,"string-vector");
            var nested:* = new Vector.<Vector.<int>>(1); nested[0] = signed; result = roundtrip(nested,3);
            check(result is Vector.<Vector.<int>> && result[0][1] == -1,"nested-vector");
            result = roundtrip([signed,signed],0); check(result[0] is Vector.<int> && result[0] === result[1],"amf0-vector-escape");

        }
        private function xmlValues():void {
            XML.prettyPrinting = false; XML.ignoreComments = false; XML.ignoreProcessingInstructions = false; XML.ignoreWhitespace = false;
            var source:String = '<root xmlns="urn:test"><child attr="&amp;">🐕</child><!--note--></root>';
            var xml:XML = new XML(source); var document:XMLDocument = new XMLDocument(source);
            var result:* = roundtrip([xml,xml,document,document],3);
            check(result[0] is XML && result[0] === result[1] && result[0].toXMLString().indexOf("<!--note-->") >= 0 && result[0].toXMLString().indexOf("🐕") >= 0,"xml-reference-and-utf");
            check(result[2] is XMLDocument && result[2] === result[3] && result[2].toString().indexOf("urn:test") >= 0,"xml-document-reference");
            check(new XML("<a>hello &amp; world</a>").toString() == "hello & world","xml-simple-content");
            var out:ByteArray = new ByteArray(); out.writeObject(new XML("<a/>")); check(hex(out) == "0b093c612f3e","xml-wire");
            out.length = 0; out.writeObject(new XMLDocument("<b/>")); check(hex(out) == "070b3c62202f3e","xml-document-wire");
            result = roundtrip([document,{x:1},{x:2}],0); check(result[0] is XMLDocument && result[1].x == 1 && result[2].x == 2,"amf0-xml-document");
            result = roundtrip(xml,0); check(result is XMLDocument && result.toString().indexOf("urn:test") >= 0,"amf0-xml-type");
            retainedAdvanced.push(roundtrip(xml,3));
            var caught:Boolean; try { new XML("<a>"); } catch (invalid:TypeError) { caught = true; } check(caught,"malformed-xml");
            caught = false; try { new XMLDocument('<!DOCTYPE a SYSTEM "file:///missing"><a/>'); } catch (dtd:TypeError) { caught = true; } check(caught,"xml-dtd-rejected");
            var deep:String = ""; for (var i:int = 0; i < 130; i++) deep += "<a>"; for (i = 0; i < 130; i++) deep += "</a>";
            caught = false; try { new XML(deep); } catch (depth:TypeError) { caught = true; } check(caught,"xml-depth-limit");
            XML.prettyPrinting = true; XML.ignoreComments = true; XML.ignoreProcessingInstructions = true; XML.ignoreWhitespace = true;
        }
        private function childFailed(event:IOErrorEvent):void { failures++; failureList += "child-load"; complete = true; }
        public function afterGC():void {
            check(retained[2] === retained[3] && retained[0].getTime() == 123456,"graph-after-gc");
            check(retainedAdvanced[0].link === retainedAdvanced[0] && retainedAdvanced[2][2] == 3,"external-vector-after-gc");
            check(retainedAdvanced[4].toXMLString().indexOf("🐕") >= 0,"xml-after-gc");
            var out:ByteArray = new ByteArray(); out.writeObject(retainedAdvanced[3]); check(hex(out) == "110101","weak-dictionary-after-gc");
        }
    }
}
