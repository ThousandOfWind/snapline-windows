namespace Snapline.Core;

/// <summary>Monitor-relative position, independent of pixels/DPI. Never restore outside the work area.</summary>
public sealed class WindowPosition
{
    public string Screen { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
}

public static class PlacementMath
{
    public static double Ratio(double value) => double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;
    public static int Offset(double ratio, int available) => (int)Math.Round(Ratio(ratio) * Math.Max(0, available));
    public static double Relative(int offset, int available) => available > 0 ? Ratio((double)offset / available) : 0;
}
