using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Snapline.Windows;

internal sealed class ShelfWindow : Window
{
    private readonly SnaplineApplication controller;
    private readonly StackPanel cards = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock status = new() { Foreground = Brush("#9EAFBC"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    private readonly TranslateTransform slide = new();
    private readonly ScrollViewer scroll;
    private readonly DispatcherTimer statusTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly Grid root;
    private readonly Path rope;
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
        var backdrop = new Border
        {
            CornerRadius = new CornerRadius(0, 0, 22, 22),
            Background = new LinearGradientBrush(Color.FromArgb(249, 22, 29, 40), Color.FromArgb(244, 30, 40, 53), 90),
            BorderBrush = Brush("#475466"), BorderThickness = new Thickness(1),
            Margin = new Thickness(12, 0, 12, 12),
            Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Opacity = 0.25 }
        };
        root.Children.Add(backdrop);
        var content = new Grid { Margin = new Thickness(28, 8, 28, 18) };
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var toolbar = new DockPanel { LastChildFill = true };
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(Button("＋ 截图", "区域截图 · Ctrl+Alt+S", () => _ = controller.CaptureAsync()));
        actions.Children.Add(Button("收起 ↑", "隐藏截图栏 · Ctrl+Alt+T", controller.Hide));
        DockPanel.SetDock(actions, Dock.Right);
        toolbar.Children.Add(actions);
        var brand = new TextBlock { Text = "◈  SNAPLINE", Foreground = Brush("#75E5C2"), FontWeight = FontWeights.SemiBold, FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 24, 0) };
        DockPanel.SetDock(brand, Dock.Left);
        toolbar.Children.Add(brand);
        status.Text = "单击复制 · 双击预览 · 拖拽分享 · 右键更多";
        toolbar.Children.Add(status);
        content.Children.Add(toolbar);
        var area = new Grid();
        Grid.SetRow(area, 1);
        rope = new Path { Stroke = Brush("#94AAAD"), StrokeThickness = 1.5, IsHitTestVisible = false };
        area.Children.Add(rope);
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
        area.SizeChanged += (_, _) =>
        {
            var w = area.ActualWidth;
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(new Point(0, 12), false, false);
                context.QuadraticBezierTo(new Point(w / 2, 35), new Point(w, 12), true, false);
            }
            geometry.Freeze(); rope.Data = geometry;
        };
        content.Children.Add(area);
        root.Children.Add(content);
        Content = root;
        SourceInitialized += (_, _) => Native.MakeNonActivating(this);
        Closing += (_, e) => { if (!closing) { e.Cancel = true; controller.Hide(); } };
        statusTimer.Tick += (_, _) => { statusTimer.Stop(); status.Text = "单击复制 · 双击预览 · 拖拽分享 · 右键更多"; };
    }

    internal void Refresh(IReadOnlyList<string> paths)
    {
        var offset = scroll.HorizontalOffset;
        cards.Children.Clear();
        if (paths.Count == 0)
        {
            cards.Children.Add(new TextBlock
            {
                Text = "截图，挂在这里。\n按 Ctrl+Alt+S 截图，或从托盘添加图片。",
                TextAlignment = TextAlignment.Center,
                Foreground = Brush("#A9BBC8"), FontSize = 17, Margin = new Thickness(20, 55, 20, 20)
            });
        }
        else foreach (var path in paths.Reverse())
        {
            if (ImageIO.TryLoad(path, out var image)) cards.Children.Add(CreateCard(path, image!));
        }
        scroll.ScrollToHorizontalOffset(offset);
    }

    private FrameworkElement CreateCard(string path, System.Windows.Media.Imaging.BitmapSource image)
    {
        var card = new Grid { Width = 170, Height = 165, Margin = new Thickness(9, 15, 9, 0), ToolTip = System.IO.Path.GetFileName(path) };
        var tilt = (StringComparer.OrdinalIgnoreCase.GetHashCode(path) % 45) / 20.0;
        card.RenderTransform = new RotateTransform(tilt);
        card.RenderTransformOrigin = new Point(0.5, 0);
        var frame = new Border
        {
            CornerRadius = new CornerRadius(9), Padding = new Thickness(7, 9, 7, 6), Margin = new Thickness(0, 14, 0, 3),
            Background = Brush("#F1F5F6"), BorderBrush = Brush("#BCC9CE"), BorderThickness = new Thickness(1),
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
        card.Children.Add(new Border
        {
            Width = 13, Height = 27, CornerRadius = new CornerRadius(3), Background = Brush("#77D5B7"),
            BorderBrush = Brush("#224C44"), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false,
            Child = new Border { Height = 2, Margin = new Thickness(2), Background = Brush("#477F70") }
        });
        var remove = new Button
        {
            Content = "×", Width = 20, Height = 20, FontSize = 15, Padding = new Thickness(0),
            Background = Brush("#DCE6E7"), BorderThickness = new Thickness(0), Foreground = Brush("#40575A"),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 14, 0, 0), ToolTip = "从栏中移除（保留文件）"
        };
        remove.Click += (_, e) => { e.Handled = true; controller.Remove(path); };
        card.Children.Add(remove);
        var menu = new ContextMenu();
        AddMenu(menu, "复制图片", () => controller.Copy(path));
        AddMenu(menu, "预览", () => controller.Preview(path));
        AddMenu(menu, "用默认应用打开", () => SnaplineApplication.Open(path));
        AddMenu(menu, "用画图编辑", () => controller.Edit(path));
        AddMenu(menu, "另存副本…", () => controller.SaveAs(path));
        AddMenu(menu, "在资源管理器中显示", () => SnaplineApplication.RevealFile(path));
        menu.Items.Add(new Separator());
        AddMenu(menu, "从栏中移除（保留文件）", () => controller.Remove(path));
        card.ContextMenu = menu;

        Point start = default;
        var dragging = false;
        var doubleClicked = false;
        DispatcherTimer? clickTimer = null;
        card.MouseLeftButtonDown += (_, e) =>
        {
            if (e.OriginalSource is Button || IsButtonAncestor(e.OriginalSource as DependencyObject)) return;
            start = e.GetPosition(card); dragging = false; doubleClicked = e.ClickCount == 2;
            if (doubleClicked) { clickTimer?.Stop(); controller.Preview(path); e.Handled = true; }
        };
        card.MouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || dragging || IsButtonAncestor(e.OriginalSource as DependencyObject)) return;
            var current = e.GetPosition(card);
            if (Math.Abs(current.X - start.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(current.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            dragging = true; clickTimer?.Stop(); controller.Drag(card, path);
        };
        card.MouseLeftButtonUp += (_, e) =>
        {
            if (dragging || doubleClicked || e.Handled || IsButtonAncestor(e.OriginalSource as DependencyObject)) return;
            clickTimer?.Stop();
            clickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Forms.SystemInformation.DoubleClickTime) };
            clickTimer.Tick += (_, _) => { clickTimer.Stop(); controller.Copy(path); };
            clickTimer.Start();
        };
        return card;
    }

    private static bool IsButtonAncestor(DependencyObject? source)
    {
        while (source != null) { if (source is Button) return true; source = VisualTreeHelper.GetParent(source); }
        return false;
    }

    internal void Reveal(Forms.Screen screen)
    {
        visibilityGeneration++;
        CurrentScreen = screen; scale = Native.Scale(screen);
        Left = screen.Bounds.Left / scale;
        Top = screen.Bounds.Top / scale;
        Width = screen.Bounds.Width / scale;
        if (!IsRevealed)
        {
            IsRevealed = true;
            if (!IsVisible)
            {
                slide.BeginAnimation(TranslateTransform.YProperty, null);
                slide.Y = -Height;
                Show();
            }
            slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(250))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }
        // Position in physical pixels to handle negative monitor origins and mixed DPI correctly.
        Native.Place(this, screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Width, (int)Math.Round(Height * scale));
        scroll.ScrollToLeftEnd();
    }

    internal void Conceal()
    {
        if (!IsRevealed) return;
        IsRevealed = false;
        var generation = ++visibilityGeneration;
        var animation = new DoubleAnimation(-Height, TimeSpan.FromMilliseconds(200))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        animation.Completed += (_, _) => { if (generation == visibilityGeneration) Hide(); };
        slide.BeginAnimation(TranslateTransform.YProperty, animation);
    }

    internal bool ContainsCursor(System.Drawing.Point cursor) =>
        cursor.X >= CurrentScreen.Bounds.Left && cursor.X < CurrentScreen.Bounds.Right
        && cursor.Y >= CurrentScreen.Bounds.Top && cursor.Y < CurrentScreen.Bounds.Top + Height * scale;

    internal void ShowStatus(string text) { status.Text = text; statusTimer.Stop(); statusTimer.Start(); }
    internal new void Close() { closing = true; statusTimer.Stop(); base.Close(); }

    private static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    private static Button Button(string text, string tooltip, Action action)
    {
        var button = new Button
        {
            Content = text, ToolTip = tooltip, Foreground = Brush("#D8E8EE"), Background = Brush("#344552"),
            BorderThickness = new Thickness(0), Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(4, 0, 0, 0), FontSize = 12
        };
        button.Click += (_, _) => action(); return button;
    }
    private static void AddMenu(ContextMenu menu, string text, Action action)
    {
        var item = new MenuItem { Header = text }; item.Click += (_, _) => action(); menu.Items.Add(item);
    }
}
