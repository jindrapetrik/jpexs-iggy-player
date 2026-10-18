using System;
using System.IO;
using System.Threading;

namespace IggyViewer {
    internal static partial class SelfTest {
        static void LoadAutoSaveChild(Native native) {
            Call(native,"","loadChild");
            for(int i=0;i<300&&!KeyFlag(native,"loaded");i++){Thread.Sleep(10);native.Frame(true);}
            Check(KeyFlag(native,"loaded")&&KeyFlag(native,"passed"),"Auto-save child loads with its isolated aliases");
        }
        public static void RunSharedObjectAutoSave(string dll,string movie,string output,Log log,string renderer) {
            Directory.CreateDirectory(output);string hash=Hash(File.ReadAllBytes(movie));
            string storage=Path.Combine(Path.GetFullPath(output),"storage-"+Guid.NewGuid().ToString("N"));SeedLegacyStore(storage,movie);
            string legacy=StoreFile(storage,movie,"legacy"),legacyHash=Hash(File.ReadAllBytes(legacy));
            string unchanged=StoreFile(storage,movie,"unchanged");File.WriteAllText(unchanged,AmfSave(10,11,1,1));
            DateTime stamp=new DateTime(2001,2,3,4,5,6,DateTimeKind.Utc);File.SetLastWriteTimeUtc(unchanged,stamp);File.SetLastWriteTimeUtc(legacy,stamp);
            for(int session=0;session<2;session++) {
                Native native=new Native(dll,log,renderer:renderer);native.StorageDirectory=storage;
                try {
                    for(int cycle=0;cycle<2;cycle++) {
                        native.Open(movie,550,400);
                        Check(KeyFlag(native,"passed")&&ReadValue(native,"before")==session*2+cycle,"Auto-saved graphs survive close, reopen and native shutdown");
                        LoadAutoSaveChild(native);Check(ReadValue(native,"childBefore")==session*2+cycle,"Isolated child saves use their own class aliases");
                        Call(native,"","edit");Call(native,"","breakSave");
                        string bad=StoreFile(storage,movie,"bad"),badHash=Hash(File.ReadAllBytes(bad));
                        if(cycle==1)Call(native,"","stopChild");
                        native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);native.Frame(false);
                        if(cycle==0) {
                            // Opening the next movie saves the previous one even
                            // when that next open subsequently fails.
                            if(session==0) {native.Open(Path.Combine(Path.GetDirectoryName(movie),"as3.iggy"),550,400);native.Close();}
                            else {
                                bool failed=false;try{native.Open(Path.Combine(output,"missing.iggy"),550,400);}catch(FileNotFoundException){failed=true;}
                                Check(failed,"Switch to missing movie reports its error");
                            }
                        } else native.Dispose();
                        Check(native.Player==IntPtr.Zero&&native.ByteArrayBufferCount==0,"Auto-save runs before VM teardown and releases buffers");
                        Check(File.Exists(StoreFile(storage,movie,"auto")),"Changes persist without explicit flush");
                        Check(File.ReadAllText(StoreFile(storage,movie,"auto0")).StartsWith("IGGY2:0:",StringComparison.Ordinal),"Automatic save honors AMF0");
                        Check(Hash(File.ReadAllBytes(bad))==badHash,"Failed serialization preserves the old save");
                        Check(File.Exists(StoreFile(storage,movie,"after-bad")),"One failed save does not block later stores");
                        Check(!File.Exists(StoreFile(storage,movie,"clear")),"Clear is not undone on close");
                        Check(File.Exists(StoreFile(storage,movie,"clear-edit")),"Mutations through held data references after clear persist");
                        foreach(string name in new string[]{"empty","transient"})Check(!File.Exists(StoreFile(storage,movie,name)),"Unchanged empty objects do not create files");
                        Check(File.GetLastWriteTimeUtc(unchanged)==stamp&&File.GetLastWriteTimeUtc(legacy)==stamp&&Hash(File.ReadAllBytes(legacy))==legacyHash,"Unchanged and legacy saves are not rewritten");
                    }
                } finally {native.Dispose();}
            }
            using(Native native=new Native(dll,log,renderer:renderer)) {
                native.StorageDirectory=storage;native.Open(movie,550,400);
                Check(KeyFlag(native,"passed")&&ReadValue(native,"before")==4,"Final disposed VM saved its last mutations");
                LoadAutoSaveChild(native);Check(ReadValue(native,"childBefore")==4,"Last stopped child retained typed saved data");native.Close();
            }
            // Disk failure still permits teardown and preserves the original file.
            string blocked=Path.Combine(output,"blocked-"+Guid.NewGuid().ToString("N"));File.WriteAllText(blocked,"preserve");
            string blockedMovie=Path.Combine(output,"blocked.iggy");File.WriteAllBytes(blockedMovie,File.ReadAllBytes(movie));
            using(Native native=new Native(dll,log,renderer:renderer)) {
                native.StorageDirectory=blocked;native.Open(blockedMovie,550,400);Call(native,"","editBlocked");native.Close();
                Check(native.Player==IntPtr.Zero&&native.ByteArrayBufferCount==0,"Disk errors cannot prevent native shutdown");
            }
            Check(File.ReadAllText(blocked)=="preserve","Failed auto-save preserves blocking file");
            bool reported=false;foreach(string message in log.Messages) {
                Check(message.IndexOf("Uncaught exception",StringComparison.Ordinal)<0,"Auto-save errors stay caught");
                reported|=message.IndexOf("SharedObject automatic save failed:",StringComparison.Ordinal)>=0;
            }
            Check(reported,"Automatic save errors are reported in the log");Check(Hash(File.ReadAllBytes(movie))==hash,"Auto-save source remains unchanged");
            Check(Directory.GetFiles(storage,"*.tmp",SearchOption.AllDirectories).Length==0,"No abandoned save temporary files");
            log.Write("PASS: SharedObject automatic save on switch/dispose, nested/binary mutations, isolated and stopped child aliases, AMF0, clear, unchanged/legacy files, GC, write failures and native shutdown.");
        }
    }
}
