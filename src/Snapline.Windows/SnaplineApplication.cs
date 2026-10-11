using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Snapline.Core;
using Forms = System.Windows.Forms;

namespace Snapline.Windows;

internal sealed class SnaplineApplication : IDisposable
{
    private readonly Application app;
    private readonly SettingsStore store;
    private readonly Preferences settings;
    private readonly ImageLibrary library;
    private readonly bool smoke;
    private readonly bool e2e;
    private readonly List<FileSystemWatcher> watchers = [];
    private readonly HashSet<string> pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly ShelfWindow shelf;
    private readonly HandleWindow handle;
    private bool showAfterCapture;
    private HwndSource? messageWindow;
    private Forms.NotifyIcon? tray;
    private DateTime hotSince;
    private DateTime awaySince;
    private DateTime peekUntil;
    private bool pinned;
    private bool capturing;
    private bool busy;
    private bool disposed;
    private bool clipboardListening;
    private uint ownClipboardSequence;
    private uint pendingClipboardSequence;
    private bool clipboardRetryPending;
    private int tickCount;

    internal SnaplineApplication(Application app, string directory, bool smoke, bool e2e = false)
    {
        this.app = app;
        this.smoke = smoke;
        this.e2e = e2e;
        store = new SettingsStore(directory);
        settings = store.Load();
        library = new ImageLibrary(settings.Capacity, settings.Items);
        shelf = new ShelfWindow(this);
        handle = new HandleWindow(() => Reveal(true));
    }

    internal void Start()
    {
        messageWindow = new HwndSource(new HwndSourceParameters("Snapline.Messages") { Width = 0, Height = 0, WindowStyle = 0 });
        messageWindow.AddHook(MessageHook);
        if (smoke)
        {
            var path = Path.Combine(store.InboxPath, "smoke.png");
            ImageIO.SavePng(ImageIO.TestImage(), path);
            AddImage(path, peek: false);
            Reveal(true);
        }
        else
        {
            CreateTray();
            if (!Native.RegisterShortcuts(messageWindow.Handle))
                Notify("快捷键被其他应用占用，可从托盘菜单打开截图栏或区域截图。");
            if (!settings.WatchFoldersInitialized && !e2e)
            {
                settings.WatchFoldersInitialized = true;
                var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                settings.WatchFolders.Add(Path.Combine(pictures, "Screenshots"));
                settings.WatchFolders.Add(Path.Combine(pictures, "屏幕截图"));
            }
            StartWatchers();
            SetClipboardListener();
            shelf.Refresh(library.Items);
            Reveal(true); // A launch must visibly confirm that the application is running.
            Save();
        }
        timer.Tick += Tick;
        timer.Start();
    }

    private IntPtr MessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WmShowShelf)
        {
            if (capturing) showAfterCapture = true;
            Reveal(true); // Relaunch shows the running instance, rather than silently exiting.
            handled = true;
        }
        if (msg == 0x8002 && e2e)
        {
            shelf.SaveTestRender(System.IO.Path.Combine(store.DirectoryPath, "shelf-render.png"));
            handled = true;
        }
        if (msg == Native.WmHotkey)
        {
            if (wParam.ToInt32() == Native.ToggleHotkey) Toggle();
            if (wParam.ToInt32() == Native.CaptureHotkey) _ = CaptureAsync();
            handled = true;
        }
        if (msg == Native.WmClipboardUpdate && settings.WatchClipboard && !smoke)
        {
            var sequence = Native.GetClipboardSequenceNumber();
            if (sequence != ownClipboardSequence) _ = CaptureClipboardAsync(sequence);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private async Task CaptureClipboardAsync(uint sequence)
    {
        pendingClipboardSequence = sequence;
        if (clipboardRetryPending) return;
        clipboardRetryPending = true;
        try
        {
            // Clipboard may be temporarily locked by another application. Try briefly, never log its contents.
            for (var retry = 0; retry < 5 && !disposed && settings.WatchClipboard; retry++)
            {
                await Task.Delay(100);
                var target = pendingClipboardSequence;
                if (target == ownClipboardSequence) return;
                try
                {
                    var data = Clipboard.GetDataObject();
                    if (data == null || !ClipboardPolicy.AllowsImage(data.GetFormats())) return;
                    if (!data.GetDataPresent(DataFormats.Bitmap) || data.GetData(DataFormats.Bitmap) is not BitmapSource image) return;
                    if (target != Native.GetClipboardSequenceNumber()) continue;
                    var path = NewImagePath("Clipboard");
                    ImageIO.SavePng(image, path);
                    AddImage(path);
                    return;
                }
                catch (ExternalException) { }
            }
        }
        finally { clipboardRetryPending = false; }
    }

    private void CreateTray()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示 / 隐藏截图栏    Ctrl+Alt+T", null, (_, _) => Toggle());
        menu.Items.Add("区域截图    Ctrl+Alt+S", null, (_, _) => _ = CaptureAsync());
        menu.Items.Add("添加图片…", null, (_, _) => ChooseImages());
        menu.Items.Add(new Forms.ToolStripSeparator());
        var clipboard = new Forms.ToolStripMenuItem("监听剪贴板图片（可选）") { Checked = settings.WatchClipboard, CheckOnClick = true };
        clipboard.CheckedChanged += (_, _) => { settings.WatchClipboard = clipboard.Checked; SetClipboardListener(); Save(); };
        menu.Items.Add(clipboard);
        var edge = new Forms.ToolStripMenuItem("鼠标停留顶部时展开") { Checked = settings.RevealAtTop, CheckOnClick = true };
        edge.CheckedChanged += (_, _) => { settings.RevealAtTop = edge.Checked; Save(); };
        menu.Items.Add(edge);
        var peek = new Forms.ToolStripMenuItem("新图片短暂展开") { Checked = settings.PeekOnNew, CheckOnClick = true };
        peek.CheckedChanged += (_, _) => { settings.PeekOnNew = peek.Checked; Save(); };
        menu.Items.Add(peek);
        menu.Items.Add("添加监听目录…", null, (_, _) => ChooseFolder());
        menu.Items.Add("管理监听目录…", null, (_, _) => ManageFolders());
        menu.Items.Add("打开本地收件箱", null, (_, _) => Open(store.InboxPath));
        menu.Items.Add("从栏中移除全部（不删除文件）", null, (_, _) => { library.Clear(); Changed(); });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("关于 Snapline", null, (_, _) => MessageBox.Show(
            "Snapline 0.1.2 · Windows 10/11\n\nCtrl+Alt+T：显示 / 隐藏\nCtrl+Alt+S：区域截图\n单击复制 · 双击预览 · 拖拽发送文件\n右键菜单：另存为、打开、画图编辑、移除\n\n数据仅保存在本机，无网络、无分析。\n灵感来源：alejandrobujan/tendedero (MIT)。\n本项目使用独立名称和原创图标，与原作者无关联。", "关于 Snapline"));
        menu.Items.Add("退出", null, (_, _) => app.Shutdown());
        var executable = Environment.ProcessPath!;
        tray = new Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(executable),
            Text = "Snapline · 截图随手可取",
            ContextMenuStrip = menu,
            Visible = true
        };
        tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) Reveal(true); };
    }

    private void SetClipboardListener()
    {
        if (messageWindow == null) return;
        if (settings.WatchClipboard && !clipboardListening)
        {
            clipboardListening = Native.AddClipboardFormatListener(messageWindow.Handle);
            if (!clipboardListening) Notify("无法监听剪贴板，请稍后重新开启。");
        }
        else if (!settings.WatchClipboard && clipboardListening)
        {
            Native.RemoveClipboardFormatListener(messageWindow.Handle);
            clipboardListening = false;
        }
    }

    private void StartWatchers()
    {
        foreach (var watcher in watchers) watcher.Dispose();
        watchers.Clear();
        foreach (var folder in settings.WatchFolders.Append(store.InboxPath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(folder)) continue;
            try
            {
                var watcher = new FileSystemWatcher(folder)
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
                };
                watcher.Created += (_, e) => QueueImage(e.FullPath);
                watcher.Changed += (_, e) => QueueImage(e.FullPath);
                watcher.Renamed += (_, e) => QueueImage(e.FullPath);
                watcher.Deleted += (_, _) => Dispatch(() => { library.Prune(); Changed(); });
                watcher.Error += (_, _) => Dispatch(() => Notify("目录监听暂时失败，请从托盘重新添加目录或重启 Snapline。"));
                watcher.EnableRaisingEvents = true;
                watchers.Add(watcher);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            { Notify("一个目录不可访问，已跳过；其他目录仍继续监听。"); }
        }
    }

    private void Dispatch(Action action)
    {
        if (!disposed && !app.Dispatcher.HasShutdownStarted) app.Dispatcher.BeginInvoke(action);
    }

    private void QueueImage(string path)
    {
        if (!ImageFiles.IsSupported(path)) return;
        Dispatch(() => _ = WaitForImageAsync(path));
    }

    private async Task WaitForImageAsync(string path)
    {
        if (!pending.Add(path)) return;
        try
        {
            long lastSize = -1;
            for (var retry = 0; retry < 20 && !disposed; retry++)
            {
                await Task.Delay(250);
                try
                {
                    var size = new FileInfo(path).Length;
                    if (size <= 0 || size != lastSize) { lastSize = size; continue; }
                    if (ImageIO.TryLoad(path, out _)) { AddImage(path); return; }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { return; }
            }
        }
        finally { pending.Remove(path); }
    }

    private void AddImage(string path, bool peek = true)
    {
        if (!ImageIO.TryLoad(path, out _)) return;
        var added = library.Add(path);
        Changed(); // Refresh thumbnails after external edits, too.
        if (added && peek && settings.PeekOnNew && !busy && !capturing)
        {
            var screen = Forms.Screen.FromPoint(Native.Cursor);
            if (!Native.FullScreen(screen, new WindowInteropHelper(shelf).Handle))
            {
                Reveal(false);
                peekUntil = DateTime.UtcNow.AddSeconds(2.5);
            }
        }
    }

    private void Changed()
    {
        shelf.Refresh(library.Items); Save();
        if (!shelf.IsRevealed && !capturing && !smoke) handle.Reveal(shelf.CurrentScreen, library.Items.Count);
    }
    private void Save() { settings.Items = library.Items.ToList(); store.Save(settings); }
    internal void Remove(string path) { library.Remove(path); Changed(); }
    internal void Hide()
    {
        shelf.Conceal(); pinned = false; peekUntil = DateTime.MinValue;
        hotSince = DateTime.MinValue;
        if (!capturing && !smoke) handle.Reveal(shelf.CurrentScreen, library.Items.Count);
    }
    internal void Toggle()
    {
        if (capturing) { showAfterCapture = true; return; }
        if (shelf.IsRevealed) Hide(); else Reveal(true);
    }

    private void Reveal(bool pin)
    {
        if (capturing) return;
        pinned |= pin; // Background notifications must not unpin a deliberately opened shelf.
        awaySince = DateTime.MinValue;
        handle.Hide();
        shelf.Reveal(Forms.Screen.FromPoint(Native.Cursor));
    }

    private void Tick(object? sender, EventArgs e)
    {
        if (smoke || capturing || busy) return;
        var now = DateTime.UtcNow;
        var cursor = Native.Cursor;
        var screen = Forms.Screen.FromPoint(cursor);
        if (shelf.IsRevealed)
        {
            if (!pinned && Native.FullScreen(shelf.CurrentScreen, new WindowInteropHelper(shelf).Handle)) { Hide(); return; }
            if (shelf.ContainsCursor(cursor)) { awaySince = DateTime.MinValue; }
            else if (!pinned && now > peekUntil)
            {
                if (awaySince == DateTime.MinValue) awaySince = now;
                if ((now - awaySince).TotalMilliseconds > 650) Hide();
            }
        }
        else if (settings.RevealAtTop && library.Items.Count > 0 && cursor.Y <= screen.Bounds.Top + 2)
        {
            if (hotSince == DateTime.MinValue) hotSince = now;
            if ((now - hotSince).TotalMilliseconds > 450) { Reveal(false); peekUntil = now.AddSeconds(1.2); }
        }
        else hotSince = DateTime.MinValue;
        if (++tickCount % 100 == 0)
        {
            var count = library.Items.Count;
            library.Prune();
            if (library.Items.Count != count) Changed();
            // Detect default Screenshots folders created after the application started.
            if (settings.WatchFolders.Any(f => Directory.Exists(f) && !watchers.Any(w => string.Equals(w.Path, f, StringComparison.OrdinalIgnoreCase))))
                StartWatchers();
        }
    }

    private string NewImagePath(string prefix) => Path.Combine(store.InboxPath, $"{prefix}-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8]}.png");

    internal async Task CaptureAsync()
    {
        if (capturing || smoke) return;
        capturing = true;
        showAfterCapture = true;
        handle.Hide();
        Hide();
        try
        {
            await Task.Delay(260);
            var overlay = new CaptureWindow(e2e ? ImageIO.TestImage() : null);
            if (overlay.ShowDialog() == true && overlay.CapturedImage != null)
            {
                var path = NewImagePath("Capture");
                ImageIO.SavePng(overlay.CapturedImage, path);
                AddImage(path, peek: false);
                shelf.ShowStatus("截图已挂上 · 点击复制 · 收起后可点右上角入口找回");
            }
        }
        catch (Exception e) when (e is Win32Exception or ExternalException or IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            Notify("无法截图，请重试；截图栏已恢复。图片仍保存在本机。");
            shelf.ShowStatus("截图未完成，截图栏已恢复");
        }
        finally
        {
            capturing = false;
            // Success, cancel and recoverable failure all leave a discoverable, pinned shelf.
            if (showAfterCapture && !disposed) Reveal(true);
            showAfterCapture = false;
        }
    }

    internal void Copy(string path)
    {
        try
        {
            var data = new DataObject();
            data.SetImage(ImageIO.Load(path, 0));
            data.SetFileDropList(new StringCollection { path });
            Clipboard.SetDataObject(data, true);
            ownClipboardSequence = Native.GetClipboardSequenceNumber();
            shelf.ShowStatus("已复制图片");
        }
        catch (Exception e) when (e is ExternalException or IOException or NotSupportedException)
        { shelf.ShowStatus("剪贴板忙碌，请重试"); }
    }

    internal void Preview(string path)
    {
        busy = true;
        try { new PreviewWindow(path).ShowDialog(); }
        finally { busy = false; }
    }

    internal void Drag(FrameworkElement source, string path)
    {
        busy = true;
        try
        {
            var data = new DataObject(DataFormats.FileDrop, new[] { path });
            // Copy only: exporting never silently deletes the original capture.
            DragDrop.DoDragDrop(source, data, DragDropEffects.Copy);
        }
        finally { busy = false; }
    }

    internal void SaveAs(string path)
    {
        busy = true;
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog { FileName = Path.GetFileName(path), Filter = "原始图片|*" + Path.GetExtension(path) };
            if (dialog.ShowDialog() == true && !string.Equals(Path.GetFullPath(path), Path.GetFullPath(dialog.FileName), StringComparison.OrdinalIgnoreCase))
            { File.Copy(path, dialog.FileName, overwrite: true); shelf.ShowStatus("已保存副本"); }
        }
        finally { busy = false; }
    }

    internal void Edit(string path)
    {
        try { Process.Start(new ProcessStartInfo("mspaint.exe") { ArgumentList = { path }, UseShellExecute = true }); Hide(); }
        catch (Win32Exception) { Notify("无法打开画图，可右键选择“用默认应用打开”。"); }
    }

    internal static void Open(string path) => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    internal static void RevealFile(string path) => Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });

    private void ChooseImages()
    {
        busy = true;
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            { Multiselect = true, Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp;*.tif;*.tiff" };
            if (dialog.ShowDialog() == true)
            {
                foreach (var path in dialog.FileNames) AddImage(path, peek: false);
                Reveal(true);
            }
        }
        finally { busy = false; }
    }

    private void ChooseFolder()
    {
        using var dialog = new Forms.FolderBrowserDialog { Description = "只监听这个目录中新建或更新的图片；不扫描历史文件", UseDescriptionForTitle = true };
        if (dialog.ShowDialog() != Forms.DialogResult.OK) return;
        if (!settings.WatchFolders.Contains(dialog.SelectedPath, StringComparer.OrdinalIgnoreCase)) settings.WatchFolders.Add(dialog.SelectedPath);
        Save(); StartWatchers();
    }

    private void ManageFolders()
    {
        var window = new Window { Title = "监听目录", Width = 560, Height = 300, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        var panel = new System.Windows.Controls.DockPanel { Margin = new Thickness(16) };
        var list = new System.Windows.Controls.ListBox { ItemsSource = settings.WatchFolders.ToList() };
        var remove = new System.Windows.Controls.Button { Content = "停止监听选中目录（不删除文件）", Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 12, 0, 0) };
        System.Windows.Controls.DockPanel.SetDock(remove, System.Windows.Controls.Dock.Bottom);
        remove.Click += (_, _) =>
        {
            if (list.SelectedItem is not string folder) return;
            settings.WatchFolders.Remove(folder); list.ItemsSource = settings.WatchFolders.ToList(); Save(); StartWatchers();
        };
        panel.Children.Add(remove); panel.Children.Add(list); window.Content = panel;
        busy = true;
        try { window.ShowDialog(); }
        finally { busy = false; }
    }

    private void Notify(string text) => tray?.ShowBalloonTip(3500, "Snapline", text, Forms.ToolTipIcon.Info);

    public void Dispose()
    {
        disposed = true;
        timer.Stop();
        foreach (var watcher in watchers) watcher.Dispose();
        if (messageWindow != null)
        {
            if (clipboardListening) Native.RemoveClipboardFormatListener(messageWindow.Handle);
            Native.UnregisterHotKey(messageWindow.Handle, Native.ToggleHotkey);
            Native.UnregisterHotKey(messageWindow.Handle, Native.CaptureHotkey);
            messageWindow.Dispose();
        }
        Save();
        if (tray != null) { tray.Visible = false; tray.Icon?.Dispose(); tray.ContextMenuStrip?.Dispose(); tray.Dispose(); }
        handle.Close();
        shelf.Close();
    }
}
