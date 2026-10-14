namespace Snapline.Core;

public enum CardRelease { None, PendingSingle, DoubleClick }

/// <summary>Click classification independent of WPF ClickCount. Double-click commits after second release.</summary>
public sealed class CardGesture(int doubleClickMs, double doubleWidth, double doubleHeight, double dragWidth, double dragHeight)
{
    private bool pressed, dragged, doubleCandidate, pending;
    private ImagePoint start, previous;
    private long downAt, previousDown;
    public bool IsPressed => pressed;

    public bool Press(long timestamp, ImagePoint point)
    {
        doubleCandidate = pending && timestamp >= previousDown && timestamp - previousDown <= doubleClickMs
            && Math.Abs(point.X - previous.X) <= doubleWidth && Math.Abs(point.Y - previous.Y) <= doubleHeight;
        start = point; downAt = timestamp; pressed = true; dragged = false;
        pending = false; return doubleCandidate;
    }
    public bool Move(ImagePoint point)
    {
        if (!pressed || dragged || (Math.Abs(point.X - start.X) < dragWidth && Math.Abs(point.Y - start.Y) < dragHeight)) return false;
        dragged = true; pressed = false; pending = false; doubleCandidate = false; return true;
    }
    public CardRelease Release(long timestamp)
    {
        if (!pressed || dragged) return CardRelease.None;
        pressed = false;
        if (doubleCandidate) { doubleCandidate = false; pending = false; return CardRelease.DoubleClick; }
        previous = start; previousDown = downAt; pending = true; return CardRelease.PendingSingle;
    }
    public int WaitForSingle(long timestamp) => (int)Math.Clamp(doubleClickMs - Math.Max(0, timestamp - previousDown), 1, doubleClickMs);
    public bool CompleteSingle() { if (!pending || pressed) return false; pending = false; return true; }
    public void Cancel() { pressed = dragged = pending = doubleCandidate = false; }
}

public readonly record struct ImageCardSize(double Width, double Height)
{
    public static ImageCardSize Fit(int pixelWidth, int pixelHeight, double maxWidth, double maxHeight)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0 || maxWidth <= 0 || maxHeight <= 0
            || !double.IsFinite(maxWidth) || !double.IsFinite(maxHeight)) throw new ArgumentOutOfRangeException(nameof(pixelWidth));
        var scale = Math.Min(maxWidth / pixelWidth, maxHeight / pixelHeight);
        return new ImageCardSize(pixelWidth * scale, pixelHeight * scale);
    }
}
