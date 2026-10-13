using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using Snapline.Core;

namespace Snapline.Windows;

internal sealed record PocketWindow(WindowIdentity Identity, string Title, ImageSource? Icon, WindowNative.Placement Original, string Marker,
    HangingKind Kind = HangingKind.Window);

/// <summary>Only session references; records must exist before minimizing, and failures never silently lose a window.</summary>
internal sealed class WindowPocketService(bool allowOwnTestWindows = false) : IDisposable
{
    private readonly WindowPocket pocket = new();
    private readonly List<PocketWindow> items = [];
    private bool operating;
    private bool disposed;
    internal IReadOnlyList<PocketWindow> Items => items.AsReadOnly();
    internal bool IsOperating => operating;
    internal event Action? Changed;
    internal string LastStatus { get; private set; } = "";

    internal async Task<bool> StashAsync(IntPtr window, WindowNative.Placement? original = null)
    {
        if (operating || disposed) return false;
        var identity = WindowNative.Identity(window, allowOwnTestWindows);
        if (identity == null || !WindowNative.Eligible(window, allowOwnTestWindows)) { LastStatus = "这个窗口无法暂存（只支持普通、可最小化窗口）"; return false; }
        if (!pocket.CanAdd(identity.Value)) { LastStatus = "窗口已暂存，或已达到 20 个窗口上限"; return false; }
        var placement = original ?? WindowNative.ReadPlacement(window);
        if (placement == null) { LastStatus = "无法读取窗口位置，未改变窗口"; return false; }
        operating = true;
        try
        {
            var marker = "Snapline.Pocket." + Guid.NewGuid().ToString("N");
            if (!WindowNative.Mark(identity.Value, marker, allowOwnTestWindows))
            { LastStatus = "窗口权限不允许暂存，未改变窗口；不支持高权限软件"; return false; }
            var item = new PocketWindow(identity.Value, WindowNative.Title(window), WindowNative.Icon(window), placement.Value, marker,
                WindowNative.IsExplorer(window) ? HangingKind.FolderWindow : HangingKind.Window);
            pocket.Add(item.Identity); items.Insert(0, item);
            if (!WindowNative.Minimize(item.Identity, allowOwnTestWindows)) { Remove(item); LastStatus = "无法最小化，窗口没有被暂存"; return false; }
            for (var attempt = 0; attempt < 12; attempt++)
            {
                await Task.Delay(50);
                if (disposed) return false;
                if (!WindowNative.Marked(item.Identity, item.Marker, allowOwnTestWindows)) { Remove(item); LastStatus = "窗口已关闭，未暂存"; return false; }
                if (WindowNative.IsIconic(window))
                {
                    WindowNative.RememberOriginalWhileMinimized(item.Identity, item.Original, allowOwnTestWindows);
                    LastStatus = "窗口已暂存 · 点击窗口卡片取回 · 任务栏也可恢复";
                    Changed?.Invoke(); return true;
                }
            }
            // Request accepted but app never minimized: roll back original placement, leave no false success.
            WindowNative.Restore(item.Identity, item.Original, allowOwnTestWindows);
            Remove(item); LastStatus = "软件没有响应最小化，已取消暂存"; return false;
        }
        finally { operating = false; }
    }

    internal async Task<bool> TakeAsync(WindowIdentity identity, bool activate = true)
    {
        if (operating || disposed) return false;
        var item = items.FirstOrDefault(item => item.Identity == identity);
        if (item == null) return false;
        if (!WindowNative.Marked(identity, item.Marker, allowOwnTestWindows)) { Remove(item); LastStatus = "原窗口已关闭，卡片已移除"; return false; }
        operating = true;
        try
        {
            if (!WindowNative.Restore(identity, item.Original, allowOwnTestWindows)) { LastStatus = "无法取回，请从 Windows 任务栏恢复"; return false; }
            for (var attempt = 0; attempt < 12; attempt++)
            {
                await Task.Delay(50);
                if (disposed) return false;
                if (!WindowNative.Marked(identity, item.Marker, allowOwnTestWindows)) { Remove(item); return false; }
                if (!WindowNative.IsIconic(new IntPtr(identity.Handle)))
                {
                    var focused = !activate || WindowNative.Activate(identity, allowOwnTestWindows);
                    Remove(item);
                    LastStatus = focused ? "已取回原窗口" : "窗口已恢复，Windows 未允许切前台；请点击任务栏";
                    return true;
                }
            }
            LastStatus = "窗口尚未恢复，卡片保留；可点击任务栏取回"; return false;
        }
        finally { operating = false; }
    }

    internal async Task TakeAllAsync()
    {
        foreach (var item in items.ToArray()) await TakeAsync(item.Identity, activate: false);
    }

    internal void WindowDestroyed(IntPtr window)
    {
        foreach (var item in items.Where(item => item.Identity.Handle == window.ToInt64()).ToArray()) Remove(item);
    }

    internal void Prune()
    {
        if (operating || disposed) return;
        foreach (var item in items.Where(item => !WindowNative.Marked(item.Identity, item.Marker, allowOwnTestWindows)
            || !WindowNative.IsIconic(new IntPtr(item.Identity.Handle))).ToArray()) Remove(item);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; Changed = null;
        foreach (var item in items.ToArray())
        {
            WindowNative.Unmark(item.Identity, item.Marker, allowOwnTestWindows); pocket.Remove(item.Identity);
        }
        items.Clear(); // Apps stay normally minimized and can always be found on the taskbar.
    }

    private void Remove(PocketWindow item)
    {
        WindowNative.Unmark(item.Identity, item.Marker, allowOwnTestWindows);
        items.Remove(item); pocket.Remove(item.Identity); Changed?.Invoke();
    }
}
