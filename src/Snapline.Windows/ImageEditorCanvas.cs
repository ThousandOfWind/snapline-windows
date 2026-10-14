using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snapline.Core;

namespace Snapline.Windows;

internal sealed class ImageEditorCanvas : FrameworkElement
{
    internal BitmapSource? Image { get; set; }
    internal ImageRegion Crop { get; set; }
    internal double Zoom { get; set; } = 1;
    internal ImageMark? Draft { get; set; }
    internal ImageRegion? CropDraft { get; set; }

    protected override void OnRender(DrawingContext draw)
    {
        base.OnRender(draw);
        draw.DrawRectangle(Brushes.White, new Pen(Brushes.LightGray, 1), new Rect(0, 0, ActualWidth, ActualHeight));
        if (Image != null) draw.DrawImage(Image, new Rect(0, 0, ActualWidth, ActualHeight));
        draw.PushTransform(new ScaleTransform(Zoom, Zoom));
        draw.PushTransform(new TranslateTransform(-Crop.X, -Crop.Y));
        draw.PushClip(new RectangleGeometry(new Rect(Crop.X, Crop.Y, Crop.Width, Crop.Height)));
        if (Draft != null) AnnotationRenderer.Draw(draw, Draft);
        if (CropDraft is { } box)
        {
            var pen = new Pen(Brushes.MediumSeaGreen, 1.5 / Zoom) { DashStyle = DashStyles.Dash };
            draw.DrawRectangle(new SolidColorBrush(Color.FromArgb(35, 73, 177, 135)), pen, new Rect(box.X, box.Y, box.Width, box.Height));
        }
        draw.Pop(); draw.Pop(); draw.Pop();
    }
}
