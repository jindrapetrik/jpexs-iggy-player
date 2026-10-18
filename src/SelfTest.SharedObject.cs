using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace IggyViewer {
    internal static partial class SelfTest {
        static string StoreHash(string text) {using(SHA256 sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").Substring(0,32);}
        static string StoreFile(string directory,string movie,string name) {
            string folder=Path.Combine(directory,StoreHash(Path.GetFullPath(movie).ToUpperInvariant()));Directory.CreateDirectory(folder);
            return Path.Combine(folder,StoreHash("$/|"+name)+".dat");
        }
        static string AmfSave(params byte[] bytes) {return "IGGY2:3:"+Convert.ToBase64String(bytes);}
        static void SeedLegacyStore(string storage,string movie) {
            string word="žluťoučký🙂";
            File.WriteAllText(StoreFile(storage,movie,"legacy"),"IGGY1:O2:S4:wordS"+word.Length+":"+word+"S4:listA3:D1:1UN",new UTF8Encoding(false));
        }
        static Dictionary<string,string> SeedInvalidStores(string storage,string movie) {
            Dictionary<string,string> result=new Dictionary<string,string>();
            string[] names={"corrupt","trailing","array-root","unknown-version","unreadable"};
            string[] contents={"IGGY2:3:!bad!",AmfSave(10,11,1,1,0),AmfSave(9,1,1),"IGGY9:future",""};
            for(int i=0;i<names.Length;i++) {
                string file=StoreFile(storage,movie,names[i]);
                if(i==4)File.WriteAllBytes(file,new byte[]{255,254,255});else File.WriteAllText(file,contents[i],new UTF8Encoding(false));
                result[file]=Hash(File.ReadAllBytes(file));
            }
            return result;
        }
        public static void RunSharedObject(string dll,string movie,string output,Log log,string renderer) {
            Directory.CreateDirectory(output);string source=Hash(File.ReadAllBytes(movie)),storage=Path.Combine(Path.GetFullPath(output),"storage-"+Guid.NewGuid().ToString("N"));
            string locked=Path.Combine(output,"locked-save.dat");File.WriteAllText(locked,"original");
            using(FileStream held=new FileStream(locked,FileMode.Open,FileAccess.Read,FileShare.Read)) {
                Thread release=new Thread(delegate(){Thread.Sleep(100);held.Dispose();});release.IsBackground=true;release.Start();
                try{SharedObjectStorage.SaveText(locked,"updated");}finally{release.Join();}
            }
            Check(File.ReadAllText(locked)=="updated","Transient file locks are retried without losing the save");
            using(FileStream held=new FileStream(locked,FileMode.Open,FileAccess.Read,FileShare.Read)) {
                bool rejected=false;try{SharedObjectStorage.SaveText(locked,"must-not-replace");}catch(IOException){rejected=true;}
                Check(rejected&&File.ReadAllText(locked)=="updated","Persistent locks report failure and preserve the previous save");
            }
            Check(Directory.GetFiles(output,"locked-save.dat.*.tmp").Length==0,"Failed atomic save leaves no temporary file");
            SeedLegacyStore(storage,movie);
            for(int session=0;session<2;session++)using(Native native=new Native(dll,log,renderer:renderer)) {
                native.StorageDirectory=storage;
                for(int cycle=0;cycle<2;cycle++) {
                    Dictionary<string,string> invalid=SeedInvalidStores(storage,movie);
                    native.Open(movie,550,400);
                    log.Write("SharedObject checks="+ReadValue(native,"checks")+", failures="+ReadValue(native,"failures")+", details="+ReadString(native,"failureList"));
                    Check(KeyFlag(native,"complete")&&ReadValue(native,"failures")==0,"SharedObject AMF constructor");
                    Check(ReadValue(native,"before")==session*2+cycle,"SharedObject graph persists across reopen and native shutdown");
                    foreach(var pair in invalid)Check(Hash(File.ReadAllBytes(pair.Key))==pair.Value,"Rejected save is preserved");
                    foreach(string name in new string[]{"graph","legacy","amf0"})Check(File.ReadAllText(StoreFile(storage,movie,name)).StartsWith("IGGY2:"+(name=="amf0"?"0":"3")+":",StringComparison.Ordinal),"New AMF encoding and legacy migration");
                    Check(!File.Exists(StoreFile(storage,movie,"clear")),"Clear removes the save file");
                    string graph=StoreFile(storage,movie,"graph"),before=Hash(File.ReadAllBytes(graph));
                    Call(native,"","failFlush");Check(KeyFlag(native,"failedFlush")&&Hash(File.ReadAllBytes(graph))==before,"Failed serialization preserves the last save");
                    File.WriteAllText(StoreFile(storage,movie,"corrupt"),AmfSave(10,11,1,5,111,107,3,1),new UTF8Encoding(false));
                    Call(native,"","retryCorrupt");Check(KeyFlag(native,"retryPassed"),"Failed reads do not poison the SharedObject cache");
                    Call(native,"",session==0&&cycle==0?"largeWrite":"largeRead");
                    native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);native.Frame(false);Call(native,"","afterGC");
                    Check(ReadValue(native,"failures")==0,"Persisted AMF values survive moving GC");native.Close();
                    Check(native.ByteArrayBufferCount==0,"Shutdown releases SharedObject byte storage");
                }
                if(session==0) {
                    string copy=Path.Combine(Path.GetFullPath(output),"isolated.iggy");File.WriteAllBytes(copy,File.ReadAllBytes(movie));SeedLegacyStore(storage,copy);SeedInvalidStores(storage,copy);
                    native.Open(copy,550,400);Check(KeyFlag(native,"complete")&&ReadValue(native,"before")==0&&ReadValue(native,"failures")==0,"Movie-path isolation");native.Close();
                }
            }
            using(Native native=new Native(dll,log,renderer:renderer)) {
                native.StorageDirectory=storage;string only=Path.Combine(Path.GetDirectoryName(movie),"sharedobject","only.iggy");
                for(int cycle=0;cycle<2;cycle++){native.Open(only,550,400);Check(KeyFlag(native,"passed")&&ReadValue(native,"before")==cycle,"SharedObject-only activation and cyclic persistence");native.Close();}
            }
            Check(Directory.GetFiles(storage,"*.tmp",SearchOption.AllDirectories).Length==0,"Atomic write temporary files are removed");
            foreach(string message in log.Messages)Check(message.IndexOf("Uncaught exception",StringComparison.Ordinal)<0,"No uncaught storage errors");
            Check(Hash(File.ReadAllBytes(movie))==source,"Storage source remains unchanged");
            log.Write("PASS: AMF0/AMF3 SharedObject graphs, aliases, externalizers, binary data, legacy migration, failed-read/write preservation, clear identity, movie isolation, GC and reopen.");
        }
    }
}
