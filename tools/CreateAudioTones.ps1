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
$payload = $null
$blockSize = 0
$sampleCount = 0
for ($at = 12; $at + 8 -le $bytes.Length; ) {
    $length = [BitConverter]::ToUInt32($bytes, $at + 4)
    if ($length -gt $bytes.Length - $at - 8) { throw 'Invalid WAV chunk' }
    $chunk = [Text.Encoding]::ASCII.GetString($bytes, $at, 4)
    if ($chunk -eq 'fmt ') {
        if ($length -lt 16 -or [BitConverter]::ToUInt16($bytes, $at + 8) -ne 0x5346 -or
            [BitConverter]::ToUInt16($bytes, $at + 10) -ne 1 -or
            [BitConverter]::ToUInt32($bytes, $at + 12) -ne 22050 -or
            [BitConverter]::ToUInt16($bytes, $at + 22) -ne 4) { throw 'Unexpected ADPCM WAV format' }
        $blockSize = [BitConverter]::ToUInt16($bytes, $at + 20)
    }
    if ($chunk -eq 'fact' -and $length -ge 4) { $sampleCount = [BitConverter]::ToUInt32($bytes, $at + 8) }
    if ($chunk -eq 'data') {
        $payload = New-Object byte[] $length
        [Array]::Copy($bytes, $at + 8, $payload, 0, $length)
    }
    $at += 8 + $length + ($length -band 1)
}
if ($null -eq $payload) { throw 'No ADPCM data chunk found' }
if ($blockSize -ne 2051 -or $sampleCount -ne 8820 -or $payload.Length -ne $blockSize * 3) {
    throw 'Unexpected ADPCM WAV packet layout'
}
# WAV stores independent byte-aligned packets. DefineSound has one code-size
# field followed by continuous 4096-sample blocks, with no packet padding.
# Keep only the real samples from the encoder's zero-padded final packet.
$bitCount = 2 + 3 * 22 + ($sampleCount - 3) * 4
$packed = New-Object byte[] ([int][Math]::Ceiling($bitCount / 8.0))
$packed[0] = 0x80 # Four bits per ADPCM code (stored as 4 - 2).
$outputBit = 2
for ($packet = 0; $packet -lt 3; $packet++) {
    $inputBit = $packet * $blockSize * 8
    if (($payload[$packet * $blockSize] -shr 6) -ne 2) { throw 'Unexpected ADPCM code size' }
    $inputBit += 2
    $samples = [Math]::Min(4096, $sampleCount - $packet * 4096)
    for ($bit = 0; $bit -lt 22 + ($samples - 1) * 4; $bit++, $inputBit++, $outputBit++) {
        $value = ($payload[$inputBit -shr 3] -shr (7 - ($inputBit -band 7))) -band 1
        $packed[$outputBit -shr 3] = $packed[$outputBit -shr 3] -bor ($value -shl (7 - ($outputBit -band 7)))
    }
}
[IO.File]::WriteAllBytes((Join-Path $directory 'tone-adpcm.bin'), $packed)
