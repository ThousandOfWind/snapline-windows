using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snapline.Core;

namespace Snapline.Windows;

internal static class EditorVerification
{
    internal static async Task RunAsync(string output, Action<bool, string> check)
    {
        var source = Pattern(); var empty = new ImageEditorDocument(source.PixelWidth, source.PixelHeight);
        var baseImage = AnnotationRenderer.Render(source, empty.State);
        check(baseImage.PixelWidth == 120 && baseImage.PixelHeight == 90, "editor renderer preserves full pixel resolution without crop");
        var origin = Path.Combine(output, "editor-source.png"); ImageIO.SavePng(source, origin);
        var originalHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(origin)));
        foreach (var tool in new[] { MarkTool.Pen, MarkTool.Highlight, MarkTool.Arrow, MarkTool.Rectangle, MarkTool.Ellipse, MarkTool.Text })
        {
            var document = new ImageEditorDocument(120, 90);
            document.Add(new ImageMark(tool, tool == MarkTool.Text ? new[] { new ImagePoint(10, 10) } : new[] { new ImagePoint(10, 10), new ImagePoint(80, 55) },
                0xFFE65F55, 4, tool == MarkTool.Text ? "Test 标注" : ""));
            var annotated = AnnotationRenderer.Render(source, document.State);
            check(!Pixels(annotated).SequenceEqual(Pixels(baseImage)), $"{tool} changes rendered pixels");
        }
        var cropped = new ImageEditorDocument(120, 90);
        cropped.Add(new ImageMark(MarkTool.Obscure, [new ImagePoint(20, 20), new ImagePoint(40, 40)], 0x00FF0000, 4));
        cropped.Crop(new ImagePoint(10, 10), new ImagePoint(70, 65));
        var cropImage = AnnotationRenderer.Render(source, cropped.State);
        check(cropImage.PixelWidth == 60 && cropImage.PixelHeight == 55, "crop exports selected raster dimensions");
        var pixel = Pixel(cropImage, 15, 15);
        check(pixel == (0, 0, 255, 255), "opaque cover stays aligned in original coordinates after crop and forces alpha opaque");
        var covered = new ImageEditorDocument(120, 90);
        covered.Add(new ImageMark(MarkTool.Obscure, [new ImagePoint(15, 15), new ImagePoint(60, 60)], 0x00000000, 4));
        covered.Add(new ImageMark(MarkTool.Mosaic, [new ImagePoint(20, 20), new ImagePoint(50, 50)], 0, 4));
        check(Pixel(AnnotationRenderer.Render(source, covered.State), 30, 30) == (0, 0, 0, 255),
            "mosaic after redaction never reveals original pixels under an opaque cover");
        var mosaic = new ImageEditorDocument(120, 90);
        mosaic.Add(new ImageMark(MarkTool.Mosaic, [new ImagePoint(40, 0), new ImagePoint(80, 50)], 0, 4));
        var mosaicImage = AnnotationRenderer.Render(source, mosaic.State);
        check(Pixel(mosaicImage, 53, 21) == Pixel(mosaicImage, 55, 22), "mosaic flattens nearby original pixels into blocks");
        var dest = Path.Combine(output, "editor-cropped.png"); AnnotationRenderer.SaveCopy(cropImage, origin, dest);
        check(ImageIO.Load(dest, 0).PixelWidth == 60, "saved edit is a decodable flattened PNG copy");
        var refused = false; try { AnnotationRenderer.SaveCopy(cropImage, origin, origin); } catch (InvalidOperationException) { refused = true; }
        check(refused && Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(origin))) == originalHash, "saving onto source path is refused and original bytes unchanged");
        refused = false; try { AnnotationRenderer.SaveCopy(cropImage, origin, dest); } catch (IOException) { refused = true; }
        check(refused, "existing copy is not silently overwritten");

        BitmapSource? copied = null;
        var copyPath = Path.Combine(output, "editor-window-copy.png");
        var window = new ImageEditorWindow(origin, () => copyPath, result => copied = result, confirmDiscard: false);
        try
        {
            window.Show(); await Task.Delay(120);
            var tools = Descendants(window).OfType<Button>().Where(b => System.Windows.Automation.AutomationProperties.GetAutomationId(b).StartsWith("EditorTool", StringComparison.Ordinal)).ToArray();
            check(tools.Length == 9, "editor exposes nine explicit image/annotation tools");
            window.SelectTool(EditorTool.Arrow);
            check(window.Commit(new ImageMark(MarkTool.Arrow, [new ImagePoint(10, 20), new ImagePoint(90, 45)], 0xFFFF0000, 3)), "editor window commits annotation through document renderer");
            window.Document.Crop(new ImagePoint(5, 5), new ImagePoint(100, 75)); window.RefreshImage();
            var copy = Descendants(window).OfType<Button>().Single(b => System.Windows.Automation.AutomationProperties.GetAutomationId(b) == "EditorCopy");
            copy.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            check(copied?.PixelWidth == 95 && copied.PixelHeight == 70, "copy control hands off edited full-resolution crop without using real clipboard in test");
            check(window.Document.IsDirty, "copy alone does not pretend edits were saved to disk");
            window.CancelDraft(); check(window.Document.State.Marks.Count == 1, "cancel draft leaves committed annotations unchanged");
            check(window.SaveDefaultCopy() && window.ResultPath == copyPath && !window.Document.IsDirty, "default save creates new edited file and marks only that state saved");
            check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(origin))) == originalHash, "editor UI operations do not alter original image bytes");
            window.UpdateLayout(); await Task.Delay(100);
            var canvas = Descendants(window).OfType<ImageEditorCanvas>().Single();
            check(canvas.Image?.PixelWidth == 95 && canvas.Crop.Width == 95, "editor canvas refresh reflects cropped annotated result");
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
            ImageIO.SavePng(bitmap, Path.Combine(output, "editor-ui.png"));
        }
        finally { window.Close(); }
        var preview = new PreviewWindow(origin);
        try
        {
            preview.Show(); await Task.Delay(60);
            var editButton = Descendants(preview).OfType<Button>().Single(b => System.Windows.Automation.AutomationProperties.GetAutomationId(b) == "PreviewEdit");
            editButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            check(preview.EditRequested && !preview.IsVisible, "preview edit button requests internal editing rather than launching an external app");
        }
        finally { preview.Close(); }
        var demoPath = Path.Combine(output, "editor-demo-source.png"); ImageIO.SavePng(ImageIO.TestImage(), demoPath);
        var demo = new ImageEditorWindow(demoPath, () => Path.Combine(output, "editor-demo-copy.png"), _ => { }, confirmDiscard: false);
        try
        {
            demo.Show(); await Task.Delay(100);
            demo.Commit(new ImageMark(MarkTool.Rectangle, [new ImagePoint(75, 60), new ImagePoint(570, 300)], 0xFFE65F55, 4));
            demo.Commit(new ImageMark(MarkTool.Arrow, [new ImagePoint(45, 330), new ImagePoint(150, 235)], 0xFFFFB861, 5));
            demo.Commit(new ImageMark(MarkTool.Text, [new ImagePoint(210, 85)], 0xFF182D30, 3, "可直接编辑、标注"));
            demo.UpdateLayout(); await Task.Delay(150);
            var bitmap = new RenderTargetBitmap((int)demo.ActualWidth, (int)demo.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(demo);
            ImageIO.SavePng(bitmap, Path.Combine(output, "editor-demo-ui.png"));
        }
        finally { demo.Close(); }
    }

    private static BitmapSource Pattern()
    {
        var bytes = new byte[120 * 90 * 4];
        for (var y = 0; y < 90; y++) for (var x = 0; x < 120; x++)
        {
            var offset = (y * 120 + x) * 4; bytes[offset] = (byte)((x + y) * 5); bytes[offset + 1] = (byte)(y * 7); bytes[offset + 2] = (byte)(x * 3); bytes[offset + 3] = 255;
        }
        var image = BitmapSource.Create(120, 90, 96, 96, PixelFormats.Bgra32, null, bytes, 120 * 4); image.Freeze(); return image;
    }
    private static byte[] Pixels(BitmapSource image)
    {
        var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        var bytes = new byte[image.PixelWidth * image.PixelHeight * 4]; converted.CopyPixels(bytes, image.PixelWidth * 4, 0); return bytes;
    }
    private static (byte, byte, byte, byte) Pixel(BitmapSource image, int x, int y)
    {
        var bytes = Pixels(image); var i = (y * image.PixelWidth + x) * 4; return (bytes[i], bytes[i + 1], bytes[i + 2], bytes[i + 3]);
    }
    private static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var item in Descendants(child)) yield return item;
        }
    }
}
