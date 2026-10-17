$ErrorActionPreference = 'Stop'
$directory = Join-Path $PSScriptRoot '../test-data/audio'
$temporary = Join-Path $PSScriptRoot '../test-output/audio-tones'
New-Item -ItemType Directory -Path $directory, $temporary -Force | Out-Null
& ffmpeg -hide_banner -loglevel error -y -f lavfi -i 'sine=frequency=440:sample_rate=44100:duration=0.4' -af 'volume=0.2' -ac 2 -c:a libmp3lame -b:a 128k -write_xing 0 -id3v2_version 0 -write_id3v1 0 (Join-Path $directory 'tone-stereo.mp3')
if ($LASTEXITCODE -ne 0) { throw 'Cannot generate stereo MP3' }
& ffmpeg -hide_banner -loglevel error -y -f lavfi -i 'sine=frequency=660:sample_rate=22050:duration=0.4' -af 'volume=0.2' -ac 1 -c:a libmp3lame -b:a 48k -write_xing 0 -id3v2_version 0 -write_id3v1 0 (Join-Path $directory 'tone-mono.mp3')
if ($LASTEXITCODE -ne 0) { throw 'Cannot generate mono MP3' }
$wave = Join-Path $temporary 'adpcm.wav'
& ffmpeg -hide_banner -loglevel error -y -f lavfi -i 'sine=frequency=550:sample_rate=22050:duration=0.4' -af 'volume=0.2' -ac 1 -c:a adpcm_swf $wave
if ($LASTEXITCODE -ne 0) { throw 'Cannot generate SWF ADPCM' }
& ffmpeg -hide_banner -loglevel error -y -f lavfi -i 'sine=frequency=440:sample_rate=44100:duration=1' -af 'volume=0.2' -ac 2 -c:a libmp3lame -q:a 4 (Join-Path $directory 'tone-vbr.mp3')
if ($LASTEXITCODE -ne 0) { throw 'Cannot generate VBR MP3' }
$bytes = [IO.File]::ReadAllBytes($wave)
for ($at = 12; $at + 8 -le $bytes.Length; ) {
    $length = [BitConverter]::ToUInt32($bytes, $at + 4)
    if ($length -gt $bytes.Length - $at - 8) { throw 'Invalid WAV chunk' }
    if ([Text.Encoding]::ASCII.GetString($bytes, $at, 4) -eq 'data') {
        $payload = New-Object byte[] $length
        [Array]::Copy($bytes, $at + 8, $payload, 0, $length)
        [IO.File]::WriteAllBytes((Join-Path $directory 'tone-adpcm.bin'), $payload)
        return
    }
    $at += 8 + $length + ($length -band 1)
}
throw 'No ADPCM data chunk found'
