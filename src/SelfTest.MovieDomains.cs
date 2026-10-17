using System;
using System.IO;
using System.Threading;

namespace IggyViewer {
    internal static partial class SelfTest {
        public static void RunMovieDomains(string dll,string movie,string output,Log log) {
            Directory.CreateDirectory(output);string hash=Hash(File.ReadAllBytes(movie));
            using(Native native=new Native(dll,log)) {
                for(int cycle=0;cycle<2;cycle++) {
                    native.Open(movie,550,400);
                    for(int step=0;step<300&&!KeyFlag(native,"complete");step++){Thread.Sleep(10);Check(native.Frame(true),"Advance application domain fixture");}
                    log.Write("Domain fixture: info="+KeyFlag(native,"initialLoaderInfo")+", definition="+KeyFlag(native,"initialDefinition")+", ready="+ReadValue(native,"ready")+", failures="+ReadValue(native,"failures"));
                    if(ReadValue(native,"failures")!=0)log.Write("Domain failed checks: "+ReadString(native,"failureList"));
                    Check(KeyFlag(native,"initialDomain"),"Main LoaderInfo and currentDomain resolve parent classes");
                    Check(KeyFlag(native,"complete")&&ReadValue(native,"ready")==7&&ReadValue(native,"errors")==0,"Domain movie/image loads complete");
                    Check(ReadValue(native,"failures")==0,"Default, shared, current and isolated domains, constructors, callbacks, parameters and reflection");
                    Check(KeyFlag(native,"blockedOk"),"allowCodeImport rejects movie code and permits images");
                    native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);GC.Collect();GC.WaitForPendingFinalizers();
                    Call(native,"","afterGc");Check(KeyFlag(native,"gcLookup"),"Domain definitions remain valid after native and managed GC");
                    native.Close();
                }
                native.Open(Path.Combine(Path.GetDirectoryName(movie),"domains","root-imports.iggy"),550,400);
                log.Write("Root imports: state="+ReadValue(native,"stateCount")+", nested="+ReadValue(native,"nestedCount")+", owner="+KeyFlag(native,"ownerOk"));
                Check(KeyFlag(native,"rootImports"),"Startup imports are registered in the main domain");
                native.Close();
                native.Open(Path.Combine(Path.GetDirectoryName(movie),"domains","info-only.iggy"),550,400);
                Check(KeyFlag(native,"infoDomain"),"LoaderInfo.applicationDomain alone enables the domain bridge");
                native.Close();
                for(int cycle=0;cycle<2;cycle++) {
                    string linking=Path.Combine(Path.GetDirectoryName(movie),"domains","linking.iggy");
                    native.Open(linking,550,400);
                    for(int step=0;step<300&&!KeyFlag(native,"complete");step++){Thread.Sleep(10);Check(native.Frame(true),"Advance imported domain fixture");}
                    log.Write("Domain linking: ready="+ReadValue(native,"ready")+", errors="+ReadValue(native,"errors")+", failures="+ReadValue(native,"failures")+", checks="+ReadString(native,"failureList"));
                    Check(KeyFlag(native,"complete")&&ReadValue(native,"ready")==6&&ReadValue(native,"errors")==0,"Nested domain imports complete");
                    Check(ReadValue(native,"failures")==0,"Parent-first classes, same-domain first definition, imported statics, method domains and timeline assets");
                    native.Bind<Native.VoidPI>("IggyPlayerGarbageCollect")(native.Player,100);GC.Collect();GC.WaitForPendingFinalizers();
                    Call(native,"","afterGc");Check(KeyFlag(native,"gcLookup"),"Imported domain classes survive GC");
                    using(System.Drawing.Bitmap image=native.Renderer.Snapshot()) {
                        image.Save(Path.Combine(output,"linked-"+cycle+".png"));
                        for(int tile=0;tile<6;tile++) {
                            System.Drawing.Color pixel=image.GetPixel(70+60*tile,10);
                            Check(tile==1?pixel.R>200&&pixel.B<30:pixel.B>200&&pixel.R<30,"Imported timeline pixels and first resource binding "+tile);
                        }
                    }
                    native.Close();
                }
            }
            Check(Hash(File.ReadAllBytes(movie))==hash,"Domain source remains unchanged");
            foreach(string message in log.Messages)Check(message.IndexOf("Uncaught exception",StringComparison.Ordinal)<0,"No application domain AS3 exceptions");
            log.Write("PASS: LoaderContext domains/parameters/allowCodeImport, isolated nested imports, parent-first and first-definition class binding, statics, timeline assets, reflection, callback domains, GC and reopen.");
        }
    }
}
