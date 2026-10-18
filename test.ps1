param(
    [string] $PlayerPath = (Join-Path $PSScriptRoot 'IggyPlayer.exe'),
    [string] $DllPath,
    [string] $OutputDirectory = (Join-Path $PSScriptRoot 'test-output'),
    [string] $GameMovie,
    [switch] $TestOpenGl,
    [switch] $TestAudio
)
$ErrorActionPreference = 'Stop'
$movie = Join-Path $PSScriptRoot 'test-data/graphics.iggy'
if ($GameMovie) { $GameMovie = (Resolve-Path -LiteralPath $GameMovie).Path }
$output = $OutputDirectory
$executable = (Resolve-Path -LiteralPath $PlayerPath).Path
$movieHash = (Get-FileHash -LiteralPath $movie -Algorithm SHA256).Hash
New-Item -ItemType Directory -Path $output -Force | Out-Null
function Invoke-PlayerTest([string[]] $PlayerArguments, [string] $LogName, [int] $ExpectedExit = 0) {
    if ($DllPath) { $PlayerArguments += @('--dll', (Resolve-Path -LiteralPath $DllPath).Path) }
    $quotedArguments = $PlayerArguments | ForEach-Object { '"' + $_ + '"' }
    $playerProcess = Start-Process -FilePath $executable -ArgumentList $quotedArguments -WindowStyle Hidden -Wait -PassThru
    Copy-Item -LiteralPath ([IO.Path]::ChangeExtension($executable, '.log')) -Destination (Join-Path $output $LogName)
    if ($playerProcess.ExitCode -ne $ExpectedExit) { throw "$LogName returned $($playerProcess.ExitCode), expected $ExpectedExit. See test-output/$LogName" }
}
Invoke-PlayerTest @('--self-test', $movie, (Join-Path $output 'graphics')) 'self-test.log'
Invoke-PlayerTest @('--self-test-seek', $movie, (Join-Path $output 'seek')) 'seek-test.log'
Invoke-PlayerTest @('--self-test-focal-instances', (Join-Path $PSScriptRoot 'test-data/focal-instances.iggy'), (Join-Path $output 'focal-instances')) 'focal-instances.log'
Invoke-PlayerTest @('--self-test-filters', (Join-Path $PSScriptRoot 'test-data/filters.iggy'), (Join-Path $output 'filters')) 'filters.log'
Invoke-PlayerTest @('--self-test-as3', (Join-Path $PSScriptRoot 'test-data/as3.iggy'), (Join-Path $output 'as3')) 'as3.log'
Invoke-PlayerTest @('--self-test-keyboard', (Join-Path $PSScriptRoot 'test-data/keyboard.iggy'), (Join-Path $output 'keyboard')) 'keyboard.log'
Invoke-PlayerTest @('--self-test-cursor', (Join-Path $PSScriptRoot 'test-data/cursor.iggy'), (Join-Path $output 'cursor')) 'cursor.log'
Invoke-PlayerTest @('--self-test-clock', $movie, (Join-Path $output 'clock')) 'clock.log'
Invoke-PlayerTest @('--self-test-zero-scale', (Join-Path $PSScriptRoot 'test-data/zero-scale.iggy'), (Join-Path $output 'zero-scale')) 'zero-scale.log'
Invoke-PlayerTest @('--self-test-loaderinfo', (Join-Path $PSScriptRoot 'test-data/loaderinfo.iggy'), (Join-Path $output 'loaderinfo')) 'loaderinfo.log'
Invoke-PlayerTest @('--self-test-runtime', (Join-Path $PSScriptRoot 'test-data/runtime.iggy'), (Join-Path $output 'runtime')) 'runtime.log'
Invoke-PlayerTest @('--renderer', 'software', '--self-test-loader', (Join-Path $PSScriptRoot 'test-data/loader.iggy'), (Join-Path $output 'loader')) 'loader.log'
Invoke-PlayerTest @('--renderer', 'software', '--self-test-loader-bytes', (Join-Path $PSScriptRoot 'test-data/bytes-loader.iggy'), (Join-Path $output 'bytes-loader')) 'bytes-loader.log'
Invoke-PlayerTest @('--renderer', 'software', '--self-test-url-loader', (Join-Path $PSScriptRoot 'test-data/url-loader.iggy'), (Join-Path $output 'url-loader')) 'url-loader.log'
Invoke-PlayerTest @('--renderer', 'software', '--self-test-http-loader', (Join-Path $PSScriptRoot 'test-data/http-loader.iggy'), (Join-Path $output 'http-loader')) 'http-loader.log'
Invoke-PlayerTest @('--renderer', 'software', '--self-test-network-display', (Join-Path $PSScriptRoot 'test-data/network-display.iggy'), (Join-Path $output 'network-display')) 'network-display.log'
Invoke-PlayerTest @('--renderer', 'software', '--self-test-bytearray', (Join-Path $PSScriptRoot 'test-data/bytearray.iggy'), (Join-Path $output 'bytearray')) 'bytearray.log'
Invoke-PlayerTest @('--renderer', 'software', '--self-test-amf', (Join-Path $PSScriptRoot 'test-data/amf.iggy'), (Join-Path $output 'amf')) 'amf.log'
Invoke-PlayerTest @('--renderer', 'software', '--self-test-sharedobject', (Join-Path $PSScriptRoot 'test-data/sharedobject.iggy'), (Join-Path $output 'sharedobject')) 'sharedobject.log'
Invoke-PlayerTest @('--renderer', 'software', '--self-test-sharedobject-autosave', (Join-Path $PSScriptRoot 'test-data/sharedobject-auto.iggy'), (Join-Path $output 'sharedobject-autosave')) 'sharedobject-autosave.log'
Invoke-PlayerTest @('--renderer', 'software', '--self-test-movie-loader', (Join-Path $PSScriptRoot 'test-data/movie-loader.iggy'), (Join-Path $output 'movie-loader')) 'movie-loader.log'
Invoke-PlayerTest @('--self-test-domains', (Join-Path $PSScriptRoot 'test-data/domains.iggy'), (Join-Path $output 'domains')) 'domains.log'
Invoke-PlayerTest @('--renderer', 'software', '--self-test-unload', (Join-Path $PSScriptRoot 'test-data/movie-loader.iggy'), (Join-Path $output 'unload')) 'unload.log'
Invoke-PlayerTest @('--self-test-fixup-partitions', $movie, (Join-Path $output 'fixup-partitions')) 'fixup-partitions.log'
if ($TestOpenGl) {
    Invoke-PlayerTest @('--self-test-opengl', $movie, (Join-Path $output 'opengl')) 'opengl.log'
    Invoke-PlayerTest @('--renderer', 'opengl', '--self-test-loader', (Join-Path $PSScriptRoot 'test-data/loader.iggy'), (Join-Path $output 'loader-opengl')) 'loader-opengl.log'
    Invoke-PlayerTest @('--renderer', 'opengl', '--self-test-loader-bytes', (Join-Path $PSScriptRoot 'test-data/bytes-loader.iggy'), (Join-Path $output 'bytes-loader-opengl')) 'bytes-loader-opengl.log'
    Invoke-PlayerTest @('--renderer', 'opengl', '--self-test-url-loader', (Join-Path $PSScriptRoot 'test-data/url-loader.iggy'), (Join-Path $output 'url-loader-opengl')) 'url-loader-opengl.log'
    Invoke-PlayerTest @('--renderer', 'opengl', '--self-test-http-loader', (Join-Path $PSScriptRoot 'test-data/http-loader.iggy'), (Join-Path $output 'http-loader-opengl')) 'http-loader-opengl.log'
    Invoke-PlayerTest @('--renderer', 'opengl', '--self-test-network-display', (Join-Path $PSScriptRoot 'test-data/network-display.iggy'), (Join-Path $output 'network-display-opengl')) 'network-display-opengl.log'
    Invoke-PlayerTest @('--renderer', 'opengl', '--self-test-bytearray', (Join-Path $PSScriptRoot 'test-data/bytearray.iggy'), (Join-Path $output 'bytearray-opengl')) 'bytearray-opengl.log'
    Invoke-PlayerTest @('--renderer', 'opengl', '--self-test-amf', (Join-Path $PSScriptRoot 'test-data/amf.iggy'), (Join-Path $output 'amf-opengl')) 'amf-opengl.log'
    Invoke-PlayerTest @('--renderer', 'opengl', '--self-test-sharedobject', (Join-Path $PSScriptRoot 'test-data/sharedobject.iggy'), (Join-Path $output 'sharedobject-opengl')) 'sharedobject-opengl.log'
    Invoke-PlayerTest @('--renderer', 'opengl', '--self-test-sharedobject-autosave', (Join-Path $PSScriptRoot 'test-data/sharedobject-auto.iggy'), (Join-Path $output 'sharedobject-autosave-opengl')) 'sharedobject-autosave-opengl.log'
    Invoke-PlayerTest @('--renderer', 'opengl', '--self-test-movie-loader', (Join-Path $PSScriptRoot 'test-data/movie-loader.iggy'), (Join-Path $output 'movie-loader-opengl')) 'movie-loader-opengl.log'
    Invoke-PlayerTest @('--renderer', 'opengl', '--self-test-unload', (Join-Path $PSScriptRoot 'test-data/movie-loader.iggy'), (Join-Path $output 'unload-opengl')) 'unload-opengl.log'
}
if ($TestAudio) {
    Invoke-PlayerTest @('--self-test-audio', (Join-Path $PSScriptRoot 'test-data/audio.iggy'), (Join-Path $output 'audio')) 'audio.log'
    Invoke-PlayerTest @('--self-test-unload-audio', (Join-Path $PSScriptRoot 'test-data/movie-loader.iggy'), (Join-Path $output 'unload-audio')) 'unload-audio.log'
}
if ($GameMovie) {
    Invoke-PlayerTest @('--self-test-fixups', $GameMovie, (Join-Path $output 'game')) 'game.log'
    Invoke-PlayerTest @('--self-test-game-playback', $GameMovie, (Join-Path $output 'game-playback')) 'game-playback.log'
    Invoke-PlayerTest @('--self-test-game-keyboard', $GameMovie, (Join-Path $output 'game-keyboard')) 'game-keyboard.log'
    Invoke-PlayerTest @('--self-test-play-response', $GameMovie, (Join-Path $output 'play-response')) 'play-response.log'
    Invoke-PlayerTest @('--self-test-level-exit', $GameMovie, (Join-Path $output 'level-exit')) 'level-exit.log'
} else {
    Write-Output 'Skipping optional game-specific tests; use -GameMovie PATH with a compatible regression fixture.'
}
Invoke-PlayerTest @('--ui-test', (Join-Path $PSScriptRoot 'test-data/as3.iggy'), (Join-Path $output 'as3-player-ui.png'), '1') 'as3-ui.log'
Invoke-PlayerTest @('--ui-test', $movie, (Join-Path $output 'player-ui.png'), '47') 'ui-test.log'
Invoke-PlayerTest @('--ui-test', $movie, (Join-Path $output 'focal-player-ui.png'), '63') 'focal-ui-test.log'
Invoke-PlayerTest @('--ui-test', $movie, (Join-Path $output 'color-player-ui.png'), '49') 'color-ui-test.log'
Invoke-PlayerTest @('--ui-test', $movie, (Join-Path $output 'seek-player-ui.png'), '5,4') 'seek-ui-test.log'
Invoke-PlayerTest @('--native-focal', '--render', $movie, (Join-Path $output 'focal-native63.png'), '960', '640', '1', '63') 'focal-native.log'
Invoke-PlayerTest @('--render', (Join-Path $output 'file-does-not-exist.iggy'), (Join-Path $output 'error.png')) 'expected-error.log' 1
if ((Get-FileHash -LiteralPath $movie -Algorithm SHA256).Hash -ne $movieHash) { throw 'Source movie changed during testing' }
Write-Output 'PASS: native playback, 67 frames, input, resource lifetime, GUI capture, CLI error exit, unchanged source.'
