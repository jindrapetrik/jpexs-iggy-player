package {
    import flash.display.MovieClip;
    import flash.display.Loader;
    import flash.net.URLLoader;
    import flash.net.URLLoaderDataFormat;
    import flash.net.URLRequest;
    import flash.net.URLVariables;
    import flash.events.Event;
    import flash.events.ProgressEvent;
    import flash.events.IOErrorEvent;
    import flash.events.SecurityErrorEvent;
    import flash.utils.ByteArray;
    import flash.utils.getQualifiedClassName;
    import flash.utils.getDefinitionByName;
    public class DataMain extends MovieClip {
        public var completed:uint;
        public var errors:uint;
        public var security:uint;
        public var failures:uint;
        public var checks:uint;
        public var details:String = "";
        public var doneLabels:String = "";
        public var cancelledEvents:uint;
        public var replaced:uint;
        public var reloaded:uint;
        public var changed:uint;
        public var variablesCompleted:uint;
        public var variablesErrors:uint;
        public var limitUrl:String;
        public var child:Loader;
        private var retained:Array = [];
        public function DataMain() {
            super(); stop();
            var fresh:URLLoader = new URLLoader(); check(fresh.dataFormat == "text" && fresh.bytesLoaded == 0 && fresh.bytesTotal == 0,"defaults");
            check(getQualifiedClassName(URLLoader)=="flash.net::URLLoader" && getDefinitionByName("flash.net.URLLoader")===URLLoader,"reflection");
            var caught:Boolean = false; try { fresh.load(null); } catch (nullError:TypeError) { caught = true; } check(caught,"null-request");
            caught = false; try { fresh.close(); } catch (closeError:Error) { caught = true; } check(caught,"inactive-close");
            fresh.dataFormat = "invalid";
            caught = false; try { fresh.load(new URLRequest("url-data/utf8.txt")); } catch (formatError:ArgumentError) { caught = true; } check(caught,"unsupported-format");
            fresh.dataFormat = URLLoaderDataFormat.TEXT; var post:URLRequest = new URLRequest("url-data/utf8.txt"); post.method = "POST";
            caught = false; try { fresh.load(post); } catch (methodError:ArgumentError) { caught = true; } check(caught,"unsupported-method");
            watch("text","url-data/utf8.txt",false,22);
            watch("ctor","url-data/utf8.txt",false,22,true);
            watch("binary","url-data/bytes.bin",true,2097152);
            watch("empty-text","url-data/empty",false,0);
            watch("empty-binary","url-data/empty",true,0);
            watch("utf16","url-data/utf16.txt",false,34);
            watch("unicode","url-data/%C5%BElu%C5%A5ou%C4%8Dk%C3%BD%F0%9F%99%82.txt?ignored=1#anchor",false,7);
            watch("large","url-data/large.txt",false,2097152);
            watch("raw","url-data/raw.swf",true,4);
            testVariables();
            watchError("url-data/missing",false);
            watchError("https://example.invalid/data",true);
            var cancel:URLLoader = new URLLoader(); retained.push(cancel); cancel.addEventListener(Event.COMPLETE,unexpected);cancel.addEventListener(IOErrorEvent.IO_ERROR,unexpected);
            cancel.load(new URLRequest("url-data/missing")); cancel.close();
            var open:URLLoader = new URLLoader(); retained.push(open);open.addEventListener(Event.OPEN,function(event:Event):void {open.close();});open.addEventListener(Event.COMPLETE,function(event:Event):void {cancelledEvents++;details+="open-complete-"+event.target["bytesLoaded"]+",";});open.load(new URLRequest("url-data/bytes.bin"));
            var progress:URLLoader = new URLLoader();retained.push(progress);progress.addEventListener(ProgressEvent.PROGRESS,function(event:ProgressEvent):void {progress.close();});progress.addEventListener(Event.COMPLETE,function(event:Event):void {cancelledEvents++;details+="progress-complete-"+event.target["bytesLoaded"]+",";});progress.load(new URLRequest("url-data/bytes.bin"));
            var replace:URLLoader = new URLLoader();retained.push(replace);replace.addEventListener(IOErrorEvent.IO_ERROR,function(event:Event):void {cancelledEvents++;details+="replace-error,";});replace.addEventListener(Event.COMPLETE,function(event:Event):void {replaced++;check(replace.data == "","replace-result");});replace.load(new URLRequest("url-data/missing"));replace.load(new URLRequest("url-data/empty"));
            var reload:URLLoader = new URLLoader();retained.push(reload);var switched:Boolean;
            reload.addEventListener(ProgressEvent.PROGRESS,function(event:ProgressEvent):void {if(!switched){switched=true;reload.load(new URLRequest("url-data/empty"));}});
            reload.addEventListener(Event.COMPLETE,function(event:Event):void {reloaded++;check(reload.data == "","progress-reload");});reload.load(new URLRequest("url-data/bytes.bin"));
            var format:URLLoader = new URLLoader();retained.push(format);format.addEventListener(ProgressEvent.PROGRESS,function(event:ProgressEvent):void {format.dataFormat="binary";});format.addEventListener(Event.COMPLETE,function(event:Event):void {changed++;check(format.data is ByteArray && format.data.length==22,"format-at-complete");});format.load(new URLRequest("url-data/utf8.txt"));
            check(completed == 0 && errors == 0 && security == 0,"asynchronous-return");
        }
        private function testVariables():void {
            var values:URLVariables = new URLVariables("name=Jindra+Pet%C5%99%C3%ADk&name=JPEXS&empty=&equal=a=b&%C5%BE=%F0%9F%99%82&plus=%2B+%2b&=blank");
            check(values.name is Array && values.name.length == 2 && values.name[0] == "Jindra Petřík" && values.name[1] == "JPEXS", "variables-duplicates");
            check(values.empty === "" && values.equal == "a=b" && values["ž"] == "🙂" && values.plus == "+ +" && values[""] == "blank", "variables-escaping");
            values.decode("name=third&more=hello+wide+world");
            check(values.name.length == 3 && values.name[2] == "third" && values.more == "hello wide world", "variables-decode-appends");
            var encoded:URLVariables = new URLVariables(); encoded["ž"] = "🙂 +&=%";
            check(encoded.toString() == "%C5%BE=%F0%9F%99%82%20%2B%26%3D%25", "variables-encode-utf8");
            encoded = new URLVariables(); encoded.symbols = "-_.!~*'()@/";
            check(encoded.toString() == "symbols=%2D%5F%2E%21%7E%2A%27%28%29%40%2F", "variables-encode-punctuation");
            encoded = new URLVariables(); encoded.repeat = ["one", "two +", "ž"];
            check(encoded.toString() == "repeat=one&repeat=two%20%2B&repeat=%C5%BE", "variables-encode-array");
            var roundtrip:URLVariables = new URLVariables(values.toString());
            check(roundtrip.name is Array && roundtrip.name.length == 3 && roundtrip["ž"] == "🙂" && roundtrip.plus == "+ +", "variables-roundtrip");
            encoded = new URLVariables(); encoded.number = 42; encoded.flag = false; encoded.nothing = null;
            roundtrip = new URLVariables(encoded.toString());
            check(roundtrip.number == "42" && roundtrip.flag == "false" && roundtrip.nothing == "null", "variables-convert-values");
            check(new URLVariables().toString() == "" && new URLVariables("").toString() == "", "variables-empty");
            var caught:Boolean = false; try { new URLVariables("missing-value"); } catch (error:Error) { caught = true; } check(caught, "variables-invalid-pair");
            check(getQualifiedClassName(URLVariables) == "flash.net::URLVariables" && getQualifiedClassName(values) == "flash.net::URLVariables" && getDefinitionByName("flash.net.URLVariables") === URLVariables, "variables-reflection");
            watchVariables("url-data/form.txt", false, false);
            watchVariables("url-data/empty", true, false);
            watchVariables("url-data/form.txt", false, true);
            var invalid:URLLoader = new URLLoader(); retained.push(invalid); invalid.dataFormat = URLLoaderDataFormat.VARIABLES;
            invalid.addEventListener(Event.COMPLETE, unexpected);
            invalid.addEventListener(IOErrorEvent.IO_ERROR, function(event:IOErrorEvent):void {variablesErrors++;check(event.text.length > 0 && invalid.data == null, "variables-invalid-response");});
            invalid.load(new URLRequest("url-data/invalid-form.txt"));
        }
        private function watchVariables(url:String, empty:Boolean, change:Boolean):void {
            var loader:URLLoader = new URLLoader(); retained.push(loader);
            loader.dataFormat = change ? URLLoaderDataFormat.TEXT : URLLoaderDataFormat.VARIABLES;
            var opened:Boolean; var progressed:Boolean;
            loader.addEventListener(Event.OPEN, function(event:Event):void {opened = true;});
            loader.addEventListener(ProgressEvent.PROGRESS, function(event:Event):void {progressed = true;if(change)loader.dataFormat = URLLoaderDataFormat.VARIABLES;});
            loader.addEventListener(IOErrorEvent.IO_ERROR, unexpected);
            loader.addEventListener(Event.COMPLETE, function(event:Event):void {
                variablesCompleted++;
                var result:URLVariables = loader.data as URLVariables;
                check(opened && progressed && result != null && loader.bytesLoaded == loader.bytesTotal, "variables-load-events");
                if(empty)check(result.toString() == "" && loader.bytesTotal == 0, "variables-load-empty");
                else check(result.name is Array && result.name[0] == "Jindra Petřík" && result.name[1] == "JPEXS" && result["ž"] == "🙂" && result.plus == "+ +", "variables-load-payload");
            });
            loader.load(new URLRequest(url));
        }
        private function check(value:Boolean,label:String):void {checks++;if(!value){failures++;details+=label+",";}}
        private function unexpected(event:Event):void {cancelledEvents++;details+="cancel-"+retained.indexOf(event.target)+":"+event.type+",";}
        private function watch(label:String,url:String,binary:Boolean,count:uint,constructor:Boolean=false):void {
            var loader:URLLoader = constructor ? new URLLoader(new URLRequest(url)) : new URLLoader();retained.push(loader);
            if(binary)loader.dataFormat=URLLoaderDataFormat.BINARY;
            var opened:Boolean;var last:Number=-1;var progress:Boolean;
            loader.addEventListener(Event.OPEN,function(event:Event):void {check(!opened && event.target === loader && loader.data == null,"open-"+label);opened=true;});
            loader.addEventListener(ProgressEvent.PROGRESS,function(event:ProgressEvent):void {
                var copy:ProgressEvent=event.clone() as ProgressEvent;
                check(opened && event.target===loader && event.bytesLoaded>=last && event.bytesLoaded<=event.bytesTotal && copy.bytesLoaded==event.bytesLoaded && loader.bytesLoaded==event.bytesLoaded,"progress-"+label);last=event.bytesLoaded;progress=true;
            });
            loader.addEventListener(IOErrorEvent.IO_ERROR,function(event:IOErrorEvent):void {failures++;details+="unexpected-io-"+label+":"+event.text+",";});
            loader.addEventListener(Event.COMPLETE,function(event:Event):void {
                doneLabels+=label+",";completed++;check(opened && progress && event.target===loader && loader.bytesLoaded==count && loader.bytesTotal==count && last==count,"complete-"+label);
                if(binary) {
                    var bytes:ByteArray=loader.data as ByteArray;check(bytes!=null && bytes.length==count && bytes.position==0,"binary-"+label);
                    if(label=="binary"){check(bytes.readUnsignedInt()==0x00010203,"binary-start");bytes.position=bytes.length-4;check(bytes.readUnsignedInt()==0xfcfdfeff,"binary-end");}
                    if(label=="raw")check(bytes.readUnsignedInt()==0x46575308,"raw-no-iggy-substitution");
                } else if(label=="large")check(loader.data.length==2097152 && loader.data.charAt(0)=="A" && loader.data.charAt(2097151)=="A","large-direct-string");
                else check(loader.data==((label=="empty-text")?"":label=="unicode"?"unicode":"žluťoučký🙂\u0000tail"),"text-"+label);
            });
            if(!constructor)loader.load(new URLRequest(url));
        }
        private function watchError(url:String,denied:Boolean):void {
            var loader:URLLoader=new URLLoader();retained.push(loader);loader.addEventListener(Event.COMPLETE,unexpected);
            if(denied)loader.addEventListener(SecurityErrorEvent.SECURITY_ERROR,function(event:SecurityErrorEvent):void {security++;check(event.text.length>0 && event.target===loader && event.clone() is SecurityErrorEvent,"security-event");});
            else loader.addEventListener(IOErrorEvent.IO_ERROR,function(event:IOErrorEvent):void {errors++;check(event.text.length>0 && event.target===loader && event.clone() is IOErrorEvent,"io-event");});
            loader.load(new URLRequest(url));
        }
        public function startLimit():void {watchError(limitUrl,false);}
        public function loadChild():void {child=new Loader();addChild(child);child.load(new URLRequest("url-data/sub/child.swf"));}
        public function get childDone():Boolean {return child!=null && child.content!=null && child.content["done"];}
        public function get childPassed():Boolean {return childDone && child.content["passed"];}
        public function stopChild():void {child.content["startPending"]();child.unloadAndStop(true);}
        public function release():void {retained=[];if(child!=null){child.unloadAndStop(true);child=null;}}
    }
}
