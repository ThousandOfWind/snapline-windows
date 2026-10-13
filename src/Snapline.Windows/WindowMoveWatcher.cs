using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using Snapline.Core;

namespace Snapline.Windows;

/// <summary>Observe normal move/size events, never intercept a global mouse hook or enumerate window contents.</summary>
internal sealed class WindowMoveWatcher : IDisposable
{
    private readonly WindowPocketService pocket;
    private readonly Func<System.Drawing.Point, bool> overTarget;
    private readonly Action<bool, bool> hint;
    private readonly Action<string> status;
    private readonly Dispatcher dispatcher;
    private readonly bool allowOwnTests;
    private readonly WindowNative.EventCallback callback;
    private readonly Func<bool> leftPressed;
    private readonly DispatcherTimer poll = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private IntPtr moveHook, destroyHook;
    private WindowIdentity? moving;
    private WindowNative.Placement? initial;
    private System.Drawing.Rectangle initialBounds;
    private bool cancelled;
    private bool disposed;
    private WindowIdentity? candidate;
    private System.Drawing.Point candidateCursor;
    private System.Drawing.Rectangle candidateBounds;
    private WindowNative.Placement? candidatePlacement;
    internal bool IsMoving => moving != null;
    internal HangingKind MovingKind { get; private set; } = HangingKind.Window;
    internal string LastDiagnostic { get; private set; } = "尚未收到窗口拖动事件";
    internal bool Enabled { get; set; } = true;

    internal WindowMoveWatcher(WindowPocketService pocket, Func<System.Drawing.Point, bool> overTarget,
        Action<bool, bool> hint, Action<string> status, bool allowOwnTests = false, Func<bool>? testLeftPressed = null)
    {
        this.pocket = pocket; this.overTarget = overTarget; this.hint = hint; this.status = status;
        this.allowOwnTests = allowOwnTests; dispatcher = Dispatcher.CurrentDispatcher;
        leftPressed = allowOwnTests ? testLeftPressed ?? (() => false) : () => WindowNative.LeftButtonPressed;
        callback = OnEvent;
        poll.Tick += (_, _) => ObservePointer(Native.Cursor, leftPressed(), WindowNative.EscapePressed);
    }

    internal bool Start()
    {
        var process = allowOwnTests ? (uint)Environment.ProcessId : 0;
        var flags = allowOwnTests ? 0u : 2u; // WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS
        moveHook = WindowNative.SetWinEventHook(WindowNative.MoveStart, WindowNative.MoveEnd, IntPtr.Zero, callback, process, 0, flags);
        destroyHook = WindowNative.SetWinEventHook(WindowNative.Destroy, WindowNative.Destroy, IntPtr.Zero, callback, process, 0, flags);
        if (moveHook != IntPtr.Zero && destroyHook != IntPtr.Zero)
        {
            if (!allowOwnTests) poll.Start();
            return true;
        }
        Dispose(); return false;
    }

    private void OnEvent(IntPtr hook, uint kind, IntPtr window, int objectId, int childId, uint thread, uint time)
    {
        if (disposed || window == IntPtr.Zero || dispatcher.HasShutdownStarted) return;
        if (kind == WindowNative.Destroy && (objectId != 0 || childId != 0)) return;
        // Modern Explorer can emit move events from a child HWND. Never normalize a child-destroy event.
        var root = kind == WindowNative.Destroy ? window : WindowNative.Root(window);
        var point = Native.Cursor; var held = leftPressed();
        dispatcher.BeginInvoke(new Action(() =>
        {
            if (disposed) return;
            if (kind == WindowNative.Destroy) { pocket.WindowDestroyed(root); return; }
            if (kind == WindowNative.MoveStart) Begin(root, point, held);
            if (kind == WindowNative.MoveEnd) _ = EndAsync(root, point, held);
        }));
    }

    internal void Begin(IntPtr window, System.Drawing.Point? eventPoint = null, bool? eventHeld = null)
    {
        window = WindowNative.Root(window);
        if (moving?.Handle == window.ToInt64()) return;
        Reset();
        LastDiagnostic = "收到移动开始事件";
        if (!Enabled) { LastDiagnostic = "窗口暂存已关闭"; return; }
        if (!WindowNative.Eligible(window, allowOwnTests)) { LastDiagnostic = "窗口类型、权限或可最小化检查未通过"; return; }
        var point = eventPoint ?? Native.Cursor; var held = eventHeld ?? leftPressed();
        if (!allowOwnTests && !WindowNative.TitleBarDrag(window, point, held))
        { LastDiagnostic = "未确认标题区域拖动（可能是调整大小或按键移动）"; return; }
        moving = WindowNative.Identity(window, allowOwnTests);
        initial = WindowNative.ReadPlacement(window); initialBounds = Native.Bounds(window);
        if (moving == null || initial == null) { Reset(); return; }
        MovingKind = WindowNative.IsExplorer(window) ? HangingKind.FolderWindow : HangingKind.Window;
        LastDiagnostic = "已确认窗口拖动，等待靠近绳子并松手";
        hint(true, false);
    }

    internal void ObservePointer(System.Drawing.Point cursor, bool held, bool escape, IntPtr? testForeground = null)
    {
        if (disposed) return;
        if (!Enabled) { if (moving != null) Reset(); candidate = null; return; }
        if (moving != null)
        {
            Update(cursor, escape);
            if (!held) _ = EndAsync(new IntPtr(moving.Value.Handle), cursor, eventHeld: false);
            return;
        }
        if (!held || escape) { candidate = null; return; }
        var foreground = WindowNative.Root(allowOwnTests ? testForeground ?? IntPtr.Zero : Native.GetForegroundWindow());
        if (foreground == IntPtr.Zero) return;
        if (candidate == null || candidate.Value.Handle != foreground.ToInt64())
        {
            candidate = null;
            if (!WindowNative.Eligible(foreground, allowOwnTests) || !WindowNative.TitleBarDrag(foreground, cursor, held)) return;
            candidate = WindowNative.Identity(foreground, allowOwnTests);
            candidateCursor = cursor; candidateBounds = Native.Bounds(foreground); candidatePlacement = WindowNative.ReadPlacement(foreground);
            return;
        }
        var current = Native.Bounds(foreground);
        var follow = WindowDragFallbackPolicy.Accept(held, WindowNative.Matches(candidate.Value, allowOwnTests),
            candidateBounds.Width, candidateBounds.Height, current.Width, current.Height,
            current.Left - candidateBounds.Left, current.Top - candidateBounds.Top,
            cursor.X - candidateCursor.X, cursor.Y - candidateCursor.Y, Native.Scale(System.Windows.Forms.Screen.FromPoint(cursor)));
        if (!follow || candidatePlacement == null) return;
        moving = candidate; initial = candidatePlacement; initialBounds = candidateBounds; candidate = null; cancelled = false;
        MovingKind = WindowNative.IsExplorer(foreground) ? HangingKind.FolderWindow : HangingKind.Window;
        LastDiagnostic = "通过标题区域与窗口跟手位移确认拖动（兼容路径）";
        hint(true, false); Update(cursor, escape);
    }

    internal void Update(System.Drawing.Point cursor, bool escape)
    {
        if (moving == null) return;
        cancelled |= escape;
        var onTarget = Enabled && !cancelled && overTarget(cursor);
        // Feedback only. Never stash/minimize while the title bar is still being held.
        hint(true, onTarget);
    }

    internal async Task<bool> EndAsync(IntPtr window, System.Drawing.Point cursor, bool? eventHeld = null)
    {
        window = WindowNative.Root(window);
        if (moving == null || moving.Value.Handle != window.ToInt64()) return false;
        if (eventHeld ?? leftPressed())
        {
            // Some move-end notifications precede button-up state by a few milliseconds. Bounded wait, never auto-commit.
            for (var wait = 0; wait < 8 && leftPressed(); wait++) await Task.Delay(15);
            if (leftPressed()) { LastDiagnostic = "结束事件到达时仍按住鼠标，未提交"; Reset(); return false; }
        }
        if (disposed || moving == null || moving.Value.Handle != window.ToInt64()) return false;
        var identity = moving.Value; var placement = initial;
        var bounds = Native.Bounds(window);
        // Resizing, cancelled move returning to original bounds, and a huge overlapping body are not drops.
        var moved = bounds.Location != initialBounds.Location;
        var titleAtPointer = cursor.X >= bounds.Left && cursor.X < bounds.Right && cursor.Y >= bounds.Top - 8
            && cursor.Y <= bounds.Top + 90 * Native.Scale(System.Windows.Forms.Screen.FromPoint(cursor));
        var target = overTarget(cursor);
        var accepted = WindowDropPolicy.Accept(Enabled, WindowNative.Matches(identity, allowOwnTests), cancelled,
            moved, target, titleAtPointer);
        LastDiagnostic = accepted ? "松手条件通过，正在暂存" : cancelled ? "拖动已取消" : !target ? "松手点不在绳子附近"
            : !moved ? "窗口位置未发生移动" : !titleAtPointer ? "松手点与标题区域不匹配" : "窗口身份或功能状态无效";
        Reset();
        if (!accepted || placement == null) return false;
        var result = await pocket.StashAsync(window, placement);
        LastDiagnostic = pocket.LastStatus;
        if (!disposed) status(pocket.LastStatus);
        return result;
    }

    internal void Reset()
    {
        moving = null; initial = null; candidate = null; cancelled = false;
        hint(false, false);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; poll.Stop(); Reset();
        if (moveHook != IntPtr.Zero) WindowNative.UnhookWinEvent(moveHook);
        if (destroyHook != IntPtr.Zero) WindowNative.UnhookWinEvent(destroyHook);
        moveHook = destroyHook = IntPtr.Zero;
        GC.KeepAlive(callback);
    }
}
