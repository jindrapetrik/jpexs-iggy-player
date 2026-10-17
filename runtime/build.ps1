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
& $java -cp "$toolsDirectory;$classPath" CompileRuntimeShim $baseMovie (Join-Path $PSScriptRoot 'sharedobject.abc') (Join-Path $PSScriptRoot 'HostCodec.as') (Join-Path $PSScriptRoot 'SharedObject.as')
if ($LASTEXITCODE -ne 0) { throw 'SharedObject compilation failed' }
& $java -cp "$toolsDirectory;$classPath" CompileRuntimeShim $baseMovie (Join-Path $PSScriptRoot 'loader.abc') (Join-Path $PSScriptRoot 'HostURLRequest.as') (Join-Path $PSScriptRoot 'HostLoaderInfo.as') (Join-Path $PSScriptRoot 'HostIOErrorEvent.as') (Join-Path $PSScriptRoot 'HostProgressEvent.as') (Join-Path $PSScriptRoot 'HostLoadedBitmap.as') (Join-Path $PSScriptRoot 'HostLoader.as') (Join-Path $PSScriptRoot 'HostFileReferenceList.as')
if ($LASTEXITCODE -ne 0) { throw 'Loader compilation failed' }
& $java -cp "$toolsDirectory;$classPath" CompileRuntimeShim $baseMovie (Join-Path $PSScriptRoot 'audio.abc') (Join-Path $PSScriptRoot 'HostSoundEvents.as')
if ($LASTEXITCODE -ne 0) { throw 'Audio compatibility compilation failed' }
