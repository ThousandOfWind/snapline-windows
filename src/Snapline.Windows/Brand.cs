using System;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Snapline.Windows;

internal static class Brand
{
    internal static Image Icon(double size) => new()
    {
        Source = new BitmapImage(new Uri("pack://application:,,,/Assets/snapline.png")),
        Width = size, Height = size, Stretch = Stretch.Uniform, IsHitTestVisible = false
    };
}
