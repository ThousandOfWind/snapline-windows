namespace Snapline.Core;

public enum MarkTool { Pen, Highlight, Arrow, Rectangle, Ellipse, Text, Obscure, Mosaic }
public readonly record struct ImagePoint(double X, double Y);
public readonly record struct ImageRegion(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public static ImageRegion? From(ImagePoint a, ImagePoint b, ImageRegion bounds)
    {
        if (!double.IsFinite(a.X) || !double.IsFinite(a.Y) || !double.IsFinite(b.X) || !double.IsFinite(b.Y)) return null;
        var x = (int)Math.Floor(Math.Clamp(Math.Min(a.X, b.X), bounds.X, bounds.Right));
        var y = (int)Math.Floor(Math.Clamp(Math.Min(a.Y, b.Y), bounds.Y, bounds.Bottom));
        var right = (int)Math.Ceiling(Math.Clamp(Math.Max(a.X, b.X), bounds.X, bounds.Right));
        var bottom = (int)Math.Ceiling(Math.Clamp(Math.Max(a.Y, b.Y), bounds.Y, bounds.Bottom));
        return right - x >= 2 && bottom - y >= 2 ? new ImageRegion(x, y, right - x, bottom - y) : null;
    }
}

public sealed class ImageMark
{
    public MarkTool Tool { get; }
    public IReadOnlyList<ImagePoint> Points { get; }
    public uint Argb { get; }
    public double Width { get; }
    public string Text { get; }
    public ImageMark(MarkTool tool, IEnumerable<ImagePoint> points, uint argb, double width, string text = "")
    {
        Tool = tool; Points = Array.AsReadOnly(points.ToArray()); Argb = argb; Width = width; Text = text;
    }
}
public sealed record ImageEditorState(ImageRegion Crop, IReadOnlyList<ImageMark> Marks);

/// <summary>Vector/crop history only; output is a flattened PNG copy, never source rewriting.</summary>
public sealed class ImageEditorDocument
{
    public const int MaxMarks = 200;
    public const int MaxStrokePoints = 4096;
    public ImageRegion Original { get; }
    public ImageEditorState State { get; private set; }
    private ImageEditorState saved;
    private readonly List<ImageEditorState> undo = [];
    private readonly List<ImageEditorState> redo = [];
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public bool IsDirty => !ReferenceEquals(State, saved);

    public ImageEditorDocument(int width, int height)
    {
        if (width <= 0 || height <= 0 || (long)width * height > 64_000_000) throw new ArgumentOutOfRangeException(nameof(width), "Image size exceeds editing limit.");
        Original = new ImageRegion(0, 0, width, height);
        State = new ImageEditorState(Original, Array.AsReadOnly(Array.Empty<ImageMark>())); saved = State;
    }

    public bool Add(ImageMark mark)
    {
        if (!Enum.IsDefined(mark.Tool) || State.Marks.Count >= MaxMarks || mark.Points.Count is < 1 or > MaxStrokePoints
            || !double.IsFinite(mark.Width) || mark.Width is < 1 or > 36 || mark.Text == null || mark.Text.Length > 1500
            || mark.Points.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y) || p.X < 0 || p.Y < 0 || p.X > Original.Right || p.Y > Original.Bottom)) return false;
        if (mark.Tool is not (MarkTool.Pen or MarkTool.Highlight or MarkTool.Text) && mark.Points.Count != 2) return false;
        if (mark.Tool == MarkTool.Text && (mark.Points.Count != 1 || string.IsNullOrWhiteSpace(mark.Text))) return false;
        if (mark.Tool == MarkTool.Arrow && Math.Abs(mark.Points[0].X - mark.Points[1].X) + Math.Abs(mark.Points[0].Y - mark.Points[1].Y) < 1) return false;
        if (mark.Tool is MarkTool.Rectangle or MarkTool.Ellipse or MarkTool.Obscure or MarkTool.Mosaic
            && ImageRegion.From(mark.Points[0], mark.Points[1], Original) == null) return false;
        if (mark.Tool == MarkTool.Mosaic && State.Marks.Count(m => m.Tool == MarkTool.Mosaic) >= 20) return false;
        Push(new ImageEditorState(State.Crop, Array.AsReadOnly(State.Marks.Append(mark).ToArray()))); return true;
    }

    public bool Crop(ImagePoint a, ImagePoint b)
    {
        var crop = ImageRegion.From(a, b, State.Crop);
        if (crop == null || crop.Value == State.Crop) return false;
        Push(State with { Crop = crop.Value }); return true;
    }
    public bool Undo() { if (!CanUndo) return false; redo.Add(State); State = undo[^1]; undo.RemoveAt(undo.Count - 1); return true; }
    public bool Redo() { if (!CanRedo) return false; undo.Add(State); State = redo[^1]; redo.RemoveAt(redo.Count - 1); return true; }
    public void MarkSaved() => saved = State;
    private void Push(ImageEditorState state)
    {
        undo.Add(State); if (undo.Count > 100) undo.RemoveAt(0); redo.Clear(); State = state;
    }
}
