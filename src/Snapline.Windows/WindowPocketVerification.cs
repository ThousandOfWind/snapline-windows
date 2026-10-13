using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Snapline.Core;

namespace Snapline.Windows;

/// <summary>Generated own-process windows and process-scoped test WinEvents; no external hooks, input or screenshots.</summary>
internal static class WindowPocketVerification
{
    internal static async Task RunAsync(SnaplineApplication app, ShelfWindow shelf, Action<bool, string> check, string? styleEvidence = null)
    {
        var service = app.PocketForVerification;
        var edit = new TextBox { Text = "Synthetic unsaved work 123", AcceptsReturn = true, Margin = new Thickness(14) };
        var fixture = new Window { Title = "Synthetic pocket test", Width = 360, Height = 220, Content = edit,
            WindowStartupLocation = WindowStartupLocation.Manual, Left = 100, Top = 300, ShowInTaskbar = true };
        var other = new Window { Title = "Synthetic cancelled move", Width = 320, Height = 180, Left = 120, Top = 360,
            ShowInTaskbar = true, Content = new TextBlock { Text = "Generated test data only" } };
        try
        {
            fixture.Show(); other.Show(); await Task.Delay(150);
            var hwnd = new WindowInteropHelper(fixture).Handle;
            var untouched = new WindowInteropHelper(other).Handle;
            var original = WindowNative.ReadPlacement(hwnd)!.Value;
            check(WindowNative.Identity(hwnd) == null, "ordinary window policy excludes Snapline's own process");
            var stashed = await service.StashAsync(hwnd, original);
            check(stashed, "synthetic ordinary window can be pocketed without closing it: " + service.LastStatus);
            check(WindowNative.IsIconic(hwnd) && service.Items.Count == 1, "pocket stores reference and minimizes actual window");
            check(edit.Text == "Synthetic unsaved work 123" && WindowNative.IsWindow(hwnd), "unsaved synthetic state and window identity survive stash");
            check(!WindowNative.IsIconic(untouched), "stashing one window does not alter another window");
            check(!await service.StashAsync(hwnd) && service.Items.Count == 1, "duplicate stash does not create extra card");
            await Task.Delay(200);
            check(Descendants(shelf).OfType<Button>().Any(b => System.Windows.Automation.AutomationProperties.GetAutomationId(b) == "RestoreWindow"),
                "window card is rendered alongside screenshot cards");
            check(Descendants(shelf).OfType<FrameworkElement>().Any(e => Equals(e.Tag, "AppWindowFrame")), "software window has a titlebar-style blue frame");
            check(Descendants(shelf).OfType<Border>().Any(e => Equals(e.Tag, "SnaplinePeg") && e.Resources["HangingKind"] is HangingKind.Window),
                "software window uses a distinct metal clip");
            var realItem = service.Items.Single();
            var folderVisual = realItem with { Kind = HangingKind.FolderWindow, Title = "Generated folder style", Identity = realItem.Identity with { Handle = long.MaxValue } };
            shelf.RefreshWindows(new[] { realItem, folderVisual });
            await Task.Delay(120);
            check(Descendants(shelf).OfType<FrameworkElement>().Any(e => Equals(e.Tag, "FolderWindowFrame"))
                && Descendants(shelf).OfType<FrameworkElement>().Any(e => Equals(e.Tag, "PhotoPaperFrame")),
                "folder-tab and photo-paper frames coexist with software windows");
            check(Descendants(shelf).OfType<Border>().Any(e => Equals(e.Tag, "SnaplinePeg") && e.Resources["HangingKind"] is HangingKind.FolderWindow),
                "folder visual uses its brass clip rather than a wood peg");
            if (styleEvidence != null) { shelf.FinishMotion(); shelf.SaveTestRender(styleEvidence); }
            shelf.RefreshWindows(service.Items);
            var identity = service.Items.Single().Identity;
            var restoreControl = Descendants(shelf).OfType<Button>().Single(b => System.Windows.Automation.AutomationProperties.GetAutomationId(b) == "RestoreWindow");
            restoreControl.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            for (var attempt = 0; attempt < 20 && service.Items.Count > 0; attempt++) await Task.Delay(30);
            check(service.Items.Count == 0 && !WindowNative.IsIconic(hwnd), "window-card click invokes controller and restores original instance");
            check(!await service.TakeAsync(identity, activate: false), "taking already-restored card cannot open a second window");
            check(!WindowNative.IsIconic(hwnd) && service.Items.Count == 0 && edit.Text == "Synthetic unsaved work 123",
                "take removes only window card and preserves application state");
            var after = WindowNative.ReadPlacement(hwnd)!.Value;
            check(Math.Abs(after.Normal.Left - original.Normal.Left) <= 3 && Math.Abs(after.Normal.Top - original.Normal.Top) <= 3,
                "taking window restores pre-stash normal placement");

            check(await service.StashAsync(hwnd), "window can be stashed again after being taken");
            var markedItem = service.Items.Single();
            WindowNative.Unmark(markedItem.Identity, markedItem.Marker, allowOwn: true);
            check(!await service.TakeAsync(markedItem.Identity, activate: false) && WindowNative.IsIconic(hwnd),
                "missing lifetime marker refuses restoration even if HWND/PID still match");
            check(service.Items.Count == 0, "invalid lifetime marker clears stale card without opening another app");
            WindowNative.Restore(markedItem.Identity, markedItem.Original, allowOwn: true); await Task.Delay(100);
            check(await service.StashAsync(hwnd), "restored taskbar window can be stashed with fresh lifetime marker");
            fixture.Close(); await Task.Delay(150); service.Prune();
            check(service.Items.Count == 0, "closed pocketed window is pruned without reopening app");
            var newSession = new WindowPocketService(allowOwnTestWindows: true);
            check(newSession.Items.Count == 0, "fresh Snapline session has no persisted HWND references");
            newSession.Dispose();
            var stale = new WindowIdentity(hwnd.ToInt64(), 123, 456, 789);
            check(!await service.TakeAsync(stale, activate: false), "stale identity never activates an unrelated window");

            var last = new Window { Title = "Synthetic gesture target", Width = 360, Height = 220, Left = 100, Top = 280,
                Content = new TextBox { Text = "Gesture fixture" }, ShowInTaskbar = true };
            try
            {
                last.Show(); await Task.Delay(100);
                var targetHwnd = new WindowInteropHelper(last).Handle;
                var bounds = Native.Bounds(targetHwnd);
                var originalGesturePlacement = WindowNative.ReadPlacement(targetHwnd)!.Value;
                var point = new System.Drawing.Point(bounds.Left + 180, bounds.Top + 120);
                bool ready = false; bool leftHeld = false;
                using var watcher = new WindowMoveWatcher(service, p => p == point, (_, r) => ready = r, _ => { },
                    allowOwnTests: true, testLeftPressed: () => leftHeld);
                check(watcher.Start(), "own-process-scoped WinEvent hooks register without observing user apps");
                WindowNative.NotifyOwnFixture(WindowNative.MoveStart, targetHwnd);
                for (var attempt = 0; attempt < 20 && !watcher.IsMoving; attempt++) await Task.Delay(25);
                check(watcher.IsMoving, "actual WinEvent callback arms synthetic move");
                watcher.Reset();
                var childFixture = new System.Windows.Forms.NativeWindow();
                try
                {
                    childFixture.CreateHandle(new System.Windows.Forms.CreateParams { Caption = "Synthetic child source", Parent = targetHwnd,
                        Style = 0x40000000, Width = 20, Height = 20 });
                    watcher.Begin(childFixture.Handle);
                    check(watcher.IsMoving, "child-origin move source normalizes to eligible top-level window");
                }
                finally { watcher.Reset(); childFixture.DestroyHandle(); }
                watcher.Begin(targetHwnd);
                Native.Place(last, point.X - 80, point.Y - 14, bounds.Width, bounds.Height);
                watcher.Update(point, escape: false);
                check(ready && !WindowNative.IsIconic(targetHwnd) && service.Items.Count == 0, "approach shows immediate readiness without minimizing held window");
                check(await watcher.EndAsync(targetHwnd, point), "release on target commits directly without a dwell delay");
                check(WindowNative.IsIconic(targetHwnd), "gesture commit minimizes only synthetic target");
                var safePlacement = WindowNative.ReadPlacement(targetHwnd)!.Value;
                check(safePlacement.Normal.Left == originalGesturePlacement.Normal.Left
                    && safePlacement.Normal.Top == originalGesturePlacement.Normal.Top,
                    "minimized window remembers pre-drag restore location for taskbar recovery");
                await service.TakeAllAsync();
                check(service.Items.Count == 0 && !WindowNative.IsIconic(targetHwnd), "take-all restores minimized windows and clears pocket");
                check(await service.StashAsync(targetHwnd), "window supports another stash before taskbar recovery");
                var taskbarItem = service.Items.Single();
                WindowNative.Restore(taskbarItem.Identity, taskbarItem.Original, allowOwn: true); await Task.Delay(100);
                service.Prune();
                check(service.Items.Count == 0 && !WindowNative.IsIconic(targetHwnd), "external taskbar-style restore removes only pocket reference");
                var owned = new Window { Title = "Synthetic owned dialog", Owner = last, Width = 200, Height = 100, ShowInTaskbar = false };
                try
                {
                    owned.Show(); await Task.Delay(50);
                    check(!await service.StashAsync(new WindowInteropHelper(owned).Handle), "owned dialogs are excluded and remain available");
                }
                finally { owned.Close(); }
                last.WindowState = WindowState.Maximized; await Task.Delay(100);
                check(await service.StashAsync(targetHwnd), "maximized synthetic window can be stashed");
                var maximized = service.Items.Single();
                check(await service.TakeAsync(maximized.Identity, activate: false), "maximized window can be taken");
                await Task.Delay(100);
                check(WindowNative.ReadPlacement(targetHwnd)?.ShowCommand == 3, "take restores original maximized state");
                last.WindowState = WindowState.Normal; await Task.Delay(100);
                watcher.Begin(targetHwnd);
                bounds = Native.Bounds(targetHwnd);
                Native.Place(last, point.X - 80, point.Y - 14, bounds.Width, bounds.Height);
                watcher.Update(point, escape: true); await Task.Delay(50);
                check(!await watcher.EndAsync(targetHwnd, point) && !WindowNative.IsIconic(targetHwnd), "cancelled gesture never pockets the target");
                watcher.Begin(targetHwnd);
                var safeBounds = Native.Bounds(targetHwnd);
                point = new System.Drawing.Point(safeBounds.Left + 140, safeBounds.Top + 70);
                Native.Place(last, point.X - 80, point.Y - 14, safeBounds.Width, safeBounds.Height);
                leftHeld = true; watcher.Update(point, escape: false);
                await Task.Delay(900); watcher.Update(point, escape: false);
                check(service.Items.Count == 0 && !WindowNative.IsIconic(targetHwnd), "long hover while holding titlebar never auto-stashes");
                check(!await watcher.EndAsync(targetHwnd, point) && service.Items.Count == 0, "move-end while mouse still held is not a release");
                leftHeld = false;
                var fallbackOrigin = Native.Bounds(targetHwnd);
                point = new System.Drawing.Point(fallbackOrigin.Left + 140, fallbackOrigin.Top + 24);
                leftHeld = true;
                watcher.ObservePointer(point, held: true, escape: false, testForeground: targetHwnd);
                Native.Place(last, fallbackOrigin.Left + 55, fallbackOrigin.Top + 30, fallbackOrigin.Width, fallbackOrigin.Height);
                point = new System.Drawing.Point(point.X + 55, point.Y + 30);
                watcher.ObservePointer(point, held: true, escape: false, testForeground: targetHwnd);
                check(watcher.IsMoving, "following geometry arms compatibility path even without a move-start event");
                check(service.Items.Count == 0 && !WindowNative.IsIconic(targetHwnd), "compatibility polling never commits before release");
                leftHeld = false;
                watcher.ObservePointer(point, held: false, escape: false, testForeground: targetHwnd);
                for (var attempt = 0; attempt < 30 && (service.Items.Count != 1 || !WindowNative.IsIconic(targetHwnd) || service.IsOperating); attempt++) await Task.Delay(25);
                check(service.Items.Count == 1 && WindowNative.IsIconic(targetHwnd), "compatibility path commits on release even without move-end event: " + watcher.LastDiagnostic);
                await service.TakeAllAsync();
                WindowNative.NotifyOwnFixture(WindowNative.MoveStart, targetHwnd);
                await Task.Delay(80); watcher.Reset();
                watcher.Enabled = false; watcher.Begin(targetHwnd);
                check(!watcher.IsMoving, "disabled window pocket listener does not arm gesture");
            }
            finally { last.Close(); }
        }
        finally { fixture.Close(); other.Close(); service.Prune(); }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
