using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Forms = System.Windows.Forms;
using Snapline.Core;

namespace Snapline.Windows;

/// <summary>A miniature hanging photo, not a blocky toolbar button.</summary>
internal sealed class HandleWindow : Window
{
    private readonly TextBlock caption;
    private readonly TranslateTransform slide = new();
    private readonly Canvas visual;
    private readonly HoverMotion hover;
    private readonly HoverMotion countHover;
    private readonly Border dropHint;

    internal HandleWindow(Action showShelf, Action moveStarted, Action moveFinished)
    {
        Title = "Snapline Handle";
        Width = 120; Height = 68;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        visual = new Canvas { Width = 112, Height = 62 };
        // Rope and clip remain fixed. Only the small paper photo receives the pendulum impulse.
        var rope = new System.Windows.Shapes.Path { Data = Geometry.Parse("M5,17 C30,23 48,23 65,17"),
            Stroke = PaperStyle.Brush("#8CAA9B"), StrokeThickness = 1.2, IsHitTestVisible = false };
        visual.Children.Add(rope);
        var paper = new Grid { IsHitTestVisible = false };
        paper.Children.Add(new Border { Margin = new Thickness(5, 6, 5, 13), CornerRadius = new CornerRadius(4), Background = PaperStyle.Brush("#B5DBCC") });
        paper.Children.Add(new Border { Margin = new Thickness(12, 14, 12, 0), Height = 2.4, VerticalAlignment = VerticalAlignment.Top, Background = PaperStyle.Brush("#438374"), CornerRadius = new CornerRadius(1) });
        paper.Children.Add(new Border { Margin = new Thickness(12, 21, 18, 0), Height = 2.4, VerticalAlignment = VerticalAlignment.Top, Background = PaperStyle.Brush("#438374"), CornerRadius = new CornerRadius(1) });
        var photo = new Border { Width = 44, Height = 44, Child = paper, Background = PaperStyle.Brush("#F9FCF6"),
            CornerRadius = new CornerRadius(7), RenderTransformOrigin = new Point(0.5, 0),
            Effect = new DropShadowEffect { Color = Color.FromRgb(30, 55, 43), BlurRadius = 5, ShadowDepth = 2, Opacity = 0.12 } };
        Canvas.SetLeft(photo, 13); Canvas.SetTop(photo, 23); visual.Children.Add(photo);
        var peg = PaperStyle.Peg(9, 21); Canvas.SetLeft(peg, 30.5); Canvas.SetTop(peg, 7); visual.Children.Add(peg);
        caption = new TextBlock { Text = "0 张", FontSize = 10, Foreground = PaperStyle.Brush("#547161") };
        var count = new Border { Child = caption, CornerRadius = new CornerRadius(10), Background = PaperStyle.Brush("#F3F7EE"),
            Padding = new Thickness(8, 4, 8, 4),
            Effect = new DropShadowEffect { Color = Color.FromRgb(30, 55, 43), BlurRadius = 5, ShadowDepth = 2, Opacity = 0.12 } };
        Canvas.SetLeft(count, 66); Canvas.SetTop(count, 26); visual.Children.Add(count);
        dropHint = new Border { CornerRadius = new CornerRadius(12), Background = PaperStyle.Brush("#DAEDDF"),
            BorderBrush = PaperStyle.Brush("#B2D3BD"), BorderThickness = new Thickness(1), Padding = new Thickness(7, 5, 7, 5),
            Child = new TextBlock { Text = "拖到这里暂存", FontSize = 10, Foreground = PaperStyle.Brush("#355748") },
            Visibility = Visibility.Hidden, IsHitTestVisible = false };
        Canvas.SetLeft(dropHint, 4); Canvas.SetTop(dropHint, 12); visual.Children.Add(dropHint);
        var button = new Button
        {
            Content = visual, Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Padding = new Thickness(0), Template = PaperStyle.Template(transparent: true),
            ToolTip = "点击展开截图栏 · 按住小照片拖动位置 · Ctrl+Alt+T"
        };
        System.Windows.Automation.AutomationProperties.SetName(button, "展开截图栏");
        System.Windows.Automation.AutomationProperties.SetAutomationId(button, "HandleOpen");
        button.Click += (_, _) => showShelf();
        // The photo and count tag have separate pivots and spring periods; the entry's hit box stays fixed.
        hover = new HoverMotion(button, photo, pendant: true);
        countHover = new HoverMotion(button, count, pendant: true, secondary: true);
        _ = new ComponentDrag(this, button, () =>
        {
            hover.Suspend(true); countHover.Suspend(true); moveStarted();
        }, () => { hover.Suspend(false); countHover.Suspend(false); moveFinished(); }, showShelf);
        Content = new Border { Child = button, RenderTransform = slide };
        SourceInitialized += (_, _) => Native.MakeNonActivating(this);
    }

    internal bool ContainsCursor(System.Drawing.Point cursor) => IsVisible
        && Native.Bounds(new WindowInteropHelper(this).Handle).Contains(cursor);

    internal void WindowMoveHint(bool moving, bool ready)
    {
        dropHint.Visibility = moving ? Visibility.Visible : Visibility.Hidden;
        ((TextBlock)dropHint.Child).Text = ready ? "松手挂上窗口" : "拖到绳子上";
        dropHint.Background = PaperStyle.Brush(ready ? "#FBE4C4" : "#DAEDDF");
    }

    internal void Reveal(Forms.Screen screen, int count, WindowPosition? saved = null, int windowCount = 0)
    {
        screen = WindowPlacement.Screen(saved, screen);
        Width = windowCount == 0 ? 120 : 160;
        caption.Text = windowCount == 0 ? $"{count} 张" : $"{count} 图 · {windowCount} 窗";
        var opening = !IsVisible;
        var scale = Native.Scale(screen);
        if (!IsVisible) Show();
        var width = (int)Math.Round(Width * scale); var height = (int)Math.Round(Height * scale);
        var position = saved == null ? new System.Drawing.Point(screen.WorkingArea.Right - (int)Math.Round((Width + 12) * scale),
            screen.WorkingArea.Top + (int)Math.Round(6 * scale)) : WindowPlacement.Restore(saved, screen.WorkingArea, width, height);
        Native.Place(this, position.X, position.Y, width, height);
        if (opening) Motion.Enter((Border)Content, slide);
    }

    internal void SaveTestRender(string path)
    {
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render((Border)Content); ImageIO.SavePng(bitmap, path);
    }

    internal HoverMotion HoverFeedback => hover;
    internal HoverMotion CountFeedback => countHover;
    internal void FinishMotion() { Motion.Reset((Border)Content, slide); hover.Reset(); countHover.Reset(); }
}
