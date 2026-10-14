using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

internal static class Program
{
    private static int passed;
    private static Process app = null!;
    private static string directory = "";
    private static bool clipboardOwned;
    private static readonly List<string> results = [];

    [STAThread]
    private static int Main(string[] args)
    {
        Native.UsePhysicalPixels();
        if (args.Length != 2) { Console.Error.WriteLine("Usage: E2E <Snapline.exe> <report.json>"); return 2; }
        if (Process.GetProcessesByName("Snapline").Length > 0) { Console.Error.WriteLine("Quit Snapline before E2E; no existing process will be killed."); return 2; }
        var exe = Path.GetFullPath(args[0]);
        directory = Path.Combine(Path.GetTempPath(), "Snapline-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var cursor = Native.Cursor;
        string? failure = null;
        try
        {
            // Launch a separate packaged application: real tray/hotkeys/watchers; only test files and synthetic capture.
            app = Process.Start(new ProcessStartInfo(exe) { ArgumentList = { "--e2e-data-dir", directory } })!;
            Wait(() => Window("Snapline") != IntPtr.Zero, "normal startup visibly opens empty shelf");
            Wait(() => File.Exists(SettingsPath), "startup saves settings");
            Check(!Settings().GetProperty("WatchClipboard").GetBoolean(), "clipboard starts disabled");
            var shelf = Window("Snapline");
            var element = AutomationElement.FromHandle(shelf);
            Check(Find(element, "截图，挂在这里。\n按 Ctrl+Alt+S 截图，或从托盘添加图片。") != null, "empty shelf instructions rendered via UI Automation");
            Check(Invoke(element, "收起"), "real UI hide button is invokable");
            Wait(() => Window("Snapline") == IntPtr.Zero, "hide removes native window from visible desktop");
            Wait(() => Window("Snapline Handle") != IntPtr.Zero, "hidden shelf leaves visible recovery handle");
            using (var relaunch = Process.Start(new ProcessStartInfo(exe))!)
            {
                Check(relaunch.WaitForExit(5000) && relaunch.ExitCode == 0, "double launch forwards to running instance");
            }
            Wait(() => Window("Snapline") != IntPtr.Zero, "double launch reveals existing shelf");
            Check(Process.GetProcessesByName("Snapline").Length == 1, "only one application instance");

            Native.Move(Forms.Screen.PrimaryScreen!.Bounds.Width / 2, 400);
            Native.Hotkey(0x54);
            Wait(() => Window("Snapline") == IntPtr.Zero, "physical Ctrl+Alt+T hides shelf");
            Native.Hotkey(0x54);
            Wait(() => Window("Snapline") != IntPtr.Zero, "physical Ctrl+Alt+T opens shelf");

            var fixture = Path.Combine(directory, "Inbox", "E2E-fixture.png");
            GenerateImage(fixture);
            Wait(() => Items().Contains(fixture), "real filesystem watcher imports generated PNG");
            Wait(() => Find(AutomationElement.FromHandle(Window("Snapline")), "E2E-fixture.png") != null, "watched thumbnail visible in UI");
            Thread.Sleep(1000);
            var card = Find(AutomationElement.FromHandle(Window("Snapline")), "E2E-fixture.png")!;
            var rect = card.Current.BoundingRectangle;
            Native.Click((int)(rect.Left + rect.Width / 2), (int)(rect.Top + rect.Height / 2));
            Wait(() => Find(AutomationElement.FromHandle(Window("Snapline")), "已复制图片") != null, "single click reports successful copy before reading test clipboard");
            clipboardOwned = true;
            Wait(() => OwnImageDimensions(640, 360), "physical single click copies full-resolution generated image");
            Check(Items().Length == 1, "copy does not duplicate card");
            Native.Message(app.Id, "Snapline.Messages", 0x8002, 0);
            var rendered = Path.Combine(directory, "shelf-render.png");
            Wait(() => File.Exists(rendered), "render transparent shelf without capturing desktop");
            Check(TransparentFraction(rendered) > 0.70, "over 70 percent of shelf pixels are fully transparent");
            File.Copy(rendered, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1]))!, "shelf-preview.png"), overwrite: true);
            var shelfRect = Native.Bounds(Window("Snapline"));
            Check(Native.HitTest(Window("Snapline"), shelfRect.Left + 60, shelfRect.Top + 110) == -1,
                "empty transparent area does not intercept pointer hit-testing");

            Native.DoubleClick((int)(rect.Left + rect.Width / 2), (int)(rect.Top + rect.Height / 2));
            Wait(() => Window("E2E-fixture.png — Snapline") != IntPtr.Zero, "physical double click opens preview");
            var preview = AutomationElement.FromHandle(Window("E2E-fixture.png — Snapline"));
            ((WindowPattern)preview.GetCurrentPattern(WindowPattern.Pattern)).Close();
            Wait(() => Window("E2E-fixture.png — Snapline") == IntPtr.Zero, "preview closes cleanly");

            using var fullscreenFixture = new Forms.Form
            {
                Text = "Synthetic fullscreen E2E fixture", FormBorderStyle = Forms.FormBorderStyle.None,
                Bounds = Forms.Screen.PrimaryScreen!.Bounds, BackColor = System.Drawing.Color.MediumAquamarine,
                ShowInTaskbar = false, StartPosition = Forms.FormStartPosition.Manual
            };
            fullscreenFixture.Show(); fullscreenFixture.Activate(); Forms.Application.DoEvents();
            Native.Hotkey(0x53);
            Wait(() => Window("Snapline Capture") != IntPtr.Zero, "physical Ctrl+Alt+S opens region selector with synthetic desktop");
            var capture = Native.Bounds(Window("Snapline Capture"));
            Native.Drag(capture.Left + 100, capture.Top + 100, capture.Left + 420, capture.Top + 280);
            Wait(() => Directory.GetFiles(Path.Combine(directory, "Inbox"), "Capture-*.png").Length == 1, "physical region drag saves selected image");
            var saved = Directory.GetFiles(Path.Combine(directory, "Inbox"), "Capture-*.png").Single();
            var bitmap = new BitmapImage();
            using (var input = File.OpenRead(saved))
            {
                bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = input; bitmap.EndInit(); bitmap.Freeze();
            }
            Check(bitmap.PixelWidth > 3 && bitmap.PixelWidth < 640 && bitmap.PixelHeight > 3 && bitmap.PixelHeight < 360, "capture saves cropped synthetic image, not full desktop");
            Wait(() => Items().Length == 2, "captured image hangs once");
            Wait(() => Window("Snapline") != IntPtr.Zero, "successful capture restores visible shelf automatically");
            Thread.Sleep(5500);
            Check(!app.HasExited && Window("Snapline") != IntPtr.Zero,
                "capture stays visible beyond old timeout even over fullscreen fixture");
            var capturedLabel = Find(AutomationElement.FromHandle(Window("Snapline")), Path.GetFileName(saved))!;
            var capturedRect = capturedLabel.Current.BoundingRectangle;
            Native.Click((int)(capturedRect.Left + capturedRect.Width / 2), (int)(capturedRect.Top + capturedRect.Height / 2));
            Wait(() => Find(AutomationElement.FromHandle(Window("Snapline")), "已复制图片") != null,
                "captured card remains visibly clickable after waiting");
            for (var cycle = 0; cycle < 5; cycle++)
            {
                Native.Message(app.Id, "Snapline.Messages", 0x0312, 1);
                Thread.Sleep(40);
                Native.Message(app.Id, "Snapline.Messages", 0x8001, 0);
                Thread.Sleep(70);
            }
            Thread.Sleep(400);
            Check(Window("Snapline") != IntPtr.Zero && Find(AutomationElement.FromHandle(Window("Snapline")), Path.GetFileName(saved))?.Current.IsOffscreen == false,
                "rapid hide/reveal never leaves content animated offscreen");
            fullscreenFixture.Close(); Forms.Application.DoEvents();

            Native.Hotkey(0x53);
            Wait(() => Window("Snapline Capture") != IntPtr.Zero, "second capture opens");
            Native.Key(0x1B);
            Wait(() => Window("Snapline Capture") == IntPtr.Zero, "Esc cancels capture");
            Check(Directory.GetFiles(Path.Combine(directory, "Inbox"), "Capture-*.png").Length == 1, "cancel saves no file");
            Wait(() => Window("Snapline") != IntPtr.Zero, "cancel automatically restores shelf without an extra hotkey");
            Thread.Sleep(3500);
            Check(Window("Snapline") != IntPtr.Zero, "cancelled capture leaves shelf pinned and discoverable");
            Native.Hotkey(0x54);
            Wait(() => Window("Snapline") == IntPtr.Zero, "post-capture hotkey can explicitly hide shelf");
            Wait(() => Window("Snapline Handle") != IntPtr.Zero, "post-capture hide exposes recovery handle");
            var recovery = AutomationElement.FromHandle(Window("Snapline Handle"));
            var recoveryButton = recovery.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
            var recoveryRect = recoveryButton.Current.BoundingRectangle;
            Native.Click((int)(recoveryRect.Left + recoveryRect.Width / 2), (int)(recoveryRect.Top + recoveryRect.Height / 2));
            Wait(() => Window("Snapline") != IntPtr.Zero, "physical handle click restores shelf after capture");
            Check(Window("Snapline Handle") == IntPtr.Zero, "recovery handle hides when shelf opens");
            Check(Invoke(AutomationElement.FromHandle(Window("Snapline")), "截图"), "toolbar capture button invokes real capture workflow");
            Wait(() => Window("Snapline Capture") != IntPtr.Zero, "toolbar capture opens selector");
            var toolbarCapture = Native.Bounds(Window("Snapline Capture"));
            Native.Drag(toolbarCapture.Left + 160, toolbarCapture.Top + 140, toolbarCapture.Left + 400, toolbarCapture.Top + 300);
            Wait(() => Items().Length == 3, "toolbar capture persists image");
            Wait(() => Window("Snapline") != IntPtr.Zero, "toolbar capture restores shelf automatically");
            Thread.Sleep(4000);
            Check(Window("Snapline") != IntPtr.Zero, "toolbar capture stays visible instead of disappearing");
            File.Delete(Items().Last());
            Wait(() => Items().Length == 2, "toolbar synthetic fixture cleanup prunes only test record");

            Native.Message(app.Id, "Snapline.Messages", 0x8001, 0);
            Thread.Sleep(1000);
            AutomationElement[] removeButtons = [];
            Wait(() =>
            {
                element = AutomationElement.FromHandle(Window("Snapline"));
                var buttons = element.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
                removeButtons = buttons.Cast<AutomationElement>().Where(b => b.Current.AutomationId == "RemoveCard").ToArray();
                return removeButtons.Length == 2;
            }, "both cards have remove controls");
            ((InvokePattern)removeButtons[0].GetCurrentPattern(InvokePattern.Pattern)).Invoke();
            Wait(() => Items().Length == 1, "real UI remove updates persisted library");
            Check(File.Exists(saved) && File.Exists(fixture), "UI removal preserves source files");
            Check(!File.Exists(Path.Combine(directory, "error.log")), "no application exceptions during interaction");

            var shelfBounds = Native.Bounds(Window("Snapline"));
            Native.Move(shelfBounds.Left + 80, shelfBounds.Top + 80);
            Thread.Sleep(400);
            Native.Move(shelfBounds.Left + 80, shelfBounds.Bottom + 100);
            Thread.Sleep(1400);
            Check(Window("Snapline") != IntPtr.Zero, "manual reveal stays pinned after cursor visits then leaves");
            Native.Hotkey(0x54);
            Wait(() => Window("Snapline") == IntPtr.Zero, "manual pin only hides on explicit command");
            Wait(() =>
            {
                var screen = Forms.Screen.PrimaryScreen!.Bounds;
                Native.Move(screen.Left + 80, screen.Top + 1);
                return Window("Snapline") != IntPtr.Zero;
            }, "physical top-edge hover reveals existing cards");
            Thread.Sleep(350);
            Native.Move(shelfBounds.Left + 80, shelfBounds.Bottom + 100);
            Wait(() => Window("Snapline") == IntPtr.Zero, "hover-only reveal still auto-hides when moving away");

            var remaining = Items().Single();
            Native.OpenTrayMenu(app.Id); Thread.Sleep(400);
            var trayMenu = AutomationElement.FromHandle(Native.FindClass(app.Id, "WindowsForms10.Window"));
            Check(Invoke(trayMenu, "监听剪贴板图片（可选）"), "real tray menu enables opt-in clipboard monitoring");
            Wait(() => Settings().GetProperty("WatchClipboard").GetBoolean(), "opt-in preference persists");
            var copiedImage = new BitmapImage();
            using (var input = File.OpenRead(fixture))
            {
                copiedImage.BeginInit(); copiedImage.CacheOption = BitmapCacheOption.OnLoad; copiedImage.StreamSource = input; copiedImage.EndInit(); copiedImage.Freeze();
            }
            Clipboard.SetImage(copiedImage);
            Wait(() => Items().Length == 2, "real clipboard notification imports generated image once");
            Thread.Sleep(1000);
            Check(Items().Length == 2, "clipboard watcher does not loop");
            Native.Message(app.Id, "Snapline.Messages", 0x8001, 0);
            Wait(() => Window("Snapline") != IntPtr.Zero, "shelf opens with clipboard capture");
            Thread.Sleep(600);
            var newestName = Path.GetFileName(Items().Last());
            var newest = Find(AutomationElement.FromHandle(Window("Snapline")), newestName)!;
            var newestRect = newest.Current.BoundingRectangle;
            Native.Click((int)(newestRect.Left + newestRect.Width / 2), (int)(newestRect.Top + newestRect.Height / 2));
            Wait(() => Find(AutomationElement.FromHandle(Window("Snapline")), "已复制图片") != null, "copy remains usable while clipboard monitoring enabled");
            Thread.Sleep(800);
            Check(Items().Length == 2, "own copy is not recaptured");
            var mixed = new DataObject(); mixed.SetImage(copiedImage); mixed.SetText("Synthetic E2E text");
            Clipboard.SetDataObject(mixed, true); Thread.Sleep(800);
            Check(Items().Length == 2, "real mixed image/text clipboard is skipped");
            var marked = new DataObject(); marked.SetImage(copiedImage); marked.SetData("ExcludeClipboardContentFromMonitorProcessing", new byte[] { 1 });
            Clipboard.SetDataObject(marked, true); Thread.Sleep(800);
            Check(Items().Length == 2, "real private-marker image clipboard is skipped");
            Native.OpenTrayMenu(app.Id); Thread.Sleep(400);
            trayMenu = AutomationElement.FromHandle(Native.FindClass(app.Id, "WindowsForms10.Window"));
            Check(Invoke(trayMenu, "监听剪贴板图片（可选）"), "real tray menu disables clipboard monitoring");
            Wait(() => !Settings().GetProperty("WatchClipboard").GetBoolean(), "clipboard monitoring returns to disabled");
            File.Delete(Items().Last());
            Wait(() => Items().Length == 1, "external fixture deletion prunes missing reference");
            Check(Items().Single() == remaining, "remaining record unchanged after clipboard tests");

            Native.Message(app.Id, "Snapline.Messages", 0x8001, 0);
            Wait(() => Window("Snapline") != IntPtr.Zero, "shelf opens for image drag visual check");
            Thread.Sleep(1200);
            var dragLabel = Find(AutomationElement.FromHandle(Window("Snapline")), Path.GetFileName(remaining))!;
            var dragRect = dragLabel.Current.BoundingRectangle;
            Native.BeginDrag((int)(dragRect.Left + dragRect.Width / 2), (int)(dragRect.Top + dragRect.Height / 2));
            Wait(() => Window("Snapline Drag Preview") != IntPtr.Zero, "physical image drag displays floating photo preview");
            var previewBefore = Native.Bounds(Window("Snapline Drag Preview"));
            Native.Move(Native.Cursor.X + 80, Native.Cursor.Y + 60);
            Wait(() => Native.Bounds(Window("Snapline Drag Preview")).Left != previewBefore.Left, "drag thumbnail follows pointer during native drag loop");
            Native.Key(0x1B); Native.ReleaseLeftButton();
            Wait(() => Window("Snapline Drag Preview") == IntPtr.Zero, "Esc cancels native drag and closes thumbnail");
            Check(File.Exists(remaining) && Items().Single() == remaining, "cancelled image drag preserves library and source file");
            Wait(() => Find(AutomationElement.FromHandle(Window("Snapline")), Path.GetFileName(remaining))?.Current.IsOffscreen == false,
                "source thumbnail remains usable after native drag cancel");

            Native.Message(app.Id, "Snapline.Messages", 0x8001, 0);
            Wait(() => Window("Snapline") != IntPtr.Zero, "shelf visible before component drag");
            var beforeMove = Native.Bounds(Window("Snapline"));
            var grip = AutomationElement.FromHandle(Window("Snapline")).FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty, "ShelfDragGrip"));
            var gripRect = grip.Current.BoundingRectangle;
            Native.Drag((int)(gripRect.Left + gripRect.Width / 2), (int)(gripRect.Top + gripRect.Height / 2),
                (int)(gripRect.Left + gripRect.Width / 2) + 180, (int)(gripRect.Top + gripRect.Height / 2) + 180);
            Wait(() => Settings().GetProperty("ShelfPosition").ValueKind == JsonValueKind.Object, "physical grip drag saves shelf position");
            var movedShelf = Native.Bounds(Window("Snapline"));
            Check(movedShelf.Top > beforeMove.Top + 50 && movedShelf.Left > beforeMove.Left + 50,
                "grip moves shelf both horizontally and vertically");
            Check(movedShelf.Right - movedShelf.Left < beforeMove.Right - beforeMove.Left, "first drag converts full-width shelf to movable floating width");
            Check(Items().Single() == remaining, "moving component does not share or remove image files");
            Native.Hotkey(0x54);
            Wait(() => Window("Snapline Handle") != IntPtr.Zero && Window("Snapline") == IntPtr.Zero, "hide floating shelf exposes handle");
            var beforeHandle = Native.Bounds(Window("Snapline Handle"));
            Native.Drag((beforeHandle.Left + beforeHandle.Right) / 2, (beforeHandle.Top + beforeHandle.Bottom) / 2,
                (beforeHandle.Left + beforeHandle.Right) / 2 - 220, (beforeHandle.Top + beforeHandle.Bottom) / 2 + 180);
            Wait(() => Settings().GetProperty("HandlePosition").ValueKind == JsonValueKind.Object, "physical handle drag saves independent position");
            var movedHandle = Native.Bounds(Window("Snapline Handle"));
            Check(movedHandle.Left < beforeHandle.Left - 50 && movedHandle.Top > beforeHandle.Top + 50, "collapsed handle moves both axes");
            Check(Window("Snapline") == IntPtr.Zero, "handle dragging does not accidentally open shelf");
            Native.Click((movedHandle.Left + movedHandle.Right) / 2, (movedHandle.Top + movedHandle.Bottom) / 2);
            Wait(() => Window("Snapline") != IntPtr.Zero, "moved handle still opens shelf with a click");
            Check(SamePosition(movedShelf, Native.Bounds(Window("Snapline"))), "shelf keeps dragged position after hide/reveal");
            Native.Hotkey(0x53);
            Wait(() => Window("Snapline Capture") != IntPtr.Zero, "capture remains available from moved shelf");
            Native.Key(0x1B);
            Wait(() => Window("Snapline") != IntPtr.Zero && Window("Snapline Capture") == IntPtr.Zero, "capture cancel restores moved shelf");
            Wait(() => SamePosition(movedShelf, Native.Bounds(Window("Snapline"))), "capture restoration respects dragged position");

            // Stop via the application's own WinForms tray menu, without traversing unrelated shell content.
            QuitViaTray();
            Check(app.WaitForExit(5000), "tray exit shuts down application");
            app = Process.Start(new ProcessStartInfo(exe) { ArgumentList = { "--e2e-data-dir", directory } })!;
            Wait(() => Window("Snapline") != IntPtr.Zero, "restart visibly opens shelf");
            Wait(() => Find(AutomationElement.FromHandle(Window("Snapline")), Path.GetFileName(Items().Single())) != null, "restart restores remaining thumbnail");
            Wait(() => SamePosition(movedShelf, Native.Bounds(Window("Snapline"))), "restart restores saved shelf position");
            Native.Hotkey(0x54);
            Wait(() => Window("Snapline Handle") != IntPtr.Zero, "restart still exposes handle on hide");
            Wait(() => SamePosition(movedHandle, Native.Bounds(Window("Snapline Handle"))), "restart restores saved handle position independently");
            Native.OpenTrayMenu(app.Id); Thread.Sleep(400);
            trayMenu = AutomationElement.FromHandle(Native.FindClass(app.Id, "WindowsForms10.Window"));
            Check(Invoke(trayMenu, "重置组件位置"), "tray menu resets component positions");
            Wait(() => Settings().GetProperty("ShelfPosition").ValueKind == JsonValueKind.Null
                && Settings().GetProperty("HandlePosition").ValueKind == JsonValueKind.Null, "reset clears both saved placements");
            Native.Message(app.Id, "Snapline.Messages", 0x8001, 0);
            Wait(() => Window("Snapline") != IntPtr.Zero, "reset shelf can be shown");
            var resetShelf = Native.Bounds(Window("Snapline"));
            var screenBounds = Forms.Screen.PrimaryScreen!.Bounds;
            Check(resetShelf.Left == screenBounds.Left && resetShelf.Top == screenBounds.Top,
                "reset returns shelf to default top edge");
            QuitViaTray();
            Check(app.WaitForExit(5000), "second clean tray exit");
        }
        catch (Exception e) { failure = e.GetType().Name + ": " + e.Message; Console.Error.WriteLine(failure); }
        finally
        {
            if (app != null && !app.HasExited) { app.Kill(); app.WaitForExit(5000); }
            Native.Move(cursor.X, cursor.Y);
            // Never read or preserve pre-existing clipboard. Clear only the known test image we wrote.
            if (clipboardOwned) { try { Clipboard.Clear(); } catch (ExternalException) { } }
            File.WriteAllText(Path.GetFullPath(args[1]), JsonSerializer.Serialize(new { passed, results, failure, syntheticDesktop = true, existingClipboardRead = false }, new JsonSerializerOptions { WriteIndented = true }));
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        Console.WriteLine($"{passed} E2E checks passed.");
        return failure == null ? 0 : 1;
    }

    private static bool SamePosition(Native.Rect expected, Native.Rect actual) =>
        Math.Abs(expected.Left - actual.Left) <= 3 && Math.Abs(expected.Top - actual.Top) <= 3;

    private static string SettingsPath => Path.Combine(directory, "settings.json");
    private static JsonElement Settings() { using var doc = JsonDocument.Parse(File.ReadAllText(SettingsPath)); return doc.RootElement.Clone(); }
    private static string[] Items() => Settings().GetProperty("Items").EnumerateArray().Select(i => i.GetString()!).ToArray();
    private static IntPtr Window(string title) => Native.Find(app.Id, title, visibleOnly: true);
    private static AutomationElement? Find(AutomationElement root, string name) => root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, name));
    private static bool Invoke(AutomationElement root, string name)
    {
        var control = Find(root, name);
        if (control == null || !control.TryGetCurrentPattern(InvokePattern.Pattern, out var pattern)) return false;
        ((InvokePattern)pattern).Invoke(); return true;
    }
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception("FAILED: " + name);
        results.Add(name); passed++; Console.WriteLine("PASS: " + name);
    }
    private static void Wait(Func<bool> condition, string name)
    {
        var stop = Stopwatch.StartNew();
        while (stop.ElapsedMilliseconds < 7000)
        {
            try { if (condition()) { Check(true, name); return; } }
            catch (Exception e) when (e is IOException or ElementNotAvailableException or InvalidOperationException or JsonException) { }
            Forms.Application.DoEvents();
            Thread.Sleep(100);
        }
        throw new Exception($"TIMEOUT: {name}; test cursor={Native.Cursor.X},{Native.Cursor.Y}");
    }
    private static bool OwnImageDimensions(int width, int height)
    {
        try { var image = Clipboard.GetImage(); return image?.PixelWidth == width && image.PixelHeight == height; }
        catch (ExternalException) { return false; }
    }
    private static double TransparentFraction(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = new System.Windows.Media.Imaging.FormatConvertedBitmap(decoder.Frames[0], System.Windows.Media.PixelFormats.Bgra32, null, 0);
        var pixels = new byte[frame.PixelWidth * frame.PixelHeight * 4];
        frame.CopyPixels(pixels, frame.PixelWidth * 4, 0);
        var transparent = 0;
        for (var i = 3; i < pixels.Length; i += 4) if (pixels[i] == 0) transparent++;
        return (double)transparent / (frame.PixelWidth * frame.PixelHeight);
    }

    private static void GenerateImage(string path)
    {
        using var bitmap = new System.Drawing.Bitmap(640, 360);
        using var g = System.Drawing.Graphics.FromImage(bitmap);
        g.Clear(System.Drawing.Color.MediumAquamarine);
        g.FillRectangle(System.Drawing.Brushes.Navy, 70, 60, 200, 160);
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
    private static void QuitViaTray()
    {
        // Open only the NotifyIcon's own context menu by its callback window (no shell-wide UI scraping).
        Native.OpenTrayMenu(app.Id);
        Thread.Sleep(400);
        var window = Native.FindClass(app.Id, "WindowsForms10.Window");
        if (window == IntPtr.Zero) throw new Exception("Tray context menu not found");
        var menu = AutomationElement.FromHandle(window);
        var exit = Find(menu, "退出");
        if (exit == null) throw new Exception("Tray exit item not found");
        if (exit.TryGetCurrentPattern(InvokePattern.Pattern, out var pattern)) ((InvokePattern)pattern).Invoke();
        else { var r = exit.Current.BoundingRectangle; Native.Click((int)(r.Left + r.Width / 2), (int)(r.Top + r.Height / 2)); }
    }
}

internal static class Native
{
    [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    internal static void UsePhysicalPixels() => SetThreadDpiAwarenessContext(new IntPtr(-4));
    private delegate bool Callback(IntPtr hwnd, IntPtr state);
    [DllImport("user32.dll")] private static extern bool EnumWindows(Callback callback, IntPtr state);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder text, int size);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    internal static int HitTest(IntPtr hwnd, int x, int y) => (int)SendMessage(hwnd, 0x0084, IntPtr.Zero, (IntPtr)((y << 16) | (x & 0xffff)));
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
    internal static Point Cursor { get { GetCursorPos(out var p); return p; } }
    internal static Rect Bounds(IntPtr hwnd) { GetWindowRect(hwnd, out var r); return r; }
    internal static void Move(int x, int y) => SetCursorPos(x, y);
    internal static IntPtr Find(int pid, string title, bool visibleOnly = false) => Search(pid, w =>
    {
        var t = new StringBuilder(256); GetWindowText(w, t, 256);
        return t.ToString() == title && (!visibleOnly || IsWindowVisible(w));
    });
    internal static IntPtr FindClass(int pid, string prefix) => Search(pid, w =>
    {
        var name = new StringBuilder(256); GetClassName(w, name, 256);
        return IsWindowVisible(w) && name.ToString().StartsWith(prefix, StringComparison.Ordinal);
    });
    private static IntPtr Search(int pid, Func<IntPtr, bool> test)
    {
        var result = IntPtr.Zero;
        EnumWindows((w, _) => { GetWindowThreadProcessId(w, out var p); if (p == pid && test(w)) { result = w; return false; } return true; }, IntPtr.Zero);
        return result;
    }
    internal static void Message(int pid, string title, uint message, int value) => PostMessage(Find(pid, title), message, (IntPtr)value, IntPtr.Zero);
    internal static void Hotkey(byte key)
    {
        keybd_event(0x11, 0, 0, UIntPtr.Zero); keybd_event(0x12, 0, 0, UIntPtr.Zero);
        keybd_event(key, 0, 0, UIntPtr.Zero); keybd_event(key, 0, 2, UIntPtr.Zero);
        keybd_event(0x12, 0, 2, UIntPtr.Zero); keybd_event(0x11, 0, 2, UIntPtr.Zero);
    }
    internal static void Key(byte key) { keybd_event(key, 0, 0, UIntPtr.Zero); keybd_event(key, 0, 2, UIntPtr.Zero); }
    internal static void Click(int x, int y) { Move(x, y); Thread.Sleep(50); mouse_event(2, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(60); mouse_event(4, 0, 0, 0, UIntPtr.Zero); }
    internal static void DoubleClick(int x, int y) { Click(x, y); Thread.Sleep(70); Click(x, y); }
    internal static void BeginDrag(int x, int y)
    {
        Move(x, y); Thread.Sleep(80); mouse_event(2, 0, 0, 0, UIntPtr.Zero);
        for (var step = 1; step <= 10; step++) { Move(x + step * 5, y + step * 6); Thread.Sleep(25); }
    }
    internal static void ReleaseLeftButton() => mouse_event(4, 0, 0, 0, UIntPtr.Zero);

    internal static void Drag(int x, int y, int targetX, int targetY)
    {
        Move(x, y); Thread.Sleep(100); mouse_event(2, 0, 0, 0, UIntPtr.Zero);
        for (var i = 1; i <= 12; i++) { Move(x + (targetX - x) * i / 12, y + (targetY - y) * i / 12); Thread.Sleep(30); }
        mouse_event(4, 0, 0, 0, UIntPtr.Zero);
    }
    private static readonly Dictionary<int, IntPtr> TrayCallbacks = [];
    internal static void OpenTrayMenu(int pid)
    {
        // .NET NotifyIcon callback message is WM_USER+1024 (0x800). ID 1 is the first icon in this process.
        // Cache before the first popup: later the hidden popup itself has a similar window class.
        if (!TrayCallbacks.TryGetValue(pid, out var hwnd))
        {
            hwnd = Search(pid, w => { var name = new StringBuilder(256); GetClassName(w, name, 256); return name.ToString().StartsWith("WindowsForms10.Window", StringComparison.Ordinal) && !IsWindowVisible(w); });
            TrayCallbacks[pid] = hwnd;
        }
        PostMessage(hwnd, 0x800, (IntPtr)1, (IntPtr)0x0205); // WM_RBUTTONUP
    }
}
