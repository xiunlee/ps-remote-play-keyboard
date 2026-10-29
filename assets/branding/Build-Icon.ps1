param(
    [string]$SourceImage = (Join-Path $PSScriptRoot 'ps-mark.png'),
    [string]$OutputIcon = (Join-Path $PSScriptRoot 'app.ico')
)
# Format conversion only: keep generated artwork and colors unchanged.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$source = [Drawing.Bitmap]::FromFile((Resolve-Path -LiteralPath $SourceImage).Path)
$frames = New-Object 'Collections.Generic.List[byte[]]'
$sizes = @(16,24,32,48,64,128,256)
try {
    foreach ($size in $sizes) {
        $bitmap = New-Object Drawing.Bitmap($size,$size,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $wrap = New-Object Drawing.Imaging.ImageAttributes
            try {
                $wrap.SetWrapMode([Drawing.Drawing2D.WrapMode]::TileFlipXY)
                $graphics.DrawImage($source,(New-Object Drawing.Rectangle(0,0,$size,$size)),0,0,$source.Width,$source.Height,[Drawing.GraphicsUnit]::Pixel,$wrap)
            } finally { $wrap.Dispose() }
        } finally { $graphics.Dispose() }
        $memory = New-Object IO.MemoryStream
        $writer = New-Object IO.BinaryWriter($memory)
        try {
            if ($size -eq 256) {
                $bitmap.Save($memory,[Drawing.Imaging.ImageFormat]::Png)
            } else {
                $maskStride = [int]([Math]::Ceiling($size / 32.0) * 4)
                $writer.Write([uint32]40)
                $writer.Write([int32]$size)
                $writer.Write([int32]($size * 2))
                $writer.Write([uint16]1)
                $writer.Write([uint16]32)
                $writer.Write([uint32]0)
                $writer.Write([uint32]($size * $size * 4))
                1..4 | ForEach-Object { $writer.Write([uint32]0) }
                for ($y=$size-1;$y -ge 0;$y--) {
                    for ($x=0;$x -lt $size;$x++) {
                        $pixel=$bitmap.GetPixel($x,$y)
                        $writer.Write([byte]$pixel.B); $writer.Write([byte]$pixel.G)
                        $writer.Write([byte]$pixel.R); $writer.Write([byte]$pixel.A)
                    }
                }
                $writer.Write((New-Object byte[] ($maskStride * $size)))
            }
            $writer.Flush()
            $frames.Add($memory.ToArray())
        } finally { $writer.Dispose(); $memory.Dispose(); $bitmap.Dispose() }
    }
} finally { $source.Dispose() }
$output = [IO.File]::Create([IO.Path]::GetFullPath($OutputIcon))
$iconWriter = New-Object IO.BinaryWriter($output)
try {
    $iconWriter.Write([uint16]0); $iconWriter.Write([uint16]1); $iconWriter.Write([uint16]$sizes.Length)
    $offset = 6 + 16 * $sizes.Length
    for ($i=0;$i -lt $sizes.Length;$i++) {
        $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
        $iconWriter.Write([byte]$dimension); $iconWriter.Write([byte]$dimension)
        $iconWriter.Write([byte]0); $iconWriter.Write([byte]0)
        $iconWriter.Write([uint16]1); $iconWriter.Write([uint16]32)
        $iconWriter.Write([uint32]$frames[$i].Length); $iconWriter.Write([uint32]$offset)
        $offset += $frames[$i].Length
    }
    foreach ($frame in $frames) { $iconWriter.Write($frame) }
} finally { $iconWriter.Dispose(); $output.Dispose() }
$check = New-Object Drawing.Icon([IO.Path]::GetFullPath($OutputIcon),32,32)
try { if ($check.Width -ne 32 -or $check.Height -ne 32) { throw 'Windows icon validation failed.' } }
finally { $check.Dispose() }
[pscustomobject]@{sizes=$sizes;file=[IO.Path]::GetFileName($OutputIcon);bytes=(Get-Item -LiteralPath $OutputIcon).Length;artworkChanged=$false}
