using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Snapline.Core;

namespace Snapline.Windows;

internal static class CardInteractionVerification
{
    internal static async Task RunAsync(ShelfWindow shelf, string data, string output, Action<bool, string> check)
    {
        var source = Path.Combine(data, "Inbox", "smoke.png");
        var card = new Border { Width = 170, Height = 112, Background = Brushes.MediumAquamarine };
        var host = new Window { Title = "Synthetic card input", Content = card, Width = 220, Height = 180, ShowInTaskbar = false };
        var singles = 0; var doubles = 0; var drags = 0; PreviewWindow? preview = null;
        var input = new ImageCardInput(card, () => singles++, () => { doubles++; preview = new PreviewWindow(source); preview.Show(); }, () => drags++, capturePointer: false);
        try
        {
            host.Show();
            input.Press(1000, new ImagePoint(100, 100)); input.Release(1040);
            input.Press(1140, new ImagePoint(100, 100));
            check(doubles == 0 && singles == 0, "second press does not open a modal while button is held");
            input.Release(1180);
            check(doubles == 1 && preview?.IsVisible == true, "card recognizer opens actual preview after second release without WPF ClickCount");
            await Task.Delay(System.Windows.Forms.SystemInformation.DoubleClickTime + 100);
            check(singles == 0 && doubles == 1, "double click never executes delayed copy callback");
            preview?.Close();
            input.Press(2000, new ImagePoint(100, 100)); input.Release(2040);
            await Task.Delay(System.Windows.Forms.SystemInformation.DoubleClickTime + 100);
            check(singles == 1 && doubles == 1, "single click still runs exactly once");
            input.Press(3000, new ImagePoint(100, 100)); input.Move(new ImagePoint(140, 115)); input.Release(3080);
            await Task.Delay(100);
            check(drags == 1 && singles == 1 && doubles == 1, "image drag does not accidentally preview or copy");
            input.Press(4000, new ImagePoint(100, 100)); input.Release(4040); input.Cancel();
            await Task.Delay(System.Windows.Forms.SystemInformation.DoubleClickTime + 100);
            check(singles == 1, "cancel/unload can stop a pending click");
            long tick = 5000; var routedDouble = 0; var routedSingle = 0;
            var routedCard = new Border();
            var routed = new ImageCardInput(routedCard, () => routedSingle++, () => routedDouble++, () => { }, capturePointer: false,
                testPointer: () => new ImagePoint(100, 100), testClock: () => tick);
            void Mouse(System.Windows.RoutedEvent kind)
            {
                routedCard.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, (int)tick,
                    System.Windows.Input.MouseButton.Left) { RoutedEvent = kind });
            }
            Mouse(UIElement.PreviewMouseLeftButtonDownEvent); tick += 40; Mouse(UIElement.PreviewMouseLeftButtonUpEvent);
            tick += 100; Mouse(UIElement.PreviewMouseLeftButtonDownEvent); tick += 40; Mouse(UIElement.PreviewMouseLeftButtonUpEvent);
            check(routedDouble == 1, "actual WPF preview-event bindings recognize double release without setting ClickCount");
            await Task.Delay(System.Windows.Forms.SystemInformation.DoubleClickTime + 100);
            check(routedSingle == 0, "routed double-click does not leak a delayed single callback");
            routed.Cancel();
        }
        finally { input.Cancel(); preview?.Close(); host.Close(); }
        shelf.Reveal(System.Windows.Forms.Screen.PrimaryScreen!); await Task.Delay(250); shelf.FinishMotion(); shelf.UpdateLayout();
        var frames = Descendants(shelf).OfType<Border>().Where(e => Equals(e.Tag, "PhotoPaperFrame")).ToArray();
        check(frames.Length > 0 && frames.All(e => e.Padding == new Thickness(0)), "image cards have zero internal padding");
        var pictures = Descendants(shelf).OfType<Image>().Where(e => Equals(e.Tag, "ScreenshotPixels")).ToArray();
        check(pictures.Length > 0 && pictures.All(p => p.Margin == new Thickness(0)), "thumbnail pixels have no synthetic inset margins");
        check(pictures.All(p => p.Source is System.Windows.Media.Imaging.BitmapSource b
            && Math.Abs(p.Width / p.Height - b.PixelWidth / (double)b.PixelHeight) < 1e-7), "thumbnail viewport matches pixel aspect ratio instead of a fixed letterbox");
        check(!Descendants(shelf).OfType<TextBlock>().Any(t => t.Text == "smoke.png"), "filename is tooltip/accessibility metadata rather than a white footer");
        shelf.SaveTestRender(Path.Combine(output, "tight-image-cards.png"));
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var item in Descendants(child)) yield return item;
        }
    }
}
