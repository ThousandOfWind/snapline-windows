using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snapline.Core;

namespace Snapline.Windows;

internal static class WindowNative
{
    internal const uint MoveStart = 0x000A, MoveEnd = 0x000B, Destroy = 0x8001;
    internal delegate void EventCallback(IntPtr hook, uint kind, IntPtr window, int objectId, int childId, uint thread, uint time);
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetWinEventHook(uint first, uint last, IntPtr module, EventCallback callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int length);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder name, int length);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindowAsync(IntPtr window, int command);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowPlacement(IntPtr window, ref Placement placement);
    [DllImport("user32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPlacement(IntPtr window, ref Placement placement);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")] private static extern IntPtr GetClassIcon(IntPtr window, int index);
    [DllImport("user32.dll")] private static extern IntPtr CopyIcon(IntPtr icon);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyIcon(IntPtr icon);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, uint attribute, out int value, int size);

    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct Rectangle { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] internal struct Placement
    {
        public uint Length, Flags, ShowCommand;
        public Point Min, Max;
        public Rectangle Normal;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProp(IntPtr window, string name, IntPtr value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetProp(IntPtr window, string name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr RemoveProp(IntPtr window, string name);

    // A unique, session property dies with the window. PID/TID alone cannot distinguish a reused HWND within the same app.
    internal static bool Mark(WindowIdentity identity, string marker, bool allowOwn = false) => Matches(identity, allowOwn)
        && SetProp(new IntPtr(identity.Handle), marker, new IntPtr(1));
    internal static bool Marked(WindowIdentity identity, string marker, bool allowOwn = false) => Matches(identity, allowOwn)
        && GetProp(new IntPtr(identity.Handle), marker) == new IntPtr(1);
    internal static void Unmark(WindowIdentity identity, string marker, bool allowOwn = false)
    {
        if (Marked(identity, marker, allowOwn)) RemoveProp(new IntPtr(identity.Handle), marker);
    }

    [DllImport("user32.dll")] private static extern void NotifyWinEvent(uint kind, IntPtr window, int objectId, int childId);
    internal static void NotifyOwnFixture(uint kind, IntPtr window)
    {
        if (Identity(window, allowOwn: true)?.ProcessId != (uint)Environment.ProcessId) throw new ArgumentException("Only own synthetic fixture events allowed.");
        NotifyWinEvent(kind, window, 0, 0);
    }

    internal static bool EscapePressed => (GetAsyncKeyState(0x1B) & 0x8000) != 0;
    internal static bool LeftButtonPressed => (GetAsyncKeyState(0x01) & 0x8000) != 0;

    internal static WindowIdentity? Identity(IntPtr window, bool allowOwn = false)
    {
        if (!IsWindow(window)) return null;
        var thread = GetWindowThreadProcessId(window, out var pid);
        if (pid == 0 || thread == 0 || (!allowOwn && pid == (uint)Environment.ProcessId)) return null;
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return new WindowIdentity(window.ToInt64(), pid, thread, process.StartTime.ToUniversalTime().Ticks);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        { return null; }
    }

    internal static bool Matches(WindowIdentity identity, bool allowOwn = false) => Identity(new IntPtr(identity.Handle), allowOwn) == identity;

    internal static bool Eligible(IntPtr window, bool allowOwn = false)
    {
        if (Identity(window, allowOwn) == null || !IsWindowVisible(window) || IsIconic(window)
            || GetAncestor(window, 2) != window || GetWindow(window, 4) != IntPtr.Zero) return false;
        var style = GetWindowLong(window, -16); var extended = GetWindowLong(window, -20);
        if ((style & 0x00020000) == 0 || (extended & (0x80 | 0x08000000)) != 0) return false; // minimizable normal top-level app only
        if (DwmGetWindowAttribute(window, 14, out var cloaked, sizeof(int)) == 0 && cloaked != 0) return false;
        var name = new StringBuilder(256); GetClassName(window, name, name.Capacity);
        if (name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return false;
        return Native.Bounds(window).Width >= 100 && Native.Bounds(window).Height >= 60;
    }

    internal static IntPtr Root(IntPtr window)
    {
        var root = GetAncestor(window, 2);
        return root == IntPtr.Zero ? window : root;
    }

    internal static bool IsExplorer(IntPtr window)
    {
        var name = new StringBuilder(128); GetClassName(window, name, name.Capacity);
        return name.ToString() is "CabinetWClass" or "ExploreWClass";
    }

    internal static bool TitleBarDrag(IntPtr window, System.Drawing.Point cursor, bool leftPressed)
    {
        var point = new IntPtr((cursor.Y << 16) | (cursor.X & 0xffff));
        int? code = SendMessageTimeout(window, 0x0084, IntPtr.Zero, point, 2, 50, out var hit) != IntPtr.Zero ? hit.ToInt32() : null;
        var bounds = Native.Bounds(window);
        return WindowDragStartPolicy.Accept(code, cursor.X - bounds.Left, cursor.Y - bounds.Top, bounds.Width,
            Native.Scale(System.Windows.Forms.Screen.FromPoint(cursor)), leftPressed);
    }

    internal static Placement? ReadPlacement(IntPtr window)
    {
        var value = new Placement { Length = (uint)Marshal.SizeOf<Placement>() };
        return GetWindowPlacement(window, ref value) ? value : null;
    }

    internal static string Title(IntPtr window)
    {
        var title = new StringBuilder(256); GetWindowText(window, title, title.Capacity);
        return title.Length == 0 ? "软件窗口" : title.ToString();
    }

    internal static ImageSource? Icon(IntPtr window)
    {
        IntPtr icon = IntPtr.Zero;
        // Bounded message, not an executable-file read. Clone borrowed icon; never destroy another app's handle.
        if (SendMessageTimeout(window, 0x007F, (IntPtr)1, IntPtr.Zero, 2, 80, out var candidate) != IntPtr.Zero) icon = candidate;
        if (icon == IntPtr.Zero) icon = GetClassIcon(window, -14);
        if (icon == IntPtr.Zero) return null;
        var copy = CopyIcon(icon);
        if (copy == IntPtr.Zero) return null;
        try
        {
            var image = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(copy, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(48, 48));
            image.Freeze(); return image;
        }
        catch (ArgumentException) { return null; }
        finally { DestroyIcon(copy); }
    }

    internal static bool Minimize(WindowIdentity identity, bool allowOwn = false) => Matches(identity, allowOwn)
        && ShowWindowAsync(new IntPtr(identity.Handle), 6);

    internal static bool RememberOriginalWhileMinimized(WindowIdentity identity, Placement initial, bool allowOwn = false)
    {
        if (!Matches(identity, allowOwn) || !IsIconic(new IntPtr(identity.Handle))) return false;
        initial.Flags = initial.ShowCommand == 3 ? initial.Flags | 2u : initial.Flags & ~2u;
        initial.ShowCommand = 2; // SW_SHOWMINIMIZED: retain taskbar restore location without displaying it.
        return SetWindowPlacement(new IntPtr(identity.Handle), ref initial);
    }

    internal static bool Restore(WindowIdentity identity, Placement initial, bool allowOwn = false)
    {
        if (!Matches(identity, allowOwn)) return false;
        var window = new IntPtr(identity.Handle);
        initial.ShowCommand = initial.ShowCommand == 3 ? 3u : 1u;
        if (!SetWindowPlacement(window, ref initial)) return false;
        ShowWindowAsync(window, (int)initial.ShowCommand);
        return true;
    }

    internal static bool Activate(WindowIdentity identity, bool allowOwn = false) => Matches(identity, allowOwn)
        && SetForegroundWindow(new IntPtr(identity.Handle)); // No AttachThreadInput, injected Alt or privilege escalation.
}
