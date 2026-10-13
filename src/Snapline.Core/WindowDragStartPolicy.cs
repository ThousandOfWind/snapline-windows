namespace Snapline.Core;

public static class WindowDragStartPolicy
{
    /// <summary>Called only after a native move-start event. Modern titlebars can report HTCLIENT or time out.</summary>
    public static bool Accept(int? hitCode, double x, double y, double width, double dpiScale, bool leftPressed)
    {
        if (!leftPressed || !double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(width)
            || !double.IsFinite(dpiScale) || dpiScale <= 0) return false;
        // Frame edges, caption buttons and system-menu hits must never turn resizing/button clicks into a stash.
        if (hitCode != null && hitCode != 1 && hitCode != 2) return false;
        var inset = 7 * dpiScale;
        return width > inset * 2 && x >= inset && x <= width - inset && y >= inset && y <= 80 * dpiScale;
    }
}
