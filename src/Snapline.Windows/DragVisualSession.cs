using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Snapline.Windows;

/// <summary>Drag-only visual state, always unwound on cancel, failed drop or an exception.</summary>
internal sealed class DragVisualSession : IDisposable
{
    private readonly FrameworkElement source;
    private readonly double opacity;
    private readonly bool hitTest;
    private readonly UIElement? peg;
    private readonly Visibility pegVisibility;
    private bool disposed;
    internal DragPreviewWindow Preview { get; }

    internal DragVisualSession(FrameworkElement source, BitmapSource image, int count = 1, bool keepSourceHitTarget = false)
    {
        this.source = source; opacity = source.Opacity; hitTest = source.IsHitTestVisible;
        peg = FindPeg(source); pegVisibility = peg?.Visibility ?? Visibility.Visible;
        Preview = new DragPreviewWindow(image, count);
        try
        {
            // Reveal the "unclipped" photo at the cursor; leave a faint placeholder on the rope.
            source.BeginAnimation(UIElement.OpacityProperty, null); source.Opacity = 0.3;
            source.IsHitTestVisible = keepSourceHitTarget && hitTest; // In-app grouping must reject a self-drop instead of exposing the app underneath.
            if (peg != null) peg.Visibility = Visibility.Hidden;
            Preview.Start();
        }
        catch { Dispose(); throw; }
    }

    private static UIElement? FindPeg(DependencyObject source)
    {
        if (source is FrameworkElement { Tag: "SnaplinePeg" } element) return element;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(source); index++)
        {
            var result = FindPeg(VisualTreeHelper.GetChild(source, index));
            if (result != null) return result;
        }
        return null;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try { Preview.Dispose(); }
        finally
        {
            source.BeginAnimation(UIElement.OpacityProperty, null); source.Opacity = opacity;
            source.IsHitTestVisible = hitTest;
            if (peg != null) peg.Visibility = pegVisibility;
        }
    }
}
