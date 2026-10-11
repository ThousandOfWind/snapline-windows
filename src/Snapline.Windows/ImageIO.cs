using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Snapline.Windows;

internal static class ImageIO
{
    internal static BitmapSource Load(string path, int maxPixels = 400)
    {
        // OnLoad avoids holding source files open: Explorer can move/delete them.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        if (maxPixels > 0) image.DecodePixelWidth = maxPixels;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    internal static void SavePng(BitmapSource image, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        encoder.Save(stream);
    }

    internal static bool TryLoad(string path, out BitmapSource? image)
    {
        try { image = Load(path); return true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException or ArgumentException)
        { image = null; return false; }
    }

    internal static BitmapSource TestImage()
    {
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
        {
            context.DrawRectangle(new SolidColorBrush(Color.FromRgb(23, 34, 49)), null, new System.Windows.Rect(0, 0, 640, 360));
            context.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(87, 218, 177)), null,
                new System.Windows.Rect(80, 65, 480, 230), 22, 22);
            context.DrawText(new System.Windows.Media.FormattedText("Snapline", System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight, new Typeface("Segoe UI"), 56, Brushes.DarkSlateGray, 1), new System.Windows.Point(190, 140));
        }
        var bitmap = new RenderTargetBitmap(640, 360, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(drawing);
        bitmap.Freeze();
        return bitmap;
    }
}
