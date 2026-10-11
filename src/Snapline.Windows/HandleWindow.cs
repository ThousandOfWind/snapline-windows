using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace Snapline.Windows;

/// <summary>A discoverable, non-activating entry point when the transparent shelf is tucked away.</summary>
internal sealed class HandleWindow : Window
{
    private readonly Button button;

    internal HandleWindow(Action showShelf)
    {
        Title = "Snapline Handle";
        Width = 130; Height = 28;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        button = new Button
        {
            Content = "◈ Snapline ↑", FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(219, 247, 238)),
            Background = new SolidColorBrush(Color.FromArgb(195, 28, 46, 48)),
            BorderThickness = new Thickness(0), Padding = new Thickness(8, 3, 8, 3),
            ToolTip = "点击打开截图栏 · Ctrl+Alt+T · 图片仍保存在本机"
        };
        button.Click += (_, _) => showShelf();
        Content = new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, Child = button };
        SourceInitialized += (_, _) => Native.MakeNonActivating(this);
    }

    internal void Reveal(Forms.Screen screen, int count)
    {
        button.Content = $"◈ Snapline · {count} ↑";
        var scale = Native.Scale(screen);
        if (!IsVisible) Show();
        Native.Place(this, screen.WorkingArea.Right - (int)Math.Round((Width + 12) * scale),
            screen.WorkingArea.Top + (int)Math.Round(6 * scale), (int)Math.Round(Width * scale), (int)Math.Round(Height * scale));
    }
}
