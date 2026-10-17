$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$directory = Join-Path $PSScriptRoot '../test-data/loader'
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$bitmap = New-Object Drawing.Bitmap(32, 24)
$graphics = [Drawing.Graphics]::FromImage($bitmap)
$alphaBrush = New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(128, 255, 0, 0))
try {
    $graphics.Clear([Drawing.Color]::Transparent)
    $graphics.FillRectangle([Drawing.Brushes]::Red, 0, 0, 8, 24)
    $graphics.FillRectangle([Drawing.Brushes]::Lime, 8, 0, 8, 24)
    $graphics.FillRectangle([Drawing.Brushes]::Blue, 16, 0, 8, 24)
    $graphics.FillRectangle($alphaBrush, 24, 0, 8, 24)
    for ($y = 0; $y -lt 4; $y++) { for ($x = 0; $x -lt 4; $x++) { $bitmap.SetPixel($x, $y, [Drawing.Color]::Transparent) } }
    $bitmap.Save((Join-Path $directory 'colors.png'), [Drawing.Imaging.ImageFormat]::Png)
    $unicodeName = [string][char]0x017e + 'lu' + [char]0x0165 + 'ou' + [char]0x010d + 'k' + [char]0x00fd + ' image.png'
    Copy-Item -LiteralPath (Join-Path $directory 'colors.png') -Destination (Join-Path $directory $unicodeName)
    $graphics.Clear([Drawing.Color]::Cyan)
    $bitmap.Save((Join-Path $directory 'photo.jpg'), [Drawing.Imaging.ImageFormat]::Jpeg)
    [IO.File]::WriteAllBytes((Join-Path $directory 'invalid.png'), [byte[]](137, 80, 78, 71, 13, 10, 26, 10, 0))
} finally {
    $alphaBrush.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}
