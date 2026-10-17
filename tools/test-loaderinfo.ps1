param(
    [string] $DllPath,
    [string] $MoviePath = (Join-Path $PSScriptRoot '../test-data/loaderinfo.iggy'),
    [string] $LogPath = (Join-Path $PSScriptRoot '../test-output/loaderinfo-probe.log')
)
$ErrorActionPreference = 'Stop'
if (-not $DllPath) {
    foreach ($name in @('iggy_w64_final.dll', 'iggy_w64.dll')) {
        $candidate = Join-Path $PSScriptRoot "../lib/$name"
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { $DllPath = $candidate; break }
    }
    if (-not $DllPath) { throw 'Cannot find lib/iggy_w64_final.dll or lib/iggy_w64.dll. Use -DllPath PATH to override.' }
}
New-Item -ItemType Directory -Path (Split-Path ([IO.Path]::GetFullPath($LogPath))) -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '../src') -Filter '*.cs' | ForEach-Object FullName)
$sources += Join-Path $PSScriptRoot 'LoaderInfoProbe.cs'
$output = Join-Path $PSScriptRoot 'LoaderInfoProbe.exe'
& $compiler /nologo /unsafe /optimize+ /platform:x64 /target:exe /main:IggyViewer.LoaderInfoProbe "/out:$output" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/resource:$(Join-Path $PSScriptRoot '../runtime/sharedobject.abc'),IggyViewer.SharedObject.abc" "/resource:$(Join-Path $PSScriptRoot '../runtime/loader.abc'),IggyViewer.Loader.abc" "/resource:$(Join-Path $PSScriptRoot '../runtime/audio.abc'),IggyViewer.Audio.abc" $sources
if ($LASTEXITCODE -ne 0) { throw 'Diagnostic compilation failed' }
& $output ([IO.Path]::GetFullPath($DllPath)) ([IO.Path]::GetFullPath($MoviePath)) ([IO.Path]::GetFullPath($LogPath))
if ($LASTEXITCODE -ne 0) { throw 'LoaderInfo diagnostic failed' }
Get-Content -LiteralPath $LogPath
