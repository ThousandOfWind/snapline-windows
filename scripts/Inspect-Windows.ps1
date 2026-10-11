param([string]$ProcessName = 'Snapline')
$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class SnaplineWindowProbe {
    public delegate bool Callback(IntPtr window, IntPtr state);
    [DllImport("user32.dll")] public static extern bool EnumWindows(Callback callback, IntPtr state);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr window, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr window, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    public static string[] Inspect(uint pid) {
        var result = new List<string>();
        EnumWindows((w,s) => { uint p; GetWindowThreadProcessId(w, out p); if (p != pid) return true;
            var title = new StringBuilder(256); var name = new StringBuilder(256); Rect r;
            GetWindowText(w,title,256); GetClassName(w,name,256); GetWindowRect(w,out r);
            result.Add($"hwnd={w} visible={IsWindowVisible(w)} class={name} title={title} rect={r.Left},{r.Top},{r.Right},{r.Bottom}");
            return true;
        }, IntPtr.Zero); return result.ToArray();
    }
    public static void Toggle(uint pid) {
        EnumWindows((w,s) => { uint p; GetWindowThreadProcessId(w, out p); if (p != pid) return true;
            var t = new StringBuilder(256); GetWindowText(w,t,256);
            if (t.ToString() == "Snapline.Messages") PostMessage(w, 0x0312, (IntPtr)1, IntPtr.Zero);
            return true;
        }, IntPtr.Zero);
    }
}
'@
Get-Process $ProcessName -ErrorAction SilentlyContinue | ForEach-Object { [SnaplineWindowProbe]::Inspect($_.Id) }
