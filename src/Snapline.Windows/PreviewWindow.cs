using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Snapline.Windows;

internal sealed class PreviewWindow : Window
{
    internal PreviewWindow(string path)
    {
        Title = System.IO.Path.GetFileName(path) + " — Snapline";
        Width = 1000; Height = 720;
        MaxWidth = SystemParameters.WorkArea.Width; MaxHeight = SystemParameters.WorkArea.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(24, 31, 42));
        var image = new Image { Source = ImageIO.Load(path, 0), Stretch = Stretch.Uniform, Margin = new Thickness(18) };
        Content = image;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }
}
