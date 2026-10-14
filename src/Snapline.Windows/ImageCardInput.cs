using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Snapline.Core;
using Forms = System.Windows.Forms;

namespace Snapline.Windows;

/// <summary>Preview-phase input with a per-card clock; no reliance on native/WPF double-click message count.</summary>
internal sealed class ImageCardInput
{
    private readonly FrameworkElement card;
    private readonly Action single, doubleClick, drag;
    private readonly bool capturePointer;
    private readonly Func<ImagePoint> pointer;
    private readonly Func<long> clock;
    private readonly DispatcherTimer timer = new();
    private CardGesture? gesture;
    private bool releasing;

    internal ImageCardInput(FrameworkElement card, Action single, Action doubleClick, Action drag, bool capturePointer = true,
        Func<ImagePoint>? testPointer = null, Func<long>? testClock = null)
    {
        this.card = card; this.single = single; this.doubleClick = doubleClick; this.drag = drag; this.capturePointer = capturePointer;
        pointer = !capturePointer && testPointer != null ? testPointer : () => { var p = Native.Cursor; return new ImagePoint(p.X, p.Y); };
        clock = !capturePointer && testClock != null ? testClock : () => Environment.TickCount64;
        timer.Tick += (_, _) => { timer.Stop(); if (gesture?.CompleteSingle() == true) single(); };
        card.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (ButtonAncestor(e.OriginalSource as DependencyObject)) return;
            Press(clock(), pointer()); e.Handled = true;
        };
        card.PreviewMouseMove += (_, e) =>
        {
            if (gesture?.IsPressed != true || e.LeftButton != MouseButtonState.Pressed) return;
            Move(pointer());
        };
        card.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (gesture?.IsPressed != true || ButtonAncestor(e.OriginalSource as DependencyObject)) return;
            Release(clock()); e.Handled = true;
        };
        card.LostMouseCapture += (_, _) => { if (!releasing) Cancel(); };
        card.Unloaded += (_, _) => Cancel();
    }

    internal void Press(long timestamp, ImagePoint point)
    {
        timer.Stop();
        if (gesture == null)
        {
            var dpi = Native.Scale(Forms.Screen.FromPoint(new System.Drawing.Point((int)point.X, (int)point.Y)));
            var distance = Native.DoubleClickDistance(dpi);
            gesture = new CardGesture(Forms.SystemInformation.DoubleClickTime, distance.Width,
                distance.Height, SystemParameters.MinimumHorizontalDragDistance * dpi,
                SystemParameters.MinimumVerticalDragDistance * dpi);
        }
        gesture.Press(timestamp, point);
        if (capturePointer) card.CaptureMouse();
    }
    internal void Move(ImagePoint point)
    {
        if (gesture?.Move(point) != true) return;
        timer.Stop(); ReleaseCapture(); drag();
    }
    internal void Release(long timestamp)
    {
        var result = gesture?.Release(timestamp) ?? CardRelease.None;
        ReleaseCapture();
        if (result == CardRelease.DoubleClick) { timer.Stop(); doubleClick(); }
        else if (result == CardRelease.PendingSingle)
        { timer.Interval = TimeSpan.FromMilliseconds(gesture!.WaitForSingle(timestamp)); timer.Start(); }
    }
    internal void Cancel() { timer.Stop(); gesture?.Cancel(); }
    private void ReleaseCapture()
    {
        releasing = true;
        try { if (capturePointer && card.IsMouseCaptured) card.ReleaseMouseCapture(); }
        finally { releasing = false; }
    }
    private static bool ButtonAncestor(DependencyObject? item)
    {
        while (item != null)
        {
            if (item is System.Windows.Controls.Primitives.ButtonBase) return true;
            item = item is ContentElement content ? ContentOperations.GetParent(content) : VisualTreeHelper.GetParent(item);
        }
        return false;
    }
}
