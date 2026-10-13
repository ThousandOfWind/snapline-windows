param([string]$FixtureDirectory)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$fixture = if ($FixtureDirectory) { [IO.Path]::GetFullPath($FixtureDirectory) } else { Join-Path $root ('artifacts/SnaplineExplorerFixture-' + [guid]::NewGuid().ToString('N').Substring(0,8)) }
if (-not $fixture.StartsWith((Join-Path $root 'artifacts/SnaplineExplorerFixture-'), [StringComparison]::OrdinalIgnoreCase)) { throw 'Only generated fixture directories may be probed.' }
New-Item -ItemType Directory -Force $fixture | Out-Null
# Open only a new empty test directory. Do not enumerate other Explorer windows, their paths or file lists.
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class ExplorerFixtureProbe {
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string name,string title);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr w,int i);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr w,uint i);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr w,uint i);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr w,out Rect r);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr w,StringBuilder text,int n);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr w,uint msg,IntPtr wp,IntPtr lp,uint flags,uint timeout,out IntPtr result);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr w,uint msg,IntPtr wp,IntPtr lp);
    [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor,int type,out uint x,out uint y);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr w,uint flags);
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left,Top,Right,Bottom; }
    public static string Describe(IntPtr w) {
        SetThreadDpiAwarenessContext(new IntPtr(-4));
        GetWindowRect(w,out var r); var c=new StringBuilder(128);GetClassName(w,c,128);
        GetDpiForMonitor(MonitorFromWindow(w,2),0,out var dpi,out _);double scale=dpi/96.0;
        var output=$"Fixture class={c}, root={GetAncestor(w,2)==w}, owned={GetWindow(w,4)!=IntPtr.Zero}, minBox={(GetWindowLong(w,-16)&0x20000)!=0}, exStyle={GetWindowLong(w,-20):X}, dpi={dpi}";
        foreach(var y in new[]{12,24,36,48}) {
            int px=r.Left+(r.Right-r.Left)/2;int py=r.Top+(int)Math.Round(y*scale);
            var ok=SendMessageTimeout(w,0x84,IntPtr.Zero,(IntPtr)((py<<16)|(px&0xffff)),2,150,out var hit)!=IntPtr.Zero;
            output+=$"\nHeader sample {y}dip: hit={(ok?hit.ToInt32():-999)}";
        }
        return output;
    }
}
'@
Start-Process explorer.exe -ArgumentList ('/n,"' + $fixture + '"') | Out-Null
$window = [IntPtr]::Zero
for ($n=0; $n -lt 40 -and $window -eq [IntPtr]::Zero; $n++) {
    Start-Sleep -Milliseconds 150
    $leaf = Split-Path $fixture -Leaf
    foreach ($title in @($leaf, $fixture, "$leaf - File Explorer", "$leaf - 文件资源管理器", "$leaf - Windows Explorer")) {
        $window = [ExplorerFixtureProbe]::FindWindow($null, $title)
        if ($window -ne [IntPtr]::Zero) { break }
    }
}
if ($window -eq [IntPtr]::Zero) { throw "Could not identify the new fixture window by its exact known title; no other windows inspected. Fixture: $fixture" }
try { [ExplorerFixtureProbe]::Describe($window) }
finally {
    # Close only the exact fixture window, never the Explorer process or other windows.
    [ExplorerFixtureProbe]::PostMessage($window,0x10,[IntPtr]::Zero,[IntPtr]::Zero) | Out-Null
}
