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
    Check(PlacementMath.Offset(0.5, 400) == 200, "normalized position restores to available travel");
    Check(PlacementMath.Offset(2, 400) == 400 && PlacementMath.Offset(-1, 400) == 0, "placement clamps offscreen ratios");
    Check(PlacementMath.Offset(double.NaN, 400) == 0 && PlacementMath.Offset(double.PositiveInfinity, 400) == 0, "invalid coordinates recover safely");
    Check(PlacementMath.Offset(0.5, -20) == 0, "oversized component remains anchored on small monitor");
    Check(PlacementMath.Relative(200, 400) == 0.5 && PlacementMath.Relative(200, 0) == 0, "relative position handles zero travel");
    Check(store.Load().ShelfPosition == null && store.Load().HandlePosition == null, "older settings retain default component positions");
    var preferences = new Preferences { WatchClipboard = true, Items = [a], WatchFolders = [directory],
        ShelfPosition = new WindowPosition { Screen = "test-monitor", X = 0.25, Y = 0.5 },
        HandlePosition = new WindowPosition { Screen = "test-monitor", X = 0.75, Y = 0.125 } };
    store.Save(preferences);
    var loaded = store.Load();
    Check(loaded.WatchClipboard && loaded.Items.Single() == a && loaded.WatchFolders.Single() == directory, "settings round trip");
    Check(loaded.ShelfPosition?.X == 0.25 && loaded.ShelfPosition.Y == 0.5 && loaded.HandlePosition?.Y == 0.125, "independent component positions persist");
    Check(!File.Exists(store.SettingsPath + ".tmp"), "atomic settings save leaves no temporary file");
    File.WriteAllText(store.SettingsPath, "{broken json");
    Check(!store.Load().WatchClipboard, "corrupt settings recover with safe defaults");
    File.WriteAllText(store.SettingsPath, "{\"Items\":null,\"WatchFolders\":null,\"Capacity\":9999}");
    loaded = store.Load();
    Check(loaded.Items.Count == 0 && loaded.WatchFolders.Count == 0 && loaded.Capacity == 100, "sanitize settings");
    restored.Clear();
    Check(File.Exists(a) && restored.Items.Count == 0, "clear preserves files");
    Console.WriteLine($"\n{passed} tests passed.");
    return 0;
}
finally { Directory.Delete(directory, recursive: true); }
