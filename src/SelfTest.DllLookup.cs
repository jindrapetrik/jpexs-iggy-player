using System;
using System.IO;

namespace IggyViewer {
    internal static partial class SelfTest {
        static void CheckDllLookup(string output) {
            string directory=Path.GetFullPath(Path.Combine(output,"dll-lookup-"+Guid.NewGuid().ToString("N")));
            string lib=Path.Combine(directory,"lib");Directory.CreateDirectory(lib);
            string preferred=Path.Combine(lib,"iggy_w64_final.dll"),fallback=Path.Combine(lib,"iggy_w64.dll");
            // Lookup checks names only; these are not loaded as libraries.
            File.WriteAllBytes(fallback,new byte[0]);
            Check(Program.FindDll(directory)==fallback,"DLL lookup uses lib/iggy_w64.dll when the final DLL is absent");
            File.WriteAllBytes(preferred,new byte[0]);
            Check(Program.FindDll(directory)==preferred,"DLL lookup prefers lib/iggy_w64_final.dll when both exist");
            File.Delete(preferred);File.Delete(fallback);
            File.WriteAllBytes(Path.Combine(directory,"iggy_w64.dll"),new byte[0]);
            bool missing=false;
            try {Program.FindDll(directory);}catch(FileNotFoundException){missing=true;}
            Check(missing,"DLL lookup requires lib and reports missing libraries");
        }
    }
}
