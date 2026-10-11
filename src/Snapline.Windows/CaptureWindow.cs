using System;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Forms = System.Windows.Forms;

namespace Snapline.Windows;

/// <summary>Freezes the virtual desktop in memory; only the selected rectangle is saved.</summary>
internal sealed class CaptureWindow : Window
{
    private readonly Canvas canvas = new();
    private readonly Rectangle selection = new() { Stroke = Brushes.MediumAquamarine, StrokeThickness = 2, Fill = new SolidColorBrush(Color.FromArgb(28, 117, 229, 194)) };
    private readonly BitmapSource desktop;
    private Point start;
    private bool selecting;
    internal BitmapSource? CapturedImage { get; private set; }

    internal CaptureWindow(BitmapSource? syntheticDesktop = null)
    {
        Title = "Snapline Capture";
        var bounds = Forms.SystemInformation.VirtualScreen;
        desktop = syntheticDesktop ?? CaptureDesktop(bounds);
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; Topmost = true; Cursor = Cursors.Cross;
        var scale = Native.Scale(Forms.Screen.PrimaryScreen!);
        Left = bounds.Left / scale; Top = bounds.Top / scale;
        Width = bounds.Width / scale; Height = bounds.Height / scale;
        Background = new ImageBrush(desktop) { Stretch = Stretch.Fill };
        canvas.Background = new SolidColorBrush(Color.FromArgb(50, 0, 0, 0));
        canvas.Children.Add(selection);
        selection.Visibility = Visibility.Hidden;
        var hint = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(24, 35, 45)), CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18, 12, 18, 12),
            Child = new TextBlock { Text = "拖动选择截图区域 · Esc 取消", Foreground = Brushes.White, FontSize = 16 }, IsHitTestVisible = false
        };
        canvas.Children.Add(hint);
        var primary = Forms.Screen.PrimaryScreen!.Bounds;
        Canvas.SetLeft(hint, (primary.Left + primary.Width / 2.0 - bounds.Left) / scale - 170);
        Canvas.SetTop(hint, (primary.Top + 40 - bounds.Top) / scale);
        Content = canvas;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) DialogResult = false; };
        MouseLeftButtonDown += (_, e) =>
        {
            start = e.GetPosition(canvas); selecting = true; selection.Visibility = Visibility.Visible;
            Canvas.SetLeft(selection, start.X); Canvas.SetTop(selection, start.Y); selection.Width = 0; selection.Height = 0; CaptureMouse();
        };
        MouseMove += (_, e) =>
        {
            if (!selecting) return;
            var p = e.GetPosition(canvas);
            var x = Math.Clamp(p.X, 0, canvas.ActualWidth); var y = Math.Clamp(p.Y, 0, canvas.ActualHeight);
            Canvas.SetLeft(selection, Math.Min(start.X, x)); Canvas.SetTop(selection, Math.Min(start.Y, y));
            selection.Width = Math.Abs(x - start.X); selection.Height = Math.Abs(y - start.Y);
        };
        MouseLeftButtonUp += (_, _) =>
        {
            if (!selecting) return;
            selecting = false; ReleaseMouseCapture();
            var sx = desktop.PixelWidth / canvas.ActualWidth; var sy = desktop.PixelHeight / canvas.ActualHeight;
            var x = Math.Clamp((int)Math.Round(Canvas.GetLeft(selection) * sx), 0, desktop.PixelWidth - 1);
            var y = Math.Clamp((int)Math.Round(Canvas.GetTop(selection) * sy), 0, desktop.PixelHeight - 1);
            var w = Math.Min((int)Math.Round(selection.Width * sx), desktop.PixelWidth - x);
            var h = Math.Min((int)Math.Round(selection.Height * sy), desktop.PixelHeight - y);
            if (w < 3 || h < 3) { DialogResult = false; return; }
            CapturedImage = new CroppedBitmap(desktop, new Int32Rect(x, y, w, h)); CapturedImage.Freeze(); DialogResult = true;
        };
        SourceInitialized += (_, _) => Native.Place(this, bounds.Left, bounds.Top, bounds.Width, bounds.Height);
        Loaded += (_, _) => Activate();
    }

    private static BitmapSource CaptureDesktop(System.Drawing.Rectangle bounds)
    {
        using var bitmap = new System.Drawing.Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
            graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, System.Drawing.CopyPixelOperation.SourceCopy);
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png); stream.Position = 0;
        var image = new BitmapImage();
        image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze();
        return image;
    }
}
