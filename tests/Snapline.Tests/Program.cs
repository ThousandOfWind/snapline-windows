using Snapline.Core;

var directory = Path.Combine(Path.GetTempPath(), "Snapline-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
var passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAILED: " + name);
    Console.WriteLine("PASS: " + name); passed++;
}
try
{
    var a = Path.Combine(directory, "a.png");
    var b = Path.Combine(directory, "b.jpg");
    var c = Path.Combine(directory, "c.bmp");
    foreach (var path in new[] { a, b, c }) File.WriteAllText(path, "fixture");
    Check(ImageFiles.IsSupported("Screenshot.PNG"), "case-insensitive image extension");
    Check(!ImageFiles.IsSupported("credentials.txt"), "reject non-images");
    Check(ClipboardPolicy.AllowsImage(new[] { "Bitmap", "PNG" }), "image-only clipboard allowed");
    Check(!ClipboardPolicy.AllowsImage(new[] { "Bitmap", "UnicodeText" }), "mixed text clipboard rejected");
    Check(!ClipboardPolicy.AllowsImage(new[] { "Bitmap", "FileDrop" }), "mixed file clipboard rejected");
    Check(!ClipboardPolicy.AllowsImage(new[] { "Bitmap", "ExcludeClipboardContentFromMonitorProcessing" }), "private clipboard marker rejected");
    Check(!ClipboardPolicy.AllowsImage(new[] { "Bitmap", "CanIncludeInClipboardHistory" }), "history privacy marker rejected conservatively");
    Check(!ClipboardPolicy.AllowsImage(new[] { "Bitmap", "application/x-keepassxc" }), "password-manager marker rejected");
    Check(ImageFiles.IsInside(a, directory), "owned folder boundary");
    Check(!ImageFiles.IsInside(directory + "-else/a.png", directory), "reject sibling prefix");
    var library = new ImageLibrary(2);
    Check(library.Add(a), "add image");
    Check(!library.Add(a.ToUpperInvariant()), "deduplicate case-insensitively");
    Check(!library.Add(Path.Combine(directory, "note.txt")), "library rejects unsupported type");
    library.Add(b); library.Add(c);
    Check(library.Items.SequenceEqual(new[] { b, c }), "capacity evicts oldest reference");
    Check(File.Exists(a), "eviction does not delete source");
    Check(library.Remove(b), "remove reference");
    Check(File.Exists(b), "removal does not delete source");
    File.Delete(c); library.Prune();
    Check(library.Items.Count == 0, "prune missing files");
    var restored = new ImageLibrary(50, new[] { a, a, c });
    Check(restored.Items.Count == 1, "restore ignores duplicate and missing files");
    var store = new SettingsStore(Path.Combine(directory, "data"));
    Check(!store.Load().WatchClipboard, "clipboard is opt-in");
    var pocket = new WindowPocket(2);
    var firstWindow = new WindowIdentity(100, 1, 2, 10);
    var secondWindow = new WindowIdentity(200, 1, 3, 10);
    Check(pocket.Add(firstWindow), "session pocket accepts valid window identity");
    Check(!pocket.Add(firstWindow) && pocket.Count == 1, "pocket deduplicates same instance");
    Check(pocket.Add(secondWindow) && !pocket.CanAdd(new WindowIdentity(300, 2, 3, 20)), "full pocket refuses without evicting a live window");
    Check(!pocket.Add(default), "pocket rejects empty handle identity");
    Check(!pocket.Contains(firstWindow with { ProcessStarted = 99 }), "process restart cannot match saved session identity");
    Check(pocket.Remove(firstWindow) && pocket.Count == 1, "taking window removes only requested identity");
    Check(WindowDropPolicy.Accept(true, true, false, true, true, true), "deliberate titlebar release on rope accepted without dwell");
    Check(!WindowDropPolicy.Accept(true, true, true, true, true, true), "cancelled window move rejected");
    Check(!WindowDropPolicy.Accept(true, true, false, false, true, true), "resize/no-position-change is not a drop");
    Check(!WindowDropPolicy.Accept(true, true, false, true, false, true), "window body overlap alone not accepted");
    Check(!WindowDropPolicy.Accept(true, true, false, true, true, false), "cursor outside title region rejected");
    Check(!WindowDropPolicy.Accept(false, true, false, true, true, true), "disabled stash never accepts");
    Check(!WindowDropPolicy.Accept(true, false, false, true, true, true), "ineligible source never accepts");
    Check(WindowDragStartPolicy.Accept(1, 150, 25, 500, 1, true), "modern HTCLIENT title region accepted after native move start");
    Check(WindowDragStartPolicy.Accept(null, 150, 25, 500, 1, true), "bounded hit-test timeout can use validated header geometry");
    Check(!WindowDragStartPolicy.Accept(10, 150, 25, 500, 1, true), "resize hit cannot be reclassified as caption drag");
    Check(!WindowDragStartPolicy.Accept(8, 150, 25, 500, 1, true), "minimize-button hit excluded from drag");
    Check(!WindowDragStartPolicy.Accept(1, 2, 25, 500, 1, true), "header edge resize rejected even on custom client hit");
    Check(!WindowDragStartPolicy.Accept(1, 150, 180, 500, 1, true), "body click cannot arm fallback drag");
    Check(!WindowDragStartPolicy.Accept(2, 150, 25, 500, 1, false), "keyboard move cannot masquerade as mouse drag");
    Check(WindowDragFallbackPolicy.Accept(true, true, 500, 300, 500, 300, 45, 35, 45, 35, 1), "fallback requires window following held pointer");
    Check(!WindowDragFallbackPolicy.Accept(true, true, 500, 300, 550, 300, 45, 35, 45, 35, 1), "fallback excludes resizing");
    Check(!WindowDragFallbackPolicy.Accept(true, true, 500, 300, 500, 300, 45, 35, 0, 0, 1), "programmatic move without pointer motion excluded");
    Check(!WindowDragFallbackPolicy.Accept(true, false, 500, 300, 500, 300, 45, 35, 45, 35, 1), "fallback rechecks source identity");
    var photoTheme = HangingTheme.For(HangingKind.Image); var appTheme = HangingTheme.For(HangingKind.Window);
    var folderTheme = HangingTheme.For(HangingKind.FolderWindow);
    Check(photoTheme.ClipShape == "wood" && appTheme.ClipShape == "metal" && folderTheme.ClipShape == "brass", "hanging kinds have different material/clip semantics");
    Check(photoTheme.Edge != appTheme.Edge && appTheme.Edge != folderTheme.Edge, "kind-specific border palette differs");
    Check(appTheme.Header != folderTheme.Header && photoTheme.Paper != appTheme.Paper, "window and folder header/paper styles are independent");
    var springStart = SpringCurve.Sample(4, 10, 0, 0, 16, 0.28);
    Check(Math.Abs(springStart.Position - 4) < 1e-8 && Math.Abs(springStart.Velocity - 10) < 1e-8, "spring preserves initial position and momentum");
    var firstSwing = SpringCurve.Sample(0, 72, 0, 0.1, 16, 0.28).Position;
    var reverseSwing = SpringCurve.Sample(0, 72, 0, 0.32, 16, 0.28).Position;
    Check(firstSwing > 2 && reverseSwing < 0, "spring pendulum swings through equilibrium instead of staying tilted");
    Check(Math.Abs(SpringCurve.Sample(0, 72, 0, 2, 16, 0.28).Position) < 0.01, "spring motion decays instead of looping indefinitely");
    var retarget = SpringCurve.Sample(0, 72, 0, 0.1, 16, 0.28);
    var resumed = SpringCurve.Sample(retarget.Position, retarget.Velocity, 1, 0, 16, 0.28);
    Check(resumed == retarget, "retarget starts from current state without jumping");
    var rejectedSpring = false;
    try { SpringCurve.Sample(double.NaN, 0, 0, 0, 16, 0.28); } catch (ArgumentOutOfRangeException) { rejectedSpring = true; }
    Check(rejectedSpring, "invalid spring input is rejected");
    Check(PlacementMath.Offset(0.5, 400) == 200, "normalized position restores to available travel");
    Check(PlacementMath.Offset(2, 400) == 400 && PlacementMath.Offset(-1, 400) == 0, "placement clamps offscreen ratios");
    Check(PlacementMath.Offset(double.NaN, 400) == 0 && PlacementMath.Offset(double.PositiveInfinity, 400) == 0, "invalid coordinates recover safely");
    Check(PlacementMath.Offset(0.5, -20) == 0, "oversized component remains anchored on small monitor");
    Check(PlacementMath.Relative(200, 400) == 0.5 && PlacementMath.Relative(200, 0) == 0, "relative position handles zero travel");
    Check(!store.Load().ReduceMotion, "motion preference defaults compatibly");
    Check(store.Load().StashWindows, "requested window-stash feature enabled by default");
    Check(!store.Load().OverrideSystemAnimations, "system animation preference is respected by default");
    Check(store.Load().ShelfPosition == null && store.Load().HandlePosition == null, "older settings retain default component positions");
    var preferences = new Preferences { WatchClipboard = true, ReduceMotion = true, OverrideSystemAnimations = true, StashWindows = false, Items = [a], WatchFolders = [directory],
        ShelfPosition = new WindowPosition { Screen = "test-monitor", X = 0.25, Y = 0.5 },
        HandlePosition = new WindowPosition { Screen = "test-monitor", X = 0.75, Y = 0.125 } };
    store.Save(preferences);
    var loaded = store.Load();
    Check(loaded.WatchClipboard && loaded.Items.Single() == a && loaded.WatchFolders.Single() == directory, "settings round trip");
    Check(loaded.ReduceMotion, "reduced motion preference persists");
    Check(loaded.OverrideSystemAnimations, "explicit application animation override persists");
    Check(!loaded.StashWindows, "window-stash opt-out persists");
    Check(loaded.ShelfPosition?.X == 0.25 && loaded.ShelfPosition.Y == 0.5 && loaded.HandlePosition?.Y == 0.125, "independent component positions persist");
    Check(!File.Exists(store.SettingsPath + ".tmp"), "atomic settings save leaves no temporary file");
    File.WriteAllText(store.SettingsPath, "{broken json");
    Check(!store.Load().WatchClipboard, "corrupt settings recover with safe defaults");
    File.WriteAllText(store.SettingsPath, "{\"Items\":null,\"WatchFolders\":null,\"Capacity\":9999}");
    loaded = store.Load();
    Check(loaded.Items.Count == 0 && loaded.WatchFolders.Count == 0 && loaded.Capacity == 100, "sanitize settings");
    restored.Clear();
    Check(File.Exists(a) && restored.Items.Count == 0, "clear preserves files");
    GroupCoreChecks.Run(directory, Check);
    EditorCoreChecks.Run(Check);
    Console.WriteLine($"\n{passed} tests passed.");
    return 0;
}
finally { Directory.Delete(directory, recursive: true); }
