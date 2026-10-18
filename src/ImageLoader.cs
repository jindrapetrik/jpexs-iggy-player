using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace IggyViewer {
    // File decoding runs off the movie thread. Only the constructor bridge
    // touches VM objects; its private fifth argument is an opaque job token.
    internal unsafe sealed partial class ImageLoader : IDisposable {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr Builtin(IntPtr context);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr Reference(IntPtr player,IntPtr value,int weak);
        sealed class Pixels {
            internal IntPtr Definition,Raw;
            internal int Size;
            internal readonly List<IntPtr> Owners=new List<IntPtr>();
        }
        sealed class InfoOwner {internal IntPtr Info,Owner;internal string BaseFile;}
        readonly List<InfoOwner> infos=new List<InfoOwner>();
        sealed class MovieTicket {internal IntPtr Request,Info;internal Native.LoadedMovie Movie;internal string Token;}
        sealed class MovieRoot {internal IntPtr Root,Info,Instance;}
        readonly List<MovieTicket> tickets=new List<MovieTicket>();
        readonly List<MovieRoot> roots=new List<MovieRoot>();
        // A private LoaderInfo subclass carries the lookup through the no-op
        // constructor. Its content getter returns an opaque AS3 Class value;
        // the host never treats that value as a native display instance.
        sealed class DefinitionTicket {internal IntPtr Request;internal int Domain;internal string Name;}
        readonly List<DefinitionTicket> definitions=new List<DefinitionTicket>();
        IntPtr loaderMetadata;
        readonly Builtin domainGetter;
        readonly Dictionary<int,IntPtr> domainObjects=new Dictionary<int,IntPtr>();
        MovieTicket constructing;
        sealed class Job {
            internal MovieActivity.Scope Owner;
            internal string Token,Status="pending",Url;
            internal int Width,Height,Bytes,InputBytes;
            internal int[] Pixels;
            internal byte[] MovieBytes;
            internal byte[] SourceBytes;
            internal Native.LoadedMovie Movie;
            internal string BaseFile,Path,ResolvedUrl;
            internal bool Linking,Opened;
            internal bool Cancelled,Uploaded;
            internal int Domain;
            internal bool AllowCode;
        }
        readonly object sync=new object();
        readonly Dictionary<string,Job> jobs=new Dictionary<string,Job>();
        readonly Dictionary<IntPtr,Pixels> bitmaps=new Dictionary<IntPtr,Pixels>();
        readonly Native native;
        readonly Reference reference;
        readonly Native.VoidPP freeReference;
        readonly SemaphoreSlim decoders=new SemaphoreSlim(2);
        readonly string directory;
        readonly Builtin originalConstructor,constructor;
        readonly Builtin originalClone,clone;
        readonly Builtin originalLoaderGetter,loaderGetter;
        readonly Builtin originalInfoConstructor,infoConstructor;
        readonly Builtin originalContent,contentGetter,originalRoot,rootGetter,originalMovieConstructor,movieConstructor,originalSpriteConstructor,spriteConstructor,originalLoaderUrl,loaderUrlGetter;
        readonly IntPtr nullValue;
        readonly Log log;
        bool closed;
        long allocated;
        internal long AllocatedBytes {get{lock(sync)return allocated;}}
        internal ImageLoader(Native native,IntPtr module,bool modern,Log log) {
            this.native=native;this.log=log;directory=Path.GetDirectoryName(Path.GetFullPath(native.FileName));
            nullValue=IntPtr.Add(module,modern?0x103979:0xf3959);
            reference=(player,value,weak)=>native.Activity.HostReference(value,weak);
            freeReference=(player,value)=>native.Activity.FreeHostReference(value);
            IntPtr vm=Marshal.ReadIntPtr(native.Player,modern?10560:10480);
            Dictionary<string,IntPtr> classes=new Dictionary<string,IntPtr>();
            int count=Marshal.ReadInt32(vm,modern?9648:9624);
            if(count<1||count>250)throw new InvalidDataException("Unexpected image class registry.");
            for(int i=0;i<count;i++) {
                IntPtr entry=IntPtr.Add(vm,(modern?3648:3624)+24*i);
                classes[Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(entry))]=Marshal.ReadIntPtr(entry,16);
            }
            native.InitializeMovieDomains(classes);
            native.InstallByteArrays(classes);
            InstallAmfDictionary(classes["Dictionary"]);
            native.InstallAmfVector(classes["Vector"]);
            InstallAmfXml(classes["XML"]);
            // These error subclasses have native stub constructors.
            foreach(string name in new string[]{"ReferenceError","SecurityError","ArgumentError","TypeError","RangeError","EOFError"})if(classes.ContainsKey(name)&&classes.ContainsKey("Error"))
                Marshal.WriteIntPtr(classes[name],56,Marshal.ReadIntPtr(classes["Error"],56));
            // Initialize inherited Event state missing from ProgressEvent.
            Marshal.WriteIntPtr(classes["ProgressEvent"],56,Marshal.ReadIntPtr(classes["Event"],56));
            IntPtr metadata=classes["BitmapData"];
            originalConstructor=(Builtin)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(metadata,56),typeof(Builtin));
            constructor=Construct;
            Marshal.WriteIntPtr(metadata,56,Marshal.GetFunctionPointerForDelegate(constructor));
            clone=Clone;
            IntPtr traits=Marshal.ReadIntPtr(metadata,72),methods=Marshal.ReadIntPtr(traits,64);
            int methodsCount=(ushort)Marshal.ReadInt16(traits,22);
            if(methodsCount>256)throw new InvalidDataException("Unexpected BitmapData method table.");
            for(int i=0;i<methodsCount;i++) {
                IntPtr function=IntPtr.Subtract(Marshal.ReadIntPtr(methods,8*i),1);
                if(function.ToInt64()<65536||Marshal.ReadByte(function)!=16)continue;
                IntPtr text=Marshal.ReadIntPtr(function,8);int length=Marshal.ReadInt32(text,48);
                if(length!=5||Marshal.PtrToStringUni(IntPtr.Add(text,52),length)!="clone")continue;
                originalClone=(Builtin)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(function,16),typeof(Builtin));
                Marshal.WriteIntPtr(function,16,Marshal.GetFunctionPointerForDelegate(clone));break;
            }
            if(originalClone==null)throw new InvalidDataException("Cannot locate BitmapData.clone.");
            loaderGetter=GetLoader;
            traits=Marshal.ReadIntPtr(classes["LoaderInfo"],72);methods=Marshal.ReadIntPtr(traits,72);
            methodsCount=(ushort)Marshal.ReadInt16(traits,26);
            if(methodsCount>256)throw new InvalidDataException("Unexpected LoaderInfo getter table.");
            for(int i=0;i<methodsCount;i++) {
                IntPtr function=Marshal.ReadIntPtr(methods,8*i);if(function.ToInt64()<65536||Marshal.ReadByte(function)!=16)continue;
                IntPtr text=Marshal.ReadIntPtr(function,8);int length=Marshal.ReadInt32(text,48);
                if(length!=6||Marshal.PtrToStringUni(IntPtr.Add(text,52),length)!="loader")continue;
                originalLoaderGetter=(Builtin)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(function,16),typeof(Builtin));
                Marshal.WriteIntPtr(function,16,Marshal.GetFunctionPointerForDelegate(loaderGetter));break;
            }
            if(originalLoaderGetter==null)throw new InvalidDataException("Cannot locate LoaderInfo.loader.");
            loaderMetadata=classes["LoaderInfo"];
            domainGetter=GetApplicationDomain;InstallGetter(loaderMetadata,"applicationDomain",domainGetter);
            // The old ExternalInterface bridge cannot marshal these object
            // arguments. LoaderInfo's native no-op constructor can receive the
            // owner directly from our AS3 subclass, before any script runs.
            metadata=classes["LoaderInfo"];
            originalInfoConstructor=(Builtin)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(metadata,56),typeof(Builtin));
            infoConstructor=ConstructInfo;
            Marshal.WriteIntPtr(metadata,56,Marshal.GetFunctionPointerForDelegate(infoConstructor));
            contentGetter=GetContent;originalContent=InstallGetter(classes["LoaderInfo"],"content",contentGetter);
            rootGetter=GetRoot;originalRoot=InstallGetter(classes["DisplayObject"],"root",rootGetter);
            loaderUrlGetter=GetLoaderUrl;originalLoaderUrl=InstallGetter(classes["LoaderInfo"],"loaderURL",loaderUrlGetter);
            originalMovieConstructor=(Builtin)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(classes["MovieClip"],56),typeof(Builtin));
            movieConstructor=c=>ConstructDisplay(c,originalMovieConstructor);Marshal.WriteIntPtr(classes["MovieClip"],56,Marshal.GetFunctionPointerForDelegate(movieConstructor));
            originalSpriteConstructor=(Builtin)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(classes["Sprite"],56),typeof(Builtin));
            spriteConstructor=c=>ConstructDisplay(c,originalSpriteConstructor);Marshal.WriteIntPtr(classes["Sprite"],56,Marshal.GetFunctionPointerForDelegate(spriteConstructor));
            log.Write("Loader: asynchronous local PNG/JPEG and IGGY movies enabled.");
        }
        static Builtin InstallGetter(IntPtr metadata,string name,Builtin replacement) {
            IntPtr traits=Marshal.ReadIntPtr(metadata,72),getters=Marshal.ReadIntPtr(traits,72);int count=(ushort)Marshal.ReadInt16(traits,26);
            if(count>256)throw new InvalidDataException("Unexpected Loader getter table.");
            for(int i=0;i<count;i++) {
                IntPtr function=Marshal.ReadIntPtr(getters,8*i);if(function.ToInt64()<65536||Marshal.ReadByte(function)!=16)continue;
                IntPtr text=Marshal.ReadIntPtr(function,8);int length=Marshal.ReadInt32(text,48);
                if(length!=name.Length||Marshal.PtrToStringUni(IntPtr.Add(text,52),length)!=name)continue;
                Builtin original=(Builtin)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(function,16),typeof(Builtin));
                Marshal.WriteIntPtr(function,16,Marshal.GetFunctionPointerForDelegate(replacement));return original;
            }
            throw new InvalidDataException("Missing native Loader getter: "+name);
        }
        internal string Call(string operation,string value,string baseUrl=null,string domainText=null,string allowCode=null) {
            lock(sync) {
                if(closed)return "error|Movie closed";
                if(operation.StartsWith("__iggy_image_domain_",StringComparison.Ordinal))return native.MovieDomainCall(operation,value);
                if(operation=="__iggy_image_start") {
                    if(native.Activity!=null&&native.Activity.Current!=null&&native.Activity.Current.Stopped)return "error|The containing movie was unloaded";
                    if(jobs.Count>=32)return "error|Too many pending image loads";
                    int domain;if(!int.TryParse(domainText,out domain)||!native.ValidMovieDomain(domain))return "error|Invalid Loader application domain";
                    Job job=new Job {Token=Guid.NewGuid().ToString("N"),Url=value,BaseFile=baseUrl,Domain=domain,AllowCode=allowCode!="no",Owner=native.Activity==null?null:native.Activity.Current};jobs.Add(job.Token,job);
                    ThreadPool.QueueUserWorkItem(delegate {decoders.Wait();try {Decode(job);}finally {decoders.Release();}});return job.Token;
                }
                Job current;if(!jobs.TryGetValue(value,out current))return "error|Image load was cancelled";
                if(operation=="__iggy_image_opened"){current.Opened=true;return "ok";}
                if(operation=="__iggy_image_poll")return current.Status;
                if(operation=="__iggy_image_finish") {jobs.Remove(value);current.Pixels=null;return current.Uploaded?"ok":"error|Cannot create loaded content";}
                if(operation=="__iggy_image_cancel") {current.Cancelled=true;current.Pixels=null;current.MovieBytes=null;current.SourceBytes=null;jobs.Remove(value);return "ok";}
                return "error|Unknown image operation";
            }
        }
        void Decode(Job job) {
            try {
                byte[] bytes;
                lock(sync){if(closed||job.Cancelled)return;bytes=job.SourceBytes;job.SourceBytes=null;}
                Uri uri;
                int separator=job.Url.IndexOfAny(new char[]{'?','#'});
                string location=separator<0?job.Url:job.Url.Substring(0,separator);
                Uri baseUri;
                if(!Uri.TryCreate(job.BaseFile,UriKind.Absolute,out baseUri)||!baseUri.IsFile)baseUri=new Uri("file:///"+Path.Combine(directory,"movie.iggy").Replace('\\','/'));
                if(!Uri.TryCreate(location,UriKind.Absolute,out uri))uri=new Uri(baseUri,location);
                if(!uri.IsFile||uri.IsUnc)throw new IOException("Only local PNG/JPEG and IGGY files are supported");
                string path=uri.LocalPath;
                if(Path.GetExtension(path).Equals(".swf",StringComparison.OrdinalIgnoreCase)&&File.Exists(Path.ChangeExtension(path,".iggy")))path=Path.ChangeExtension(path,".iggy");
                job.Path=path;job.ResolvedUrl=uri.AbsoluteUri+(separator<0?"":job.Url.Substring(separator));
                if(bytes==null) {
                    using(FileStream input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)) {
                        if(input.Length>256*1024*1024)throw new IOException("Loaded file exceeds 256 MiB");
                        bytes=new byte[(int)input.Length];int offset=0;
                        while(offset<bytes.Length){int size=input.Read(bytes,offset,bytes.Length-offset);if(size==0)throw new EndOfStreamException();offset+=size;}
                    }
                }
                if(bytes.Length>=32&&BitConverter.ToUInt32(bytes,0)==0xed0a6749) {
                    if(!job.AllowCode)throw new IOException("LoaderContext.allowCodeImport is false");
                    if(bytes[9]!=64)throw new IOException("Dynamic loading requires a 64-bit IGGY container");
                    lock(sync)if(!closed&&!job.Cancelled){job.MovieBytes=bytes;job.Bytes=bytes.Length;}
                    return;
                }
                if(bytes.Length>32*1024*1024)throw new IOException("Image file exceeds 32 MiB");
                string type=bytes.Length>=8&&bytes[0]==137&&bytes[1]==80&&bytes[2]==78&&bytes[3]==71?"image/png":bytes.Length>=3&&bytes[0]==255&&bytes[1]==216&&bytes[2]==255?"image/jpeg":null;
                if(type==null)throw new IOException("Resource is not PNG, JPEG or a compiled IGGY movie; raw SWF loading is unavailable");
                int[] pixels;int width,height;
                using(MemoryStream stream=new MemoryStream(bytes,false))using(Image source=Image.FromStream(stream,true,true)) {
                    width=source.Width;height=source.Height;
                    if(width<1||height<1||width>3840||height>3840||width*(long)height>16*1024*1024)throw new IOException("Image dimensions exceed native BitmapData limits");
                    lock(sync)if(closed||job.Cancelled)return;
                    using(Bitmap bitmap=new Bitmap(width,height,PixelFormat.Format32bppArgb)) {
                        using(Graphics g=Graphics.FromImage(bitmap)){g.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;g.DrawImage(source,new Rectangle(0,0,width,height));}
                        pixels=new int[width*height];BitmapData data=bitmap.LockBits(new Rectangle(0,0,width,height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
                        try {for(int y=0;y<height;y++)Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),pixels,y*width,width);}finally {bitmap.UnlockBits(data);}
                    }
                }
                lock(sync) {
                    if(closed||job.Cancelled)return;
                    long pending=allocated;
                    foreach(Job existing in jobs.Values)if(existing.Pixels!=null)pending+=existing.Pixels.LongLength*4;
                    if(pending+pixels.LongLength*4>128*1024*1024)throw new IOException("Loaded image storage exceeds 128 MiB for this movie");
                    job.Pixels=pixels;job.Width=width;job.Height=height;job.Bytes=bytes.Length;
                    job.Status="ready|"+width+"|"+height+"|"+bytes.Length+"|"+type+"|"+job.ResolvedUrl;
                }
            } catch(Exception ex) {
                lock(sync)if(!closed&&!job.Cancelled)job.Status="error|"+ex.Message.Replace('|',' ');
            }
        }
        internal void PumpMovies() {
            List<Job> pending=new List<Job>();
            lock(sync)foreach(Job job in jobs.Values)if(job.MovieBytes!=null&&job.Opened&&!job.Cancelled&&!job.Linking){job.Linking=true;pending.Add(job);}
            foreach(Job job in pending) {
                try {
                    Native.LoadedMovie movie=native.RegisterMovie(job.Path,job.MovieBytes,job.Domain);
                    lock(sync)if(!closed&&!job.Cancelled){job.Movie=movie;job.Status="movie|"+movie.Width+"|"+movie.Height+"|"+job.Bytes+"|application/x-shockwave-flash|"+job.ResolvedUrl;}
                } catch(Exception ex) {lock(sync)if(!closed&&!job.Cancelled)job.Status="error|"+ex.Message.Replace('|',' ');}
                finally {lock(sync)job.MovieBytes=null;}
            }
        }
        IntPtr Construct(IntPtr context) {
            IntPtr result=originalConstructor(context);
            try {
                if(Marshal.ReadInt32(context,16)<5)return result;
                IntPtr arguments=Marshal.ReadIntPtr(context,24),value=Marshal.ReadIntPtr(arguments,40);
                if((value.ToInt64()&3)!=1||Marshal.ReadByte(IntPtr.Subtract(value,1))!=8)return result;
                IntPtr text=Marshal.ReadIntPtr(value,7);int length=Marshal.ReadInt32(text,7);if(length!=32)return result;
                string token=Marshal.PtrToStringUni(IntPtr.Add(text,11),length);
                lock(sync) {
                    Job job;if(!jobs.TryGetValue(token,out job)||job.Pixels==null||job.Cancelled||job.Uploaded)return result;
                    IntPtr receiver=Marshal.ReadIntPtr(arguments),buffer=Marshal.ReadIntPtr(receiver,55);
                    if(buffer==IntPtr.Zero||Marshal.ReadInt32(receiver,47)!=job.Width||Marshal.ReadInt32(receiver,51)!=job.Height)return result;
                    int size=checked(job.Pixels.Length*4);
                    if(allocated+size>128*1024*1024)throw new IOException("Loaded image storage exceeds 128 MiB for this movie");
                    Marshal.Copy(job.Pixels,0,buffer,job.Pixels.Length);
                    Pixels owned=new Pixels {Definition=Marshal.AllocHGlobal(88),Size=size};
                    bitmaps.Add(owned.Definition,owned);
                    IntPtr definition=owned.Definition,raw=owned.Raw=Marshal.AllocHGlobal(size);allocated+=size;
                    for(int i=0;i<88;i++)Marshal.WriteByte(definition,i,0);
                    Marshal.WriteByte(definition,0,4);Marshal.WriteByte(definition,1,128);
                    Marshal.WriteInt16(definition,32,(short)job.Width);Marshal.WriteInt16(definition,34,(short)job.Height);
                    Marshal.WriteByte(definition,36,4);Marshal.WriteByte(definition,39,2);
                    *((float*)((byte*)definition+48))=1;*((float*)((byte*)definition+52))=1;
                    // Native texture source format 0 is RGBA, premultiplied.
                    int[] rgba=new int[job.Pixels.Length];
                    for(int i=0;i<rgba.Length;i++) {
                        uint p=(uint)job.Pixels[i],a=p>>24;
                        rgba[i]=(int)((a<<24)|(((p>>16)&255)*a/255)|((((p>>8)&255)*a/255)<<8)|(((p&255)*a/255)<<16));
                    }
                    Marshal.Copy(rgba,0,raw,rgba.Length);Marshal.WriteIntPtr(definition,64,raw);Marshal.WriteInt32(definition,80,size);
                    Marshal.WriteIntPtr(receiver,39,definition);
                    owned.Owners.Add(reference(native.Player,receiver,1));job.Uploaded=true;
                }
            } catch(Exception ex) {log.Write("Loader image upload failed: "+ex.Message);}
            return result;
        }
        IntPtr Clone(IntPtr context) {
            IntPtr result=originalClone(context),value=Marshal.ReadIntPtr(Marshal.ReadIntPtr(context,8));
            if((value.ToInt64()&3)==1) {
                Pixels pixels;if(bitmaps.TryGetValue(Marshal.ReadIntPtr(value,39),out pixels))pixels.Owners.Add(reference(native.Player,value,1));
            }
            return result;
        }
        IntPtr ConstructInfo(IntPtr context) {
            IntPtr result=originalInfoConstructor(context);
            if(Marshal.ReadInt32(context,16)>0) {
                IntPtr arguments=Marshal.ReadIntPtr(context,24),info=Marshal.ReadIntPtr(arguments),owner=Marshal.ReadIntPtr(arguments,8);
                if(ReadText(owner)=="__iggy_amf"&&Marshal.ReadInt32(context,16)>=4){StartAmfRequest(info,arguments);return result;}
                if(ReadText(owner)=="__iggy_bytes"&&Marshal.ReadInt32(context,16)>=5) {
                    StartBytes(info,arguments);return result;
                }
                if(ReadText(owner)=="__iggy_bind_domain"&&Marshal.ReadInt32(context,16)>=3) {
                    int domain;
                    if(int.TryParse(ReadText(Marshal.ReadIntPtr(arguments,24)),out domain)&&native.ValidMovieDomain(domain)) {
                        IntPtr previous;if(domainObjects.TryGetValue(domain,out previous))freeReference(native.Player,previous);
                        domainObjects[domain]=reference(native.Player,Marshal.ReadIntPtr(arguments,16),1);
                    }
                    return result;
                }
                if(ReadText(owner)=="__iggy_definition"&&Marshal.ReadInt32(context,16)>=3) {
                    int domain;string name=ReadText(Marshal.ReadIntPtr(arguments,24));
                    if(int.TryParse(ReadText(Marshal.ReadIntPtr(arguments,16)),out domain)&&name!=null)
                        definitions.Add(new DefinitionTicket {Request=reference(native.Player,info,1),Domain=domain,Name=name});
                    return result;
                }
                if(Marshal.ReadInt32(context,16)>=4&&MovieOperation(owner)) {
                    native.Activity.Stop(Marshal.ReadIntPtr(arguments,24),native.Activity.True(Marshal.ReadIntPtr(arguments,32)));return result;
                }
                string token=Token(owner);
                if(token!=null&&Marshal.ReadInt32(context,16)>=2) {
                    Job job;if(jobs.TryGetValue(token,out job)&&job.Movie!=null&&!job.Cancelled) {
                        tickets.Add(new MovieTicket {Request=reference(native.Player,info,1),Info=reference(native.Player,Marshal.ReadIntPtr(arguments,16),1),Movie=job.Movie,Token=token});
                    }
                } else if((owner.ToInt64()&3)==1&&Marshal.ReadByte(IntPtr.Subtract(owner,1))==36)
                    infos.Add(new InfoOwner {Info=reference(native.Player,info,1),Owner=reference(native.Player,owner,1),BaseFile=constructing==null?null:constructing.Movie.FileName});
            }
            return result;
        }
        static bool MovieOperation(IntPtr value) {
            if((value.ToInt64()&3)!=1||value.ToInt64()<65536||Marshal.ReadByte(IntPtr.Subtract(value,1))!=8)return false;
            IntPtr text=Marshal.ReadIntPtr(value,7);int length=Marshal.ReadInt32(text,7);
            return length==22&&Marshal.PtrToStringUni(IntPtr.Add(text,11),length)=="__iggy_unload_and_stop";
        }
        static string Token(IntPtr value) {
            if((value.ToInt64()&3)!=1||Marshal.ReadByte(IntPtr.Subtract(value,1))!=8)return null;
            IntPtr text=Marshal.ReadIntPtr(value,7);if(Marshal.ReadInt32(text,7)!=32)return null;
            return Marshal.PtrToStringUni(IntPtr.Add(text,11),32);
        }
        static string ReadText(IntPtr value) {
            if(value.ToInt64()<65536||(value.ToInt64()&3)!=1||Marshal.ReadByte(IntPtr.Subtract(value,1))!=8)return null;
            IntPtr text=Marshal.ReadIntPtr(value,7);int length=Marshal.ReadInt32(text,7);
            return length>=0&&length<=65536?Marshal.PtrToStringUni(IntPtr.Add(text,11),length):null;
        }
        MovieRoot FindRoot(IntPtr value) {
            if((value.ToInt64()&3)!=1)return null;
            IntPtr instance=Marshal.ReadIntPtr(value,39);
            for(int depth=0;instance!=IntPtr.Zero&&depth<256;depth++,instance=Marshal.ReadIntPtr(instance,128))
                foreach(MovieRoot root in roots)if(root.Instance==instance&&Marshal.ReadIntPtr(root.Root).ToInt64()!=7)return root;
            return null;
        }
        internal bool IsMovieRoot(IntPtr value) {foreach(MovieRoot root in roots)if(Marshal.ReadIntPtr(root.Root)==value)return true;return false;}
        internal IntPtr LookupLoaderInfo(IntPtr arguments) {
            MovieRoot root=FindRoot(Marshal.ReadIntPtr(arguments));
            if(root==null)return IntPtr.Zero;IntPtr info=Marshal.ReadIntPtr(root.Info);return info.ToInt64()==7?IntPtr.Zero:info;
        }
        IntPtr GetRoot(IntPtr context) {
            MovieRoot root=FindRoot(Marshal.ReadIntPtr(Marshal.ReadIntPtr(context,24)));
            if(root==null)return originalRoot(context);
            Marshal.WriteIntPtr(Marshal.ReadIntPtr(context,8),Marshal.ReadIntPtr(root.Root));return IntPtr.Zero;
        }
        IntPtr GetLoaderUrl(IntPtr context) {
            IntPtr receiver=Marshal.ReadIntPtr(Marshal.ReadIntPtr(context,24));
            foreach(InfoOwner info in infos)if(Marshal.ReadIntPtr(info.Info)==receiver) {
                string file=info.BaseFile;
                MovieRoot parent=FindRoot(Marshal.ReadIntPtr(info.Owner));
                if(parent!=null)foreach(MovieTicket ticket in tickets)if(Marshal.ReadIntPtr(ticket.Info)==Marshal.ReadIntPtr(parent.Info)){file=ticket.Movie.FileName;break;}
                if(file==null)file=native.FileName;
                // Use the ordinary string constructor so the VM roots and GC
                // representation match its other LoaderInfo string getters.
                return native.ReturnMovieString(context,new Uri("file:///"+file.Replace('\\','/')).AbsoluteUri);
            }
            return originalLoaderUrl(context);
        }
        IntPtr ConstructDisplay(IntPtr context,Builtin original) {
            if(native.Activity!=null)native.Activity.Track(Marshal.ReadIntPtr(Marshal.ReadIntPtr(context,24)));
            IntPtr result=original(context);
            if(constructing!=null) {
                IntPtr value=Marshal.ReadIntPtr(Marshal.ReadIntPtr(context,24)),instance=Marshal.ReadIntPtr(value,39);
                if(instance!=IntPtr.Zero&&Marshal.ReadIntPtr(instance,168)==constructing.Movie.Definition&&!roots.Exists(root=>root.Instance==instance))
                    roots.Add(new MovieRoot {Root=reference(native.Player,value,1),Info=reference(native.Player,Marshal.ReadIntPtr(constructing.Info),1),Instance=instance});
            }
            return result;
        }
        IntPtr GetContent(IntPtr context) {
            IntPtr receiver=Marshal.ReadIntPtr(Marshal.ReadIntPtr(context,24));
            IntPtr amfResult;if(AmfContent(context,receiver,out amfResult))return amfResult;
            for(int i=0;i<byteRequests.Count;i++)if(Marshal.ReadIntPtr(byteRequests[i].Request)==receiver) {
                ByteRequest request=byteRequests[i];byteRequests.RemoveAt(i);freeReference(native.Player,request.Request);
                return native.ReturnMovieString(context,request.Token);
            }
            for(int i=0;i<definitions.Count;i++)if(Marshal.ReadIntPtr(definitions[i].Request)==receiver) {
                DefinitionTicket definition=definitions[i];definitions.RemoveAt(i);freeReference(native.Player,definition.Request);
                IntPtr value;
                bool found=native.TryMovieDomainDefinition(definition.Domain,definition.Name,out value);
                Marshal.WriteIntPtr(Marshal.ReadIntPtr(context,8),found?value:nullValue);return IntPtr.Zero;
            }
            foreach(MovieTicket ticket in tickets.ToArray())if(Marshal.ReadIntPtr(ticket.Request)==receiver) {
                MovieTicket previous=constructing;constructing=ticket;
                MovieActivity.Scope previousScope=native.Activity.Current;int previousDomain=native.ExecutionDomain;
                native.ExecutionDomain=ticket.Movie.Domain;
                native.Activity.Current=new MovieActivity.Scope {Parent=previousScope,Domain=ticket.Movie.Domain};
                try {
                    IntPtr value=native.InstantiateMovie(ticket.Movie);Marshal.WriteIntPtr(Marshal.ReadIntPtr(context,8),value);
                    Job job;if(jobs.TryGetValue(ticket.Token,out job))job.Uploaded=true;
                } catch(Exception ex) {log.Write("Loader movie construction failed: "+ex.Message);Marshal.WriteIntPtr(Marshal.ReadIntPtr(context,8),nullValue);}
                finally {constructing=previous;native.Activity.Current=previousScope;native.ExecutionDomain=previousDomain;}
                return IntPtr.Zero;
            }
            return originalContent(context);
        }
        IntPtr GetApplicationDomain(IntPtr context) {
            IntPtr reference,value=nullValue;
            if(domainObjects.TryGetValue(1,out reference))value=Marshal.ReadIntPtr(reference);
            Marshal.WriteIntPtr(Marshal.ReadIntPtr(context,8),value);return IntPtr.Zero;
        }
        IntPtr GetLoader(IntPtr context) {
            IntPtr receiver=Marshal.ReadIntPtr(Marshal.ReadIntPtr(context,24));
            foreach(InfoOwner info in infos)if(Marshal.ReadIntPtr(info.Info)==receiver) {
                IntPtr owner=Marshal.ReadIntPtr(info.Owner);
                if(owner.ToInt64()==7)break;
                Marshal.WriteIntPtr(Marshal.ReadIntPtr(context,8),owner);return IntPtr.Zero;
            }
            return originalLoaderGetter(context);
        }
        internal void Prune() {
            native.PruneByteArrays();
            PruneAmfRequests();
            PruneXml();
            PruneByteRequests();
            for(int i=definitions.Count-1;i>=0;i--)if(Marshal.ReadIntPtr(definitions[i].Request).ToInt64()==7){freeReference(native.Player,definitions[i].Request);definitions.RemoveAt(i);}
            for(int i=tickets.Count-1;i>=0;i--)if(Marshal.ReadIntPtr(tickets[i].Request).ToInt64()==7){freeReference(native.Player,tickets[i].Request);freeReference(native.Player,tickets[i].Info);tickets.RemoveAt(i);}
            for(int i=roots.Count-1;i>=0;i--)if(Marshal.ReadIntPtr(roots[i].Root).ToInt64()==7){freeReference(native.Player,roots[i].Root);freeReference(native.Player,roots[i].Info);roots.RemoveAt(i);}
            for(int i=infos.Count-1;i>=0;i--)if(Marshal.ReadIntPtr(infos[i].Info).ToInt64()==7||Marshal.ReadIntPtr(infos[i].Owner).ToInt64()==7) {
                freeReference(native.Player,infos[i].Info);freeReference(native.Player,infos[i].Owner);infos.RemoveAt(i);
            }
            foreach(Pixels pixels in bitmaps.Values) {
                for(int i=pixels.Owners.Count-1;i>=0;i--)if(Marshal.ReadIntPtr(pixels.Owners[i]).ToInt64()==7) {
                    freeReference(native.Player,pixels.Owners[i]);pixels.Owners.RemoveAt(i);
                }
                if(pixels.Owners.Count!=0||pixels.Raw==IntPtr.Zero)continue;
                // Native cache records can retain the small owner structure.
                // Keep that address until PlayerDestroy, but retire its texture
                // and pixel allocation as soon as all BitmapData clones die.
                native.Renderer.ReleaseLoadedTexture(Marshal.ReadIntPtr(pixels.Definition,40));
                Marshal.WriteIntPtr(pixels.Definition,40,IntPtr.Zero);Marshal.WriteIntPtr(pixels.Definition,64,IntPtr.Zero);
                Marshal.FreeHGlobal(pixels.Raw);pixels.Raw=IntPtr.Zero;
                lock(sync)allocated-=pixels.Size;
            }
        }
        internal void CancelAll() {
            foreach(AmfRequest request in amfRequests)FreeAmfRequest(request);amfRequests.Clear();
            foreach(IntPtr item in weakDictionaries)freeReference(native.Player,item);weakDictionaries.Clear();
            foreach(XmlValue item in xmlValues)freeReference(native.Player,item.Reference);xmlValues.Clear();
            foreach(ByteRequest request in byteRequests)freeReference(native.Player,request.Request);byteRequests.Clear();
            foreach(IntPtr reference in domainObjects.Values)freeReference(native.Player,reference);domainObjects.Clear();
            foreach(DefinitionTicket definition in definitions)freeReference(native.Player,definition.Request);definitions.Clear();
            lock(sync){closed=true;foreach(Job job in jobs.Values){job.Cancelled=true;job.Pixels=null;job.MovieBytes=null;job.SourceBytes=null;}jobs.Clear();}
            lock(sync)foreach(DataJob job in new List<DataJob>(dataJobs.Values))CancelData(job);
            foreach(Pixels pixels in bitmaps.Values){foreach(IntPtr owner in pixels.Owners)freeReference(native.Player,owner);pixels.Owners.Clear();}
            foreach(InfoOwner info in infos){freeReference(native.Player,info.Info);freeReference(native.Player,info.Owner);}infos.Clear();
            foreach(MovieTicket ticket in tickets){freeReference(native.Player,ticket.Request);freeReference(native.Player,ticket.Info);}tickets.Clear();
            foreach(MovieRoot root in roots){freeReference(native.Player,root.Root);freeReference(native.Player,root.Info);}roots.Clear();
        }
        internal void CancelScope(MovieActivity.Scope scope) {
            lock(sync)foreach(DataJob job in new List<DataJob>(dataJobs.Values))if(MovieActivity.Within(job.Owner,scope))CancelData(job);
            lock(sync)foreach(Job job in new List<Job>(jobs.Values))if(MovieActivity.Within(job.Owner,scope)) {
                job.Cancelled=true;job.Pixels=null;job.MovieBytes=null;job.SourceBytes=null;jobs.Remove(job.Token);
            }
        }
        public void Dispose() {
            // Native bitmap clones and cached textures share these definitions.
            // Release them only after PlayerDestroy has drained its cache queue.
            CancelAll();foreach(Pixels pixels in bitmaps.Values){if(pixels.Raw!=IntPtr.Zero)Marshal.FreeHGlobal(pixels.Raw);Marshal.FreeHGlobal(pixels.Definition);}
            bitmaps.Clear();GC.KeepAlive(constructor);GC.KeepAlive(clone);GC.KeepAlive(loaderGetter);GC.KeepAlive(infoConstructor);
            GC.KeepAlive(contentGetter);GC.KeepAlive(rootGetter);GC.KeepAlive(movieConstructor);GC.KeepAlive(spriteConstructor);GC.KeepAlive(loaderUrlGetter);
        }
    }
}
