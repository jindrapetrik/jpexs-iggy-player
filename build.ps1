param([string] $OutputPath = (Join-Path $PSScriptRoot 'IggyPlayer.exe'))
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$sources = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName
& $compiler /nologo /unsafe /optimize+ /platform:x64 /target:winexe "/out:$OutputPath" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/resource:$(Join-Path $PSScriptRoot 'runtime/sharedobject.abc'),IggyViewer.SharedObject.abc" "/resource:$(Join-Path $PSScriptRoot 'runtime/loader.abc'),IggyViewer.Loader.abc" "/resource:$(Join-Path $PSScriptRoot 'runtime/audio.abc'),IggyViewer.Audio.abc" $sources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
Write-Output "Built $OutputPath"
