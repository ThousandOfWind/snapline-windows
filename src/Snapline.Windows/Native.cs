using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace Snapline.Windows;

internal static class Native
{
    internal const int WmHotkey = 0x0312;
    internal const int WmClipboardUpdate = 0x031D;
    internal const int ToggleHotkey = 1;
    internal const int CaptureHotkey = 2;
    private const uint Modifiers = 0x0002 | 0x0001 | 0x4000; // Ctrl + Alt + no-repeat

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll")]
    internal static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(Point point, uint flags);
    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left; public int Top; public int Right; public int Bottom; }

    internal static System.Drawing.Point Cursor
    {
        get { GetCursorPos(out var p); return new System.Drawing.Point(p.X, p.Y); }
    }

    internal static double Scale(Forms.Screen screen)
    {
        var p = new Point { X = screen.Bounds.Left + 1, Y = screen.Bounds.Top + 1 };
        var monitor = MonitorFromPoint(p, 2);
        return GetDpiForMonitor(monitor, 0, out var x, out _) == 0 ? x / 96.0 : 1;
    }

    internal static void MakeNonActivating(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        SetWindowLong(hwnd, -20, GetWindowLong(hwnd, -20) | 0x08000000 | 0x00000080);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    internal static void Place(Window window, int x, int y, int width, int height) =>
        SetWindowPos(new WindowInteropHelper(window).Handle, IntPtr.Zero, x, y, width, height, 0x0010 | 0x0004);

    internal static bool RegisterShortcuts(IntPtr hwnd)
    {
        var toggle = RegisterHotKey(hwnd, ToggleHotkey, Modifiers, 0x54); // T
        var capture = RegisterHotKey(hwnd, CaptureHotkey, Modifiers, 0x53); // S
        return toggle && capture;
    }

    internal static bool FullScreen(Forms.Screen screen, IntPtr ownWindow)
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero || foreground == ownWindow || !GetWindowRect(foreground, out var r)) return false;
        var bounds = screen.Bounds;
        // Do not treat Explorer's desktop as a full-screen application.
        var name = new System.Text.StringBuilder(256);
        GetClassName(foreground, name, name.Capacity);
        if (name.ToString() is "Progman" or "WorkerW") return false;
        return r.Left <= bounds.Left && r.Top <= bounds.Top && r.Right >= bounds.Right && r.Bottom >= bounds.Bottom;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder name, int max);
}
