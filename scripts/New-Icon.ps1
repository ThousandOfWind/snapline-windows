$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$assets = Join-Path $root 'src/Snapline.Windows/Assets'
New-Item -ItemType Directory -Force $assets | Out-Null

# Approved original artwork: a tilted screenshot held by an apricot clothespin.
# Supersample every ICO size; do not rely on Windows downscaling a single 256px entry.
Add-Type -ReferencedAssemblies System.Drawing.Common, System.Drawing.Primitives, System.Private.Windows.GdiPlus, System.Private.Windows.Core @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
public static class SnaplineIconArt {
    static GraphicsPath Rounded(float x, float y, float w, float h, float r) {
        var p = new GraphicsPath(); float d = r * 2;
        p.AddArc(x,y,d,d,180,90); p.AddArc(x+w-d,y,d,d,270,90);
        p.AddArc(x+w-d,y+h-d,d,d,0,90); p.AddArc(x,y+h-d,d,d,90,90); p.CloseFigure(); return p;
    }
    public static Bitmap Draw(int size) {
        using var large = new Bitmap(size * 4, size * 4);
        using (var g = Graphics.FromImage(large)) {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.Transparent);
            g.ScaleTransform(size * 4 / 100f, size * 4 / 100f);
            using var rope = new Pen(ColorTranslator.FromHtml("#59776C"),size <= 24 ? 4 : 2.6f) { StartCap=LineCap.Round,EndCap=LineCap.Round };
            g.DrawBezier(rope,5,25,35,32,65,32,95,25);
            var state=g.Save();g.TranslateTransform(50,27);g.RotateTransform(-7);g.TranslateTransform(-50,-27);
            using var shadow = new SolidBrush(Color.FromArgb(25,25,61,46));
            using var shade=Rounded(22,31,58,61,9);g.FillPath(shadow,shade);
            using var paper = new SolidBrush(ColorTranslator.FromHtml("#F9FCF6"));
            using var photo=Rounded(20,27,58,61,9);g.FillPath(paper,photo);
            using var edge = new Pen(ColorTranslator.FromHtml("#DDE9DF"),0.7f);g.DrawPath(edge,photo);
            using var mint=new SolidBrush(ColorTranslator.FromHtml("#B5DBCC"));
            using var inset=Rounded(27,35,44,36,5);g.FillPath(mint,inset);
            using var ink=new Pen(ColorTranslator.FromHtml("#438374"),size<=24?6:4){StartCap=LineCap.Round,EndCap=LineCap.Round};
            g.DrawLine(ink,35,46,61,46);if(size>20)g.DrawLine(ink,35,56,52,56);
            if(size>24){using var label=new Pen(ColorTranslator.FromHtml("#ABC6BB"),3){StartCap=LineCap.Round,EndCap=LineCap.Round};g.DrawLine(label,35,79,56,79);}
            g.Restore(state);
            using var wood=new SolidBrush(ColorTranslator.FromHtml("#EAAF75"));
            using var peg=Rounded(44,13,12,27,4);g.FillPath(wood,peg);
            if(size>24){using var groove=new Pen(ColorTranslator.FromHtml("#A76B42"),1.4f){StartCap=LineCap.Round,EndCap=LineCap.Round};g.DrawLine(groove,47,23,53,23);g.DrawLine(groove,50,28,50,37);}
        }
        var bitmap = new Bitmap(size,size);
        using (var g = Graphics.FromImage(bitmap)) {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(large,0,0,size,size);
        }
        return bitmap;
    }
}
'@
$sizes = @(16, 20, 24, 32, 48, 64, 128, 256)
$entries = foreach ($size in $sizes) {
    $bitmap = [SnaplineIconArt]::Draw($size)
    $stream = [System.IO.MemoryStream]::new()
    try {
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        if ($size -eq 256) { $bitmap.Save((Join-Path $assets 'snapline.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
        [pscustomobject]@{ Size = $size; Bytes = $stream.ToArray() }
    } finally { $bitmap.Dispose(); $stream.Dispose() }
}
$file = [System.IO.File]::Create((Join-Path $assets 'snapline.ico'))
$writer = [System.IO.BinaryWriter]::new($file)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$entries.Count)
    $offset = 6 + 16 * $entries.Count
    foreach ($entry in $entries) {
        $dimension = if ($entry.Size -eq 256) { 0 } else { $entry.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$entry.Bytes.Length); $writer.Write([uint32]$offset)
        $offset += $entry.Bytes.Length
    }
    foreach ($entry in $entries) { $writer.Write([byte[]]$entry.Bytes) }
} finally { $writer.Dispose() }
Write-Host 'Generated hanging-screenshot logo: PNG and 8 ICO sizes with simplified small-size details.'
