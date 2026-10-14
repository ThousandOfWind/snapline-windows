using Snapline.Core;

internal static class EditorCoreChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var doc = new ImageEditorDocument(120, 90);
        check(doc.State.Crop == new ImageRegion(0, 0, 120, 90) && !doc.IsDirty, "editor starts with original pixel bounds and no edits");
        var points = new[] { new ImagePoint(5, 5), new ImagePoint(20, 10) };
        var pen = new ImageMark(MarkTool.Pen, points, 0xFFFF0000, 4); points[0] = new ImagePoint(99, 99);
        check(doc.Add(pen) && doc.State.Marks[0].Points[0] == new ImagePoint(5, 5), "annotation points are immutable snapshots");
        check(doc.IsDirty && doc.CanUndo, "annotation makes editor dirty and undoable");
        check(doc.Undo() && doc.State.Marks.Count == 0 && !doc.IsDirty, "undo returns clean original state");
        check(doc.Redo() && doc.State.Marks.Count == 1, "redo restores vector mark");
        doc.MarkSaved(); check(!doc.IsDirty, "saved snapshot clears dirty state");
        check(doc.Crop(new ImagePoint(90, 70), new ImagePoint(10, 8)) && doc.State.Crop == new ImageRegion(10, 8, 80, 62), "reverse crop drag normalizes exact pixel rectangle");
        check(doc.State.Marks[0].Points[0] == new ImagePoint(5, 5), "crop does not destructively translate or drop earlier marks");
        check(doc.Undo() && !doc.IsDirty && doc.State.Crop == doc.Original, "undo crop restores saved full image state");
        check(doc.Redo() && doc.IsDirty, "redo crop retains dirty comparison");
        check(!doc.Crop(new ImagePoint(20, 20), new ImagePoint(20.1, 20.1)), "tiny crop is not committed");
        check(doc.Crop(new ImagePoint(-100, -100), new ImagePoint(30, 30)) && doc.State.Crop == new ImageRegion(10, 8, 20, 22), "crop clamps to current visible image");
        var count = doc.State.Marks.Count;
        check(!doc.Add(new ImageMark(MarkTool.Text, [new ImagePoint(12, 12)], 0xFF000000, 4, "")), "empty text does not create hidden history item");
        check(!doc.Add(new ImageMark(MarkTool.Pen, [new ImagePoint(double.NaN, 1)], 0, 4)), "nonfinite annotation coordinate rejected");
        check(!doc.Add(new ImageMark(MarkTool.Rectangle, [new ImagePoint(1, 1), new ImagePoint(121, 50)], 0, 4)), "annotation outside original raster rejected");
        check(!doc.Add(new ImageMark(MarkTool.Pen, [new ImagePoint(1, 1)], 0, 0)), "invalid line width rejected");
        check(!doc.Add(new ImageMark(MarkTool.Arrow, [new ImagePoint(10, 10), new ImagePoint(10, 10)], 0, 4)), "zero-length arrow does not create an invisible edit");
        check(!doc.Add(new ImageMark(MarkTool.Text, [new ImagePoint(10, 10)], 0, 4, new string('x', 1501))), "oversized text is rejected");
        check(!doc.Crop(new ImagePoint(double.NaN, 10), new ImagePoint(20, 20)), "nonfinite crop coordinates rejected");
        check(doc.State.Marks.Count == count, "invalid edit attempts do not mutate history");
        doc.Undo(); check(doc.CanRedo, "undo makes redo available");
        doc.Add(new ImageMark(MarkTool.Text, [new ImagePoint(20, 20)], 0xFF000000, 3, "test"));
        check(!doc.CanRedo, "new edit clears stale redo branch");
        var limited = new ImageEditorDocument(20, 20);
        for (var n = 0; n < ImageEditorDocument.MaxMarks; n++) limited.Add(new ImageMark(MarkTool.Pen, [new ImagePoint(10, 10)], 0xFF000000, 1));
        check(!limited.Add(new ImageMark(MarkTool.Pen, [new ImagePoint(10, 10)], 0xFF000000, 1)), "annotation capacity is bounded");
        var undos = 0; while (limited.Undo()) undos++;
        check(undos == 100, "editor history is bounded to 100 snapshots");
        var rejected = false; try { _ = new ImageEditorDocument(0, 10); } catch (ArgumentOutOfRangeException) { rejected = true; }
        check(rejected, "invalid editor image size rejected");
    }
}
