namespace Snapline.Core;

public static class WindowDragFallbackPolicy
{
    /// <summary>Fallback requires translated geometry following the same held pointer, not a resize or mere header click.</summary>
    public static bool Accept(bool held, bool sameIdentity, int initialWidth, int initialHeight,
        int width, int height, double windowDx, double windowDy, double pointerDx, double pointerDy, double scale)
    {
        if (!held || !sameIdentity || !double.IsFinite(scale) || scale <= 0
            || !double.IsFinite(windowDx) || !double.IsFinite(windowDy) || !double.IsFinite(pointerDx) || !double.IsFinite(pointerDy)) return false;
        if (Math.Abs(width - initialWidth) > 3 * scale || Math.Abs(height - initialHeight) > 3 * scale) return false;
        return Math.Max(Math.Abs(pointerDx), Math.Abs(pointerDy)) >= 10 * scale
            && Math.Max(Math.Abs(windowDx), Math.Abs(windowDy)) >= 8 * scale
            && Math.Abs(windowDx - pointerDx) <= 18 * scale && Math.Abs(windowDy - pointerDy) <= 18 * scale;
    }
}
