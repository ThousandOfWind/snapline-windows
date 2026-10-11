using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace Snapline.Windows;

/// <summary>Drag only an explicit grip (or the collapsed button). Uses physical cursor coordinates.</summary>
internal sealed class ComponentDrag
{
    private readonly Window window;
    private readonly FrameworkElement grip;
    private readonly Action started;
    private readonly Action finished;
    private readonly Action? clicked;
    private System.Drawing.Point origin;
    private System.Drawing.Rectangle initial;
    private bool pressed;
    private bool moving;

    internal ComponentDrag(Window window, FrameworkElement grip, Action started, Action finished, Action? clicked = null)
    {
        this.window = window; this.grip = grip; this.started = started; this.finished = finished; this.clicked = clicked;
        grip.Cursor = Cursors.SizeAll;
        grip.PreviewMouseLeftButtonDown += Down;
        grip.PreviewMouseMove += Move;
        grip.PreviewMouseLeftButtonUp += Up;
        grip.LostMouseCapture += (_, _) => End(invokeClick: false);
    }

    private void Down(object sender, MouseButtonEventArgs e)
    {
        origin = Native.Cursor;
        initial = Native.Bounds(new WindowInteropHelper(window).Handle);
        pressed = true; moving = false;
        grip.CaptureMouse();
        e.Handled = true; // Do not turn a drag of the collapsed button into a click.
    }

    private void Move(object sender, MouseEventArgs e)
    {
        if (!pressed || e.LeftButton != MouseButtonState.Pressed) return;
        var cursor = Native.Cursor;
        var dx = cursor.X - origin.X; var dy = cursor.Y - origin.Y;
        if (!moving)
        {
            var dpi = Native.Scale(Forms.Screen.FromPoint(origin));
            if (Math.Abs(dx) < SystemParameters.MinimumHorizontalDragDistance * dpi
                && Math.Abs(dy) < SystemParameters.MinimumVerticalDragDistance * dpi) return;
            moving = true; started();
        }
        var screen = Forms.Screen.FromPoint(cursor);
        var scale = Native.Scale(screen);
        var width = (int)Math.Round(window.Width * scale);
        var height = (int)Math.Round(window.Height * scale);
        var area = screen.WorkingArea;
        var x = Math.Clamp(initial.Left + dx, area.Left, Math.Max(area.Left, area.Right - width));
        var y = Math.Clamp(initial.Top + dy, area.Top, Math.Max(area.Top, area.Bottom - height));
        Native.Place(window, x, y, width, height);
        e.Handled = true;
    }

    private void Up(object sender, MouseButtonEventArgs e)
    {
        if (!pressed) return;
        End(invokeClick: true);
        e.Handled = true;
    }

    private void End(bool invokeClick)
    {
        if (!pressed) return;
        pressed = false;
        var wasMoving = moving; moving = false;
        if (grip.IsMouseCaptured) grip.ReleaseMouseCapture();
        if (wasMoving) finished();
        else if (invokeClick) clicked?.Invoke();
    }
}
