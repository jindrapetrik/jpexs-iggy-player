param(
    [Parameter(Mandatory = $true)][string] $FFDecJar,
    [string] $JavaHome = $env:JAVA_HOME
)
$ErrorActionPreference = 'Stop'
$jar = (Resolve-Path -LiteralPath $FFDecJar).Path
$toolsDirectory = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../tools')).Path
$baseMovie = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../test-data/as3.swf')).Path
$java = if ($JavaHome) { Join-Path $JavaHome 'bin/java.exe' } else { 'java' }
$javac = if ($JavaHome) { Join-Path $JavaHome 'bin/javac.exe' } else { 'javac' }
$classPath = "$jar;$(Join-Path (Split-Path $jar) 'lib/*')"
& $javac -cp $classPath (Join-Path $toolsDirectory 'CompileRuntimeShim.java')
if ($LASTEXITCODE -ne 0) { throw 'Runtime compiler helper failed' }
& $java -cp "$toolsDirectory;$classPath" CompileRuntimeShim $baseMovie (Join-Path $PSScriptRoot 'sharedobject.abc') (Join-Path $PSScriptRoot 'HostSharedObjectRequest.as') (Join-Path $PSScriptRoot 'HostCodec.as') (Join-Path $PSScriptRoot 'SharedObject.as')
if ($LASTEXITCODE -ne 0) { throw 'SharedObject compilation failed' }
& $java -cp "$toolsDirectory;$classPath" CompileRuntimeShim $baseMovie (Join-Path $PSScriptRoot 'loader.abc') (Join-Path $PSScriptRoot 'Endian.as') (Join-Path $PSScriptRoot 'CompressionAlgorithm.as') (Join-Path $PSScriptRoot 'ObjectEncoding.as') (Join-Path $PSScriptRoot 'HostAmfRequest.as') (Join-Path $PSScriptRoot 'HostAmfAliases.as') (Join-Path $PSScriptRoot 'HostRegisterClassAlias.as') (Join-Path $PSScriptRoot 'HostGetClassByAlias.as') (Join-Path $PSScriptRoot 'IDataInput.as') (Join-Path $PSScriptRoot 'IDataOutput.as') (Join-Path $PSScriptRoot 'IExternalizable.as') (Join-Path $PSScriptRoot 'HostAmfIO.as') (Join-Path $PSScriptRoot 'XMLDocument.as') (Join-Path $PSScriptRoot 'HostAmf.as') (Join-Path $PSScriptRoot 'HostDomainFactory.as') (Join-Path $PSScriptRoot 'HostDomainBinding.as') (Join-Path $PSScriptRoot 'HostDefinitionRequest.as') (Join-Path $PSScriptRoot 'ApplicationDomain.as') (Join-Path $PSScriptRoot 'LoaderContext.as') (Join-Path $PSScriptRoot 'HostGetDefinitionByName.as') (Join-Path $PSScriptRoot 'HostDomainNames.as') (Join-Path $PSScriptRoot 'HostGetQualifiedClassName.as') (Join-Path $PSScriptRoot 'HostGetQualifiedSuperclassName.as') (Join-Path $PSScriptRoot 'HostURLRequestMethod.as') (Join-Path $PSScriptRoot 'HostURLRequestHeader.as') (Join-Path $PSScriptRoot 'HostURLRequest.as') (Join-Path $PSScriptRoot 'HostLoaderInfo.as') (Join-Path $PSScriptRoot 'HostIOErrorEvent.as') (Join-Path $PSScriptRoot 'HostProgressEvent.as') (Join-Path $PSScriptRoot 'HostLoadedBitmap.as') (Join-Path $PSScriptRoot 'HostMovieRequest.as') (Join-Path $PSScriptRoot 'HostBytesRequest.as') (Join-Path $PSScriptRoot 'HostLoader.as') (Join-Path $PSScriptRoot 'HostSecurityErrorEvent.as') (Join-Path $PSScriptRoot 'HostURLLoaderDataFormat.as') (Join-Path $PSScriptRoot 'HostURLVariables.as') (Join-Path $PSScriptRoot 'HostHTTPStatusEvent.as') (Join-Path $PSScriptRoot 'HostURLLoader.as') (Join-Path $PSScriptRoot 'HostSoundLoaderContext.as') (Join-Path $PSScriptRoot 'HostSoundLoad.as') (Join-Path $PSScriptRoot 'HostSoundLoading.as') (Join-Path $PSScriptRoot 'HostFileReferenceList.as') (Join-Path $PSScriptRoot 'HostTimerTask.as') (Join-Path $PSScriptRoot 'HostTimers.as') (Join-Path $PSScriptRoot 'setInterval.as') (Join-Path $PSScriptRoot 'setTimeout.as') (Join-Path $PSScriptRoot 'clearInterval.as') (Join-Path $PSScriptRoot 'clearTimeout.as')
if ($LASTEXITCODE -ne 0) { throw 'Loader compilation failed' }
& $java -cp "$toolsDirectory;$classPath" CompileRuntimeShim $baseMovie (Join-Path $PSScriptRoot 'audio.abc') (Join-Path $PSScriptRoot 'HostSoundEvents.as')
if ($LASTEXITCODE -ne 0) { throw 'Audio compatibility compilation failed' }
