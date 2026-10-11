$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$assets = Join-Path $root 'src/Snapline.Windows/Assets'
New-Item -ItemType Directory -Force $assets | Out-Null

# Original Snapline artwork: mint picture tiles on a navy diamond. No upstream assets.
$bitmap = [System.Drawing.Bitmap]::new(256, 256)
$g = [System.Drawing.Graphics]::FromImage($bitmap)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)
$navy = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#18232F'))
$mint = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#75E5C2'))
$white = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#F1F5F6'))
$points = [System.Drawing.Point[]]@([System.Drawing.Point]::new(128, 8), [System.Drawing.Point]::new(248, 128), [System.Drawing.Point]::new(128, 248), [System.Drawing.Point]::new(8, 128))
$g.FillPolygon($navy, $points)
$g.FillRectangle($mint, 66, 64, 109, 96)
$g.FillRectangle($white, 84, 89, 109, 96)
$g.FillRectangle($navy, 94, 99, 89, 58)
$g.FillEllipse($mint, 147, 109, 18, 18)
$g.FillPolygon($mint, [System.Drawing.Point[]]@([System.Drawing.Point]::new(95, 156), [System.Drawing.Point]::new(122, 124), [System.Drawing.Point]::new(150, 156)))
$bitmap.Save((Join-Path $assets 'snapline.png'), [System.Drawing.Imaging.ImageFormat]::Png)
# PNG-backed ICO, supported by Windows Vista and later.
$stream = [System.IO.MemoryStream]::new()
$bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
$png = $stream.ToArray()
$file = [System.IO.File]::Create((Join-Path $assets 'snapline.ico'))
$writer = [System.IO.BinaryWriter]::new($file)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
$writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0)
$writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$png.Length); $writer.Write([uint32]22)
$writer.Write($png)
$writer.Dispose(); $stream.Dispose(); $g.Dispose(); $bitmap.Dispose(); $navy.Dispose(); $mint.Dispose(); $white.Dispose()
Write-Host 'Generated original Snapline icon.'
