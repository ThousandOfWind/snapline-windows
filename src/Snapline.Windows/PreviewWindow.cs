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
        Width = 1000; Height = 720;
        MaxWidth = SystemParameters.WorkArea.Width; MaxHeight = SystemParameters.WorkArea.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(24, 31, 42));
        var image = new Image { Source = ImageIO.Load(path, 0), Stretch = Stretch.Uniform, Margin = new Thickness(18) };
        var panel = new DockPanel();
        var edit = PaperStyle.Action("编辑 / 标注", "M0,14 L3,7 12,0 16,4 7,13 Z", "打开内置编辑器，保存为副本，不覆盖原图", true,
            () => { EditRequested = true; Close(); });
        System.Windows.Automation.AutomationProperties.SetAutomationId(edit, "PreviewEdit");
        edit.HorizontalAlignment = HorizontalAlignment.Right; edit.Margin = new Thickness(12);
        DockPanel.SetDock(edit, Dock.Top); panel.Children.Add(edit); panel.Children.Add(image); Content = panel;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }
}
