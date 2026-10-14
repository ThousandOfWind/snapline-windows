using Snapline.Core;

internal static class CardInteractionChecks
{
    internal static void Run(Action<bool, string> check)
    {
        CardGesture Make() => new(500, 6, 6, 4, 4);
        var p = new ImagePoint(100, 100);
        var g = Make();
        check(!g.Press(100, p) && g.IsPressed, "first click arms pointer but is not a double click");
        check(!g.Move(new ImagePoint(101, 101)), "small jitter does not start image drag");
        check(g.Release(150) == CardRelease.PendingSingle && g.WaitForSingle(150) == 450, "single click waits only remaining double-click interval");
        check(g.CompleteSingle() && !g.CompleteSingle(), "single-click callback can run exactly once");
        g = Make(); g.Press(100, p); g.Release(140);
        check(g.Press(250, p) && !g.CompleteSingle(), "second press suppresses pending single action");
        check(g.Release(290) == CardRelease.DoubleClick && !g.CompleteSingle(), "double click commits on release and never copies afterward");
        g = Make(); g.Press(100, p); g.Release(140); g.Press(250, p);
        check(g.Move(new ImagePoint(120, 110)) && g.Release(300) == CardRelease.None && !g.CompleteSingle(), "second-press drag cancels both click actions");
        g = Make(); g.Press(100, p); g.Release(140);
        check(!g.Press(700, p) && g.Release(740) == CardRelease.PendingSingle, "click outside time window stays a single click");
        g = Make(); g.Press(100, p); g.Release(140);
        check(!g.Press(250, new ImagePoint(130, 100)), "distant second click is not a double click");
        g.Cancel(); check(!g.IsPressed && !g.CompleteSingle(), "cancel clears capture and pending click");
        g = Make(); g.Press(100, p); g.Release(140);
        check(!g.Press(50, p), "backward timestamp does not synthesize double click");
        var wide = ImageCardSize.Fit(640, 360, 170, 148);
        check(Math.Abs(wide.Width / wide.Height - 640.0 / 360) < 1e-8 && wide.Width == 170, "landscape thumbnail fits exact aspect ratio without letterbox");
        var tall = ImageCardSize.Fit(200, 800, 170, 148);
        check(tall.Height == 148 && tall.Width == 37, "portrait thumbnail narrows rather than filling a fixed canvas");
        var square = ImageCardSize.Fit(500, 500, 170, 148);
        check(square.Width == 148 && square.Height == 148, "square thumbnail does not acquire synthetic margins");
        var bad = false; try { ImageCardSize.Fit(0, 5, 170, 148); } catch (ArgumentOutOfRangeException) { bad = true; }
        check(bad, "invalid thumbnail dimensions rejected");
    }
}
