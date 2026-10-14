using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Snapline.Windows;

internal sealed class PreviewWindow : Window
{
    internal bool EditRequested { get; private set; }
    internal PreviewWindow(string path)
    {
        Title = System.IO.Path.GetFileName(path) + " — Snapline";
        SizeToContent = SizeToContent.WidthAndHeight;
        MaxWidth = SystemParameters.WorkArea.Width; MaxHeight = SystemParameters.WorkArea.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(24, 31, 42));
        var bitmap = ImageIO.Load(path, 0);
        var fit = Snapline.Core.ImageCardSize.Fit(bitmap.PixelWidth, bitmap.PixelHeight,
            System.Math.Max(100, SystemParameters.WorkArea.Width - 40), System.Math.Max(100, SystemParameters.WorkArea.Height - 125));
        var image = new Image { Source = bitmap, Width = fit.Width, Height = fit.Height, Stretch = Stretch.Fill, Margin = new Thickness(0), Tag = "PreviewPixels" };
        var panel = new DockPanel();
        var edit = PaperStyle.Action("编辑 / 标注", "M0,14 L3,7 12,0 16,4 7,13 Z", "打开内置编辑器，保存为副本，不覆盖原图", true,
            () => { EditRequested = true; Close(); });
        System.Windows.Automation.AutomationProperties.SetAutomationId(edit, "PreviewEdit");
        edit.HorizontalAlignment = HorizontalAlignment.Right; edit.Margin = new Thickness(12);
        DockPanel.SetDock(edit, Dock.Top); panel.Children.Add(edit); panel.Children.Add(image); Content = panel;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }
}
