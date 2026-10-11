using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Snapline.Windows;

/// <summary>A disposable, input-transparent photo following the cursor during the native OLE drag loop.</summary>
internal sealed class DragPreviewWindow : Window, IDisposable
{
    private readonly DispatcherTimer follow = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly TextBlock hint;
    private bool disposed;

    internal DragPreviewWindow(BitmapSource image)
    {
        Title = "Snapline Drag Preview";
        Width = 160; Height = 150;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true; IsHitTestVisible = false;
        var visual = new StackPanel { Margin = new Thickness(10), RenderTransform = new RotateTransform(-3), RenderTransformOrigin = new Point(0.5, 0.5) };
        var photo = new Border
        {
            Background = PaperStyle.Brush("#F9FCF6"), CornerRadius = new CornerRadius(10), Padding = new Thickness(6), Height = 105,
            Effect = new DropShadowEffect { BlurRadius = 12, ShadowDepth = 4, Opacity = 0.23 },
            Child = new Image { Source = image, Stretch = Stretch.Uniform }
        };
        visual.Children.Add(photo);
        hint = new TextBlock { Text = "拖到目标以复制", FontSize = 10, Foreground = PaperStyle.Brush("#355748") };
        visual.Children.Add(new Border { Child = hint, Background = PaperStyle.Brush("#F3F7EE"), CornerRadius = new CornerRadius(8),
            Padding = new Thickness(7, 4, 7, 4), Margin = new Thickness(0, 7, 0, 0), HorizontalAlignment = HorizontalAlignment.Center });
        Content = visual;
        SourceInitialized += (_, _) =>
        {
            Native.MakeInputTransparent(this);
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook((IntPtr hwnd, int msg, IntPtr wp, IntPtr lp, ref bool handled) =>
            {
                if (msg == 0x0084) { handled = true; return new IntPtr(-1); }
                return IntPtr.Zero;
            });
        };
        follow.Tick += (_, _) => PlaceAt(Native.Cursor);
    }

    internal void Start()
    {
        Show(); PlaceAt(Native.Cursor); follow.Start();
    }

    internal void PlaceAt(System.Drawing.Point cursor)
    {
        if (disposed) return;
        var screen = Forms.Screen.FromPoint(cursor);
        var scale = Native.Scale(screen);
        var width = (int)Math.Round(Width * scale); var height = (int)Math.Round(Height * scale);
        var gap = (int)Math.Round(18 * scale);
        // Prefer the lower right, flip near screen edges; keep clear of the cursor/drop target.
        var x = cursor.X + gap; var y = cursor.Y + gap;
        if (x + width > screen.Bounds.Right) x = cursor.X - gap - width;
        if (y + height > screen.Bounds.Bottom) y = cursor.Y - gap - height;
        Native.Place(this, x, y, width, height);
    }

    internal void Feedback(DragDropEffects effect) => hint.Text =
        (effect & DragDropEffects.Copy) != 0 ? "松开以复制图片" : "拖到目标以复制 · Esc 取消";

    internal bool DoesNotInterceptInput => Native.IsInputTransparent(this);

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; follow.Stop(); Close();
    }
}
