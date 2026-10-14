using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snapline.Core;

namespace Snapline.Windows;

internal static class AnnotationRenderer
{
    internal static BitmapSource Render(BitmapSource source, ImageEditorState state)
    {
        BitmapSource current = source;
        var batch = new List<ImageMark>();
        foreach (var mark in state.Marks)
        {
            if (mark.Tool != MarkTool.Mosaic) { batch.Add(mark); continue; }
            // Flatten preceding marks before mosaic so it cannot reveal pixels underneath an opaque cover.
            current = Compose(current, batch, new ImageRegion(0, 0, source.PixelWidth, source.PixelHeight)); batch.Clear();
            current = Mosaic(current, mark);
        }
        return Compose(current, batch, state.Crop);
    }

    private static BitmapSource Compose(BitmapSource source, IEnumerable<ImageMark> marks, ImageRegion crop)
    {
        var visual = new DrawingVisual();
        using (var draw = visual.RenderOpen())
        {
            draw.PushTransform(new TranslateTransform(-crop.X, -crop.Y));
            draw.PushClip(new RectangleGeometry(new Rect(crop.X, crop.Y, crop.Width, crop.Height)));
            draw.DrawImage(source, new Rect(0, 0, source.PixelWidth, source.PixelHeight));
            foreach (var mark in marks) Draw(draw, mark);
            draw.Pop(); draw.Pop();
        }
        return Raster(visual, crop.Width, crop.Height);
    }

    internal static void Draw(DrawingContext draw, ImageMark mark)
    {
        var color = Color.FromArgb((byte)(mark.Argb >> 24), (byte)(mark.Argb >> 16), (byte)(mark.Argb >> 8), (byte)mark.Argb);
        if (mark.Tool == MarkTool.Highlight) color.A = 85;
        if (mark.Tool == MarkTool.Obscure) color.A = 255;
        var brush = new SolidColorBrush(color);
        var pen = new Pen(brush, mark.Tool == MarkTool.Highlight ? mark.Width * 4 : mark.Width)
        { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        var first = new Point(mark.Points[0].X, mark.Points[0].Y);
        var last = new Point(mark.Points[^1].X, mark.Points[^1].Y);
        var rect = new Rect(first, last);
        switch (mark.Tool)
        {
            case MarkTool.Pen:
            case MarkTool.Highlight:
                if (mark.Points.Count == 1) { draw.DrawEllipse(brush, null, first, pen.Thickness / 2, pen.Thickness / 2); break; }
                var geometry = new StreamGeometry();
                using (var path = geometry.Open())
                {
                    path.BeginFigure(first, false, false);
                    for (var i = 1; i < mark.Points.Count; i++) path.LineTo(new Point(mark.Points[i].X, mark.Points[i].Y), true, false);
                }
                draw.DrawGeometry(null, pen, geometry); break;
            case MarkTool.Arrow:
                draw.DrawLine(pen, first, last);
                var delta = last - first;
                if (delta.Length < 1) break;
                delta.Normalize(); var normal = new Vector(-delta.Y, delta.X);
                var length = Math.Max(12, mark.Width * 4);
                draw.DrawLine(pen, last, last - delta * length + normal * length * 0.45);
                draw.DrawLine(pen, last, last - delta * length - normal * length * 0.45); break;
            case MarkTool.Rectangle: draw.DrawRectangle(null, pen, rect); break;
            case MarkTool.Ellipse: draw.DrawEllipse(null, pen, new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2), rect.Width / 2, rect.Height / 2); break;
            case MarkTool.Obscure: draw.DrawRectangle(brush, null, rect); break;
            case MarkTool.Text:
                draw.DrawText(new FormattedText(mark.Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 12 + mark.Width * 5, brush, 1), first); break;
        }
    }

    private static BitmapSource Mosaic(BitmapSource source, ImageMark mark)
    {
        var bounds = ImageRegion.From(mark.Points[0], mark.Points[1], new ImageRegion(0, 0, source.PixelWidth, source.PixelHeight));
        if (bounds == null) return source;
        var area = bounds.Value;
        var piece = new FormatConvertedBitmap(new CroppedBitmap(source, new Int32Rect(area.X, area.Y, area.Width, area.Height)), PixelFormats.Bgra32, null, 0);
        var cell = Math.Max(6, (int)Math.Round(mark.Width * 3));
        var stride = area.Width * 4;
        var input = new byte[stride * area.Height]; piece.CopyPixels(input, stride, 0);
        var output = new byte[input.Length];
        for (var top = 0; top < area.Height; top += cell) for (var left = 0; left < area.Width; left += cell)
        {
            var right = Math.Min(area.Width, left + cell); var bottom = Math.Min(area.Height, top + cell);
            long blue = 0, green = 0, red = 0; var count = (right - left) * (bottom - top);
            for (var y = top; y < bottom; y++) for (var x = left; x < right; x++)
            {
                var i = y * stride + x * 4; var alpha = input[i + 3];
                blue += (input[i] * alpha + 255 * (255 - alpha) + 127) / 255;
                green += (input[i + 1] * alpha + 255 * (255 - alpha) + 127) / 255;
                red += (input[i + 2] * alpha + 255 * (255 - alpha) + 127) / 255;
            }
            for (var y = top; y < bottom; y++) for (var x = left; x < right; x++)
            {
                var i = y * stride + x * 4; output[i] = (byte)(blue / count); output[i + 1] = (byte)(green / count);
                output[i + 2] = (byte)(red / count); output[i + 3] = 255;
            }
        }
        var pixelated = BitmapSource.Create(area.Width, area.Height, 96, 96, PixelFormats.Bgra32, null, output, stride); pixelated.Freeze();
        var visual = new DrawingVisual();
        using (var draw = visual.RenderOpen())
        {
            draw.DrawImage(source, new Rect(0, 0, source.PixelWidth, source.PixelHeight));
            draw.DrawRectangle(Brushes.White, null, new Rect(area.X, area.Y, area.Width, area.Height));
            draw.DrawImage(pixelated, new Rect(area.X, area.Y, area.Width, area.Height));
        }
        return Raster(visual, source.PixelWidth, source.PixelHeight);
    }

    private static BitmapSource Raster(Visual visual, int width, int height)
    {
        var image = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); image.Render(visual); image.Freeze(); return image;
    }

    internal static void SaveCopy(BitmapSource rendered, string source, string destination)
    {
        if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("原图不能被覆盖，请另存为新文件。");
        // CreateNew also prevents alias/symlink/hardlink target overwrite. Shared output is flattened PNG, no edit layers/source payload.
        ImageIO.SavePng(rendered, destination);
    }
}
