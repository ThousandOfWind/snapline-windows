using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Snapline.Core;

namespace Snapline.Windows;

internal sealed partial class ShelfWindow : Window
{
    private readonly SnaplineApplication controller;
    private readonly StackPanel cards = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock status = new() { Foreground = Brush("#9EAFBC"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    private readonly TranslateTransform slide = new();
    private readonly ScrollViewer scroll;
    private readonly DispatcherTimer statusTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly Grid root;
    private readonly Border brandGrip;
    private readonly Grid ropeArea;
    private readonly System.Windows.Shapes.Path rope;
    private readonly TranslateTransform ropeOffset = new();
    private readonly SpringMotion ropeDip;
    private readonly Canvas ropeFeedback = new() { IsHitTestVisible = false };
    private Border emptyPeg;
    private HangingKind previewKind = HangingKind.Window;
    private readonly Border dropLabel;
    private bool nearRope;
    private bool ropeHighlighted;
    private Point ropePoint;
    private readonly TextBlock welcome;
    private string[] imagePaths = [];
    private IReadOnlyList<PocketWindow> pocketItems = Array.Empty<PocketWindow>();
    private readonly Dictionary<string, FrameworkElement> cardViews = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HangingCard> hanging = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> pendingEntry = new(StringComparer.OrdinalIgnoreCase);
    private int removalGeneration;
    private bool removing;
    private bool cardDragging;
    private string[]? deferredPaths;
    private bool closing;
    private double scale = 1;
    private int visibilityGeneration;

    internal bool IsRevealed { get; private set; }
    internal Forms.Screen CurrentScreen { get; private set; } = Forms.Screen.PrimaryScreen!;

    internal ShelfWindow(SnaplineApplication controller)
    {
        this.controller = controller;
        Title = "Snapline";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Height = 246;
        root = new Grid { RenderTransform = slide };
        // No full-width backdrop: pixels between cards remain completely transparent.
        var content = new Grid { Margin = new Thickness(28, 8, 28, 18) };
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var toolbar = new DockPanel { LastChildFill = true };
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(PaperStyle.Action("截图", PaperStyle.Capture, "区域截图 · Ctrl+Alt+S", true, () => _ = controller.CaptureAsync()));
        actions.Children.Add(PaperStyle.Action("收起", PaperStyle.Fold, "隐藏截图栏 · Ctrl+Alt+T", false, controller.Hide));
        DockPanel.SetDock(actions, Dock.Right);
        toolbar.Children.Add(actions);
        var identity = new StackPanel { Orientation = Orientation.Horizontal };
        identity.Children.Add(Brand.Icon(30));
        identity.Children.Add(new TextBlock { Text = "Snapline", Foreground = Brush("#466C58"), FontWeight = FontWeights.SemiBold,
            FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 12, 0) });
        identity.Children.Add(new TextBlock { Text = "拖动移动", Foreground = Brush("#A9C6BF"), FontSize = 10, VerticalAlignment = VerticalAlignment.Center });
        var brand = new Border { Child = identity, Tag = "SnaplineGrip", ToolTip = "按住这里拖动截图栏；图片卡片拖动用于分享",
            Background = Brushes.Transparent, CornerRadius = new CornerRadius(12), Padding = new Thickness(3, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 20, 0) };
        brandGrip = brand;
        System.Windows.Automation.AutomationProperties.SetAutomationId(brand, "ShelfDragGrip");
        _ = new ComponentDrag(this, brand, controller.BeginComponentMove, () =>
        {
            CurrentScreen = Forms.Screen.FromRectangle(Native.Bounds(new System.Windows.Interop.WindowInteropHelper(this).Handle));
            controller.EndComponentMove(isShelf: true);
        });
        DockPanel.SetDock(brand, Dock.Left);
        toolbar.Children.Add(brand);
        status.Text = "单击复制 · 双击预览 · 拖拽分享 · 右键更多";
        status.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 4, ShadowDepth = 1, Opacity = 0.9 };
        toolbar.Children.Add(status);
        content.Children.Add(toolbar);
        var area = new Grid(); ropeArea = area;
        Grid.SetRow(area, 1);
        rope = new System.Windows.Shapes.Path { Stroke = Brush("#8CAA9B"), StrokeThickness = 1.3,
            IsHitTestVisible = false, Tag = "SnaplineRope", RenderTransform = ropeOffset };
        ropeDip = new SpringMotion(ropeOffset, TranslateTransform.YProperty, 0);
        area.Children.Add(rope);
        area.SizeChanged += (_, _) =>
        {
            var width = area.ActualWidth;
            var geometry = new StreamGeometry();
            using (var drawing = geometry.Open())
            {
                drawing.BeginFigure(new Point(0, 17), false, false);
                drawing.BezierTo(new Point(width / 3, 29), new Point(width * 2 / 3, 29), new Point(width, 17), true, false);
            }
            geometry.Freeze(); rope.Data = geometry;
        };
        scroll = new ScrollViewer
        {
            Content = cards,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(10, 0, 10, 0),
            PanningMode = PanningMode.HorizontalOnly
        };
        scroll.PreviewMouseWheel += (_, e) => { scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset - e.Delta); e.Handled = true; };
        area.Children.Add(scroll);
        emptyPeg = PaperStyle.Clip(HangingKind.Window); emptyPeg.Opacity = 0.9; emptyPeg.Tag = "WindowDropPeg";
        ropeFeedback.Children.Add(emptyPeg);
        dropLabel = new Border { Child = new TextBlock { Text = "松手挂上窗口", FontSize = 11, Foreground = Brush("#355748") },
            CornerRadius = new CornerRadius(9), Background = Brush("#FBE4C4"), Padding = new Thickness(9, 5, 9, 5),
            BorderBrush = Brush("#EAAF75"), BorderThickness = new Thickness(1) };
        ropeFeedback.Children.Add(dropLabel); ropeFeedback.Visibility = Visibility.Hidden;
        area.Children.Add(ropeFeedback);
        welcome = new TextBlock { Text = "将软件标题栏拖到绳子上，松手挂好", FontSize = 11, Foreground = Brush("#668675"),
            IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, 0, 0) };
        area.Children.Add(welcome);
        content.Children.Add(area);
        root.Children.Add(content);
        Content = root;
        SourceInitialized += (_, _) =>
        {
            Native.MakeNonActivating(this);
            System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle)?.AddHook(HitTestHook);
        };
        Closing += (_, e) => { if (!closing) { e.Cancel = true; controller.Hide(); } };
        statusTimer.Tick += (_, _) => { statusTimer.Stop(); status.Text = "单击复制 · 双击预览 · 拖拽分享 · 右键更多"; };
    }

    private IntPtr HitTestHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != 0x0084) return IntPtr.Zero; // WM_NCHITTEST
        var value = lParam.ToInt64();
        var point = PointFromScreen(new Point(unchecked((short)(value & 0xffff)), unchecked((short)((value >> 16) & 0xffff))));
        // Only visible controls/cards catch input, not the transparent full-screen strip.
        if (InputHitTest(point) is not DependencyObject hit || !IsInteractive(hit))
        {
            handled = true;
            return new IntPtr(-1); // HTTRANSPARENT
        }
        return IntPtr.Zero;
    }

    private static bool IsInteractive(DependencyObject hit)
    {
        for (DependencyObject? node = hit; node != null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is Button || node is FrameworkElement { Tag: "SnaplineCard" or "SnaplineGrip" }) return true;
        }
        return false;
    }

    internal void Refresh(IReadOnlyList<string> paths)
    {
        if (cardDragging) { deferredPaths = paths.ToArray(); return; }
        imagePaths = paths.ToArray();
        var offset = scroll.HorizontalOffset;
        var existing = new HashSet<string>(cardViews.Keys, StringComparer.OrdinalIgnoreCase);
        cards.Children.Clear();
        cardViews.Clear(); hanging.Clear();
        pendingEntry.IntersectWith(paths);
        foreach (var window in pocketItems)
        {
            var key = WindowKey(window);
            var card = CreateWindowCard(window, key);
            cardViews[key] = card; cards.Children.Add(card);
            if (!existing.Contains(key))
            {
                pendingEntry.Add(key);
                if (IsRevealed) card.Loaded += (_, _) => { if (pendingEntry.Remove(key)) hanging[key].Hang(); };
            }
        }
        if (paths.Count == 0 && pocketItems.Count == 0)
        {
            cards.Children.Add(new TextBlock
            {
                Text = "截图，挂在这里。\n按 Ctrl+Alt+S 截图，或从托盘添加图片。",
                TextAlignment = TextAlignment.Center,
                Foreground = Brush("#A9BBC8"), FontSize = 17, Margin = new Thickness(20, 55, 20, 20)
            });
        }
        else
        {
            var rendered = new HashSet<Guid>();
            foreach (var path in paths.Reverse())
            {
                var group = controller.ImageGroupFor(path);
                if (group != null)
                {
                    if (!rendered.Add(group.Id)) continue;
                    var key = GroupKey(group.Id);
                    var groupedCard = CreateGroupCard(group, key);
                    AddImageView(key, groupedCard, existing);
                    if (expandedGroups.Contains(group.Id))
                        foreach (var member in group.Members)
                            if (ImageIO.TryLoad(member, out var childImage)) AddImageView(member, CreateCard(member, childImage!), existing);
                }
                else if (ImageIO.TryLoad(path, out var image)) AddImageView(path, CreateCard(path, image!), existing);
            }
            expandedGroups.RemoveWhere(id => !rendered.Contains(id));
        }
        scroll.ScrollToHorizontalOffset(offset);
    }

    internal void RefreshWindows(IReadOnlyList<PocketWindow> windows)
    {
        pocketItems = windows.ToArray(); Refresh(imagePaths);
    }

    internal bool WindowDropTarget(System.Drawing.Point cursor)
    {
        nearRope = false;
        if (!IsVisible || !IsRevealed || ropeArea.ActualWidth <= 0) return false;
        var local = ropeArea.PointFromScreen(new Point(cursor.X, cursor.Y));
        var x = Math.Clamp(local.X, 8, Math.Max(8, ropeArea.ActualWidth - 8));
        var t = x / ropeArea.ActualWidth;
        var lineY = 17 + 36 * t * (1 - t); // Match the gentle cubic rope curve; ignore decorative motion for stable hit geometry.
        nearRope = local.X >= 0 && local.X <= ropeArea.ActualWidth && Math.Abs(local.Y - lineY) <= 22;
        ropePoint = new Point(x, lineY);
        return nearRope;
    }

    internal void WindowMoveHint(bool moving, bool ready, HangingKind kind = HangingKind.Window)
    {
        var theme = HangingTheme.For(kind);
        if (kind != previewKind)
        {
            previewKind = kind; ropeFeedback.Children.Remove(emptyPeg); emptyPeg = PaperStyle.Clip(kind);
            emptyPeg.Opacity = 0.9; emptyPeg.Tag = "WindowDropPeg"; ropeFeedback.Children.Insert(0, emptyPeg);
        }
        dropLabel.Background = Brush(theme.Header); dropLabel.BorderBrush = Brush(theme.Edge);
        brandGrip.Background = Brushes.Transparent;
        welcome.Visibility = moving ? Visibility.Hidden : Visibility.Visible;
        if (moving) status.Text = ready ? "松手挂上窗口 · 移开或 Esc 取消" : "将软件标题栏拖到整条晾衣绳，松手挂上";
        else status.Text = "单击复制 · 双击预览 · 拖拽分享 · 右键更多";
        var active = moving && ready && nearRope;
        if (active != ropeHighlighted)
        {
            ropeHighlighted = active;
            ropeDip.To(active ? 4 : 0, impulse: active ? 16 : 0, frequency: 22, damping: 0.48, duration: 0.45);
            rope.Stroke = Brush(active ? theme.Clip : "#8CAA9B"); rope.StrokeThickness = active ? 2 : 1.3;
        }
        ropeFeedback.Visibility = active ? Visibility.Visible : Visibility.Hidden;
        if (active)
        {
            Canvas.SetLeft(emptyPeg, ropePoint.X - 7); Canvas.SetTop(emptyPeg, ropePoint.Y - 9);
            Canvas.SetLeft(dropLabel, Math.Clamp(ropePoint.X - 54, 0, Math.Max(0, ropeArea.ActualWidth - 120)));
            Canvas.SetTop(dropLabel, ropePoint.Y + 28);
        }
        if (!moving) { nearRope = false; ropeDip.Reset(); }
    }

    internal System.Drawing.Point RopeTargetPoint(double fraction)
    {
        var width = ropeArea.ActualWidth;
        var x = Math.Clamp(fraction, 0.05, 0.95) * width;
        var t = x / width;
        var point = ropeArea.PointToScreen(new Point(x, 17 + 36 * t * (1 - t)));
        return new System.Drawing.Point((int)Math.Round(point.X), (int)Math.Round(point.Y));
    }

    internal bool RopeFeedbackVisible => ropeFeedback.Visibility == Visibility.Visible;

    private static string WindowKey(PocketWindow window) => $"window:{window.Identity.Handle}:{window.Identity.ProcessStarted}";

    private FrameworkElement CreateWindowCard(PocketWindow window, string key)
    {
        var card = new Grid { Width = 170, Height = 165, Margin = new Thickness(9, 15, 9, 0), Tag = "SnaplineCard",
            ToolTip = "暂存的软件窗口：" + window.Title + " · 点击取回原窗口" };
        var theme = HangingTheme.For(window.Kind);
        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25) });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var titleBar = new DockPanel { LastChildFill = true, Background = Brush(theme.Header), Margin = new Thickness(0), Tag = "WindowTitleBar" };
        var controls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 7, 0) };
        controls.Children.Add(PaperStyle.Symbol("M0,0 L7,0", 7));
        controls.Children.Add(new Border { Width = 7, Height = 6, BorderThickness = new Thickness(1), BorderBrush = Brush(theme.Ink), Margin = new Thickness(7, 0, 0, 0) });
        DockPanel.SetDock(controls, Dock.Right); titleBar.Children.Add(controls);
        titleBar.Children.Add(new TextBlock { Text = window.Kind == HangingKind.FolderWindow ? "文件夹" : "运行中窗口", FontSize = 10, Foreground = Brush(theme.Ink),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });
        content.Children.Add(titleBar);
        var body = new StackPanel { Margin = new Thickness(8, 8, 8, 5) };
        body.Children.Add(window.Icon == null ? Brand.Icon(36) : new Image { Source = window.Icon, Width = 36, Height = 36 });
        body.Children.Add(new TextBlock { Text = window.Title, FontSize = 11, Foreground = Brush(theme.Ink), TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 6, 0, 4) });
        body.Children.Add(new TextBlock { Text = "点击取回 ↗", FontSize = 10, Foreground = Brush(theme.Ink), TextAlignment = TextAlignment.Center });
        Grid.SetRow(body, 1); content.Children.Add(body);
        var restore = new Button { Content = content, Cursor = Cursors.Hand, Background = Brush(theme.Paper), BorderBrush = Brush(theme.Edge),
            BorderThickness = new Thickness(1.5), Padding = new Thickness(0), Margin = new Thickness(0, 14, 0, 3),
            Template = PaperStyle.Template(radius: 7, hoverColor: window.Kind == HangingKind.FolderWindow ? "#FFF0C4" : "#E2EFF8",
                pressedColor: window.Kind == HangingKind.FolderWindow ? "#F1D795" : "#C0D5E5"),
            Tag = window.Kind == HangingKind.FolderWindow ? "FolderWindowFrame" : "AppWindowFrame" };
        System.Windows.Automation.AutomationProperties.SetName(restore, "取回窗口 " + window.Title);
        System.Windows.Automation.AutomationProperties.SetAutomationId(restore, "RestoreWindow");
        restore.Click += (_, _) => _ = controller.TakeWindowAsync(window.Identity);
        card.Children.Add(restore);
        var peg = PaperStyle.Clip(window.Kind); peg.Tag = "SnaplinePeg";
        peg.Resources["HangingKind"] = window.Kind;
        peg.HorizontalAlignment = HorizontalAlignment.Center; peg.VerticalAlignment = VerticalAlignment.Top;
        card.Children.Add(peg);
        var movement = new HangingCard(card, peg, StableTilt(key), window.Kind); hanging[key] = movement;
        card.MouseEnter += (_, _) => movement.Hover(true); card.MouseLeave += (_, _) => movement.Hover(false);
        var menu = new ContextMenu();
        AddMenu(menu, "取回原窗口", () => _ = controller.TakeWindowAsync(window.Identity));
        AddMenu(menu, "取回全部窗口", () => _ = controller.TakeAllWindowsAsync());
        card.ContextMenu = menu;
        return card;
    }

    private FrameworkElement CreateCard(string path, System.Windows.Media.Imaging.BitmapSource image)
    {
        var card = new Grid { Width = 170, Height = 165, Margin = new Thickness(9, 15, 9, 0), ToolTip = System.IO.Path.GetFileName(path), Tag = "SnaplineCard" };
        var theme = HangingTheme.For(HangingKind.Image);
        var peg = PaperStyle.Clip(HangingKind.Image);
        peg.Resources["HangingKind"] = HangingKind.Image;
        peg.HorizontalAlignment = HorizontalAlignment.Center;
        peg.VerticalAlignment = VerticalAlignment.Top;
        peg.Tag = "SnaplinePeg";
        var tilt = StableTilt(path);
        var movement = new HangingCard(card, peg, tilt);
        hanging[path] = movement;
        card.MouseEnter += (_, _) => movement.Hover(true);
        card.MouseLeave += (_, _) => movement.Hover(false);
        var frame = new Border
        {
            CornerRadius = new CornerRadius(12), Padding = new Thickness(6, 6, 6, 5), Margin = new Thickness(0, 14, 0, 3),
            Background = Brush(theme.Paper), BorderBrush = Brush(theme.Edge), BorderThickness = new Thickness(1), Tag = "PhotoPaperFrame",
            Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 4, Opacity = 0.22 }
        };
        var body = new Grid();
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(23) });
        body.Children.Add(new Border
        {
            Background = Brush("#DDE4E8"), CornerRadius = new CornerRadius(4), ClipToBounds = true,
            Child = new Image { Source = image, Stretch = Stretch.Uniform }
        });
        var name = new TextBlock
        {
            Text = System.IO.Path.GetFileName(path), FontSize = 10, Foreground = Brush("#445460"),
            TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(name, 1); body.Children.Add(name); frame.Child = body; card.Children.Add(frame);
        card.Children.Add(peg);
        var remove = new Button
        {
            Content = PaperStyle.Symbol(PaperStyle.Cross, 7), Width = 22, Height = 22, Padding = new Thickness(5),
            Background = Brush("#EDF3E9"), BorderThickness = new Thickness(0), Foreground = Brush("#809589"),
            Cursor = Cursors.Hand, Template = PaperStyle.Template(radius: 11),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 14, 0, 0), ToolTip = "从栏中移除（保留文件）"
        };
        PaperStyle.AttachHover(remove);
        System.Windows.Automation.AutomationProperties.SetName(remove, "移除截图");
        System.Windows.Automation.AutomationProperties.SetAutomationId(remove, "RemoveCard");
        remove.Click += (_, e) => { e.Handled = true; ReleaseCard(path); };
        card.Children.Add(remove);
        var menu = new ContextMenu();
        AddMenu(menu, "复制图片", () => controller.Copy(path));
        AddMenu(menu, "预览", () => controller.Preview(path));
        AddMenu(menu, "用默认应用打开", () => SnaplineApplication.Open(path));
        AddMenu(menu, "用画图编辑", () => controller.Edit(path));
        AddMenu(menu, "另存副本…", () => controller.SaveAs(path));
        AddMenu(menu, "在资源管理器中显示", () => SnaplineApplication.RevealFile(path));
        menu.Items.Add(new Separator());
        AddMenu(menu, "从栏中移除（保留文件）", () => ReleaseCard(path));
        var membership = controller.ImageGroupFor(path);
        if (membership != null) AddMenu(menu, "移出截图组（保留图片）", () => controller.TakeOutImage(membership.Id, path));
        card.ContextMenu = menu;
        BindImageActions(card, () => new[] { path }, movement, () => controller.Copy(path), () => controller.Preview(path));
        BindGroupDrop(card, frame, path);
        return card;
    }

    private static bool IsButtonAncestor(DependencyObject? source)
    {
        while (source != null) { if (source is Button) return true; source = VisualTreeHelper.GetParent(source); }
        return false;
    }

    internal void Reveal(Forms.Screen screen, WindowPosition? saved = null)
    {
        visibilityGeneration++;
        var opening = !IsRevealed;
        CurrentScreen = screen = WindowPlacement.Screen(saved, screen);
        scale = Native.Scale(screen);
        Left = screen.Bounds.Left / scale;
        Top = screen.Bounds.Top / scale;
        // A screen-wide window has no horizontal travel. Saved/moved shelves use a compact floating width.
        Width = saved == null ? screen.Bounds.Width / scale : Math.Min(960, screen.WorkingArea.Width / scale);
        IsRevealed = true;
        if (!IsVisible) Show();
        // Reset cancelled clocks before entry; no movement ever exceeds ten DIPs.
        Motion.Reset(root, slide);
        // Position in physical pixels to handle negative monitor origins and mixed DPI correctly.
        var width = (int)Math.Round(Width * scale);
        var height = (int)Math.Round(Height * scale);
        var position = saved == null ? new System.Drawing.Point(screen.Bounds.Left, screen.Bounds.Top)
            : WindowPlacement.Restore(saved, screen.WorkingArea, width, height);
        Native.Place(this, position.X, position.Y, width, height);
        scroll.ScrollToLeftEnd();
        if (opening) Motion.Enter(root, slide);
        var delay = 0;
        foreach (var path in pendingEntry.ToArray())
        {
            if (hanging.TryGetValue(path, out var motion)) { motion.Hang(Math.Min(delay, 100)); delay += 25; }
            pendingEntry.Remove(path);
        }
    }

    internal void Conceal()
    {
        if (!IsRevealed) return;
        removalGeneration++; removing = false;
        IsRevealed = false;
        var generation = ++visibilityGeneration;
        Motion.Exit(root, slide, () => { if (generation == visibilityGeneration && !IsRevealed) Hide(); });
    }

    internal void PrepareForMove()
    {
        Motion.Reset(root, slide);
        Width = Math.Min(960, CurrentScreen.WorkingArea.Width / Native.Scale(CurrentScreen));
    }

    internal bool ContainsCursor(System.Drawing.Point cursor) =>
        Native.Bounds(new System.Windows.Interop.WindowInteropHelper(this).Handle).Contains(cursor);

    internal void SaveTestRender(string path)
    {
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        ImageIO.SavePng(bitmap, path);
    }

    internal void FinishMotion()
    {
        Motion.Reset(root, slide);
        ropeDip.Reset();
        removalGeneration++; removing = false;
        foreach (var motion in hanging.Values) motion.Settle();
        ResetControlHover(this);
    }

    private static void ResetControlHover(DependencyObject root)
    {
        if (root is Button button) PaperStyle.Feedback(button)?.Reset();
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) ResetControlHover(VisualTreeHelper.GetChild(root, i));
    }

    internal void ShowStatus(string text) { status.Text = text; statusTimer.Stop(); statusTimer.Start(); }
    internal new void Close() { closing = true; statusTimer.Stop(); base.Close(); }

    private static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    internal bool HangingSettled => hanging.Values.All(motion => motion.Settled);
    internal void Copied(string path) { if (hanging.TryGetValue(path, out var motion)) motion.Copied(); }

    private void ReleaseCard(string path)
    {
        if (removing || !hanging.TryGetValue(path, out var motion)) return;
        removing = true;
        var generation = ++removalGeneration;
        motion.Release(() =>
        {
            if (generation != removalGeneration) return;
            removing = false;
            controller.Remove(path);
        });
    }

    private static double StableTilt(string path)
    {
        uint hash = 2166136261;
        foreach (var letter in path.ToUpperInvariant()) hash = unchecked((hash ^ letter) * 16777619);
        return (hash % 41 - 20.0) / 10;
    }

    private static void AddMenu(ContextMenu menu, string text, Action action)
    {
        var item = new MenuItem { Header = text }; item.Click += (_, _) => action(); menu.Items.Add(item);
    }
}
