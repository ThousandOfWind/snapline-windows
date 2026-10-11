using System;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using Snapline.Core;
using Forms = System.Windows.Forms;

namespace Snapline.Windows;

internal static class WindowPlacement
{
    internal static Forms.Screen Screen(WindowPosition? saved, Forms.Screen fallback) =>
        Forms.Screen.AllScreens.FirstOrDefault(s => string.Equals(s.DeviceName, saved?.Screen, StringComparison.OrdinalIgnoreCase)) ?? fallback;

    internal static System.Drawing.Point Restore(WindowPosition saved, System.Drawing.Rectangle area, int width, int height) =>
        new(area.Left + PlacementMath.Offset(saved.X, area.Width - width), area.Top + PlacementMath.Offset(saved.Y, area.Height - height));

    internal static WindowPosition Remember(Window window)
    {
        var bounds = Native.Bounds(new WindowInteropHelper(window).Handle);
        var screen = Forms.Screen.FromRectangle(bounds);
        var area = screen.WorkingArea;
        return new WindowPosition
        {
            Screen = screen.DeviceName,
            X = PlacementMath.Relative(bounds.Left - area.Left, area.Width - bounds.Width),
            Y = PlacementMath.Relative(bounds.Top - area.Top, area.Height - bounds.Height)
        };
    }
}
