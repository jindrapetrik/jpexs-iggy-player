using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace IggyViewer {
    // An immutable download snapshot handed to the linker on the movie thread.
    // File names are identities only; no network resource is written to disk.
    internal sealed class MovieResources {
        internal sealed class Movie {
            internal string FileName,Url,Key;
            internal byte[] Bytes,Textures;
            internal readonly Dictionary<string,string> Imports=new Dictionary<string,string>(StringComparer.Ordinal);
        }
        internal readonly Dictionary<string,Movie> Movies=new Dictionary<string,Movie>(StringComparer.OrdinalIgnoreCase);
        internal static string FileName(string directory,string url) {
            using(SHA256 sha=SHA256.Create())return Path.Combine(directory,"__iggy_remote_"+BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(url))).Replace("-","")+".iggy");
        }
        internal string Resolve(string from,string url) {
            string file;if(!Movies[from].Imports.TryGetValue(url,out file))throw new IOException("Missing downloaded Iggy import: "+url);
            return file;
        }
        internal static void Seal(Movie movie,MovieResources resources) {
            using(SHA256 sha=SHA256.Create()) {
                StringBuilder key=new StringBuilder(movie.Url);key.Append('|').Append(Convert.ToBase64String(sha.ComputeHash(movie.Bytes)));
                if(movie.Textures!=null)key.Append('|').Append(Convert.ToBase64String(sha.ComputeHash(movie.Textures)));
                foreach(KeyValuePair<string,string> import in movie.Imports)key.Append('|').Append(import.Key).Append('|').Append(resources.Movies[import.Value].Key);
                movie.Key=Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(key.ToString())));
            }
        }
    }
}
