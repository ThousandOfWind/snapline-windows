using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace Snapline.Windows;

/// <summary>Isolated real WPF rendering/timeline checks; no desktop capture, clipboard, tray or input injection.</summary>
internal static class MotionVerification
{
    internal static async Task<bool> RunAsync(string output)
    {
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        var windows = new List<Window>();
        string? failure = null;
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            checks.Add(name);
        }
        try
        {
            var image = new Border { Width = 120, Height = 80, Background = Brushes.MediumAquamarine };
            var shift = new TranslateTransform(); image.RenderTransform = shift;
            var host = new Window { Title = "Snapline synthetic motion check", Width = 220, Height = 150, Content = image, ShowInTaskbar = false };
            windows.Add(host); host.Show();
            Motion.Reduced = false; Motion.OverrideSystem = true;
            Check(Motion.Enabled, "verification explicitly enables motion instead of silently skipping it");
            Motion.Enter(image, shift);
            var intermediate = false;
            for (var frame = 0; frame < 24; frame++)
            {
                await Task.Delay(12); Pulse(host);
                intermediate |= shift.Y > -10 && shift.Y < 0 && image.Opacity > 0 && image.Opacity < 1;
            }
            Check(intermediate, "finite fade/translation has intermediate state");
            Check(shift.Y == 0 && image.Opacity == 1, "entry settles on visible base values");
            var completionCount = 0;
            Motion.Exit(image, shift, () => completionCount++);
            await Task.Delay(30);
            Motion.Enter(image, shift);
            await Task.Delay(250);
            Check(shift.Y == 0 && image.Opacity == 1 && completionCount <= 1, "interrupted exit cannot leave content outside view");
            Motion.Reduced = true;
            Motion.Enter(image, shift, 100);
            Check(shift.Y == 0 && image.Opacity == 1, "reduced motion renders immediately without delay");
            Motion.Exit(image, shift, () => completionCount++);
            Check(completionCount > 0, "reduced-motion conceal callback runs immediately");
            host.Hide();
            var hoverFixture = new Grid { Width = 170, Height = 165 };
            var hoverPeg = PaperStyle.Peg(); hoverFixture.Children.Add(hoverPeg);
            var hoverCard = new HangingCard(hoverFixture, hoverPeg, rest: 0);
            host.Content = hoverFixture; host.Show(); Motion.Reduced = false;
            hoverCard.Hover(true);
            var hoverRotation = ((TransformGroup)hoverFixture.RenderTransform).Children.OfType<RotateTransform>().Single();
            var samples = new List<double>();
            for (var sample = 0; sample < 24; sample++) { await Task.Delay(25); Pulse(host); samples.Add(hoverRotation.Angle); }
            Check(samples.Max() >= 2,
                $"zero-rest-angle screenshot receives a perceptible pendulum impulse (peak {samples.Max():F2})");
            Check(!((TransformGroup)hoverFixture.RenderTransform).Children.OfType<ScaleTransform>().Any(), "card motion preserves physical size rather than zooming");
            Check(samples.Min() < -0.2, "pendulum naturally reverses instead of holding a static hover tilt");
            await Task.Delay(650);
            Check(hoverCard.Settled, "hover settles naturally even while pointer stays over card");
            hoverCard.Hover(false); await Task.Delay(50);
            Check(hoverCard.Settled, "hover leave preserves the resting pose");
            hoverCard.Hover(true); await Task.Delay(30); hoverCard.Hover(false); await Task.Delay(30); hoverCard.Hover(true);
            await Task.Delay(220); hoverCard.Settle();
            Check(hoverCard.Settled, "rapid hover interruption resets safely");
            Motion.Reduced = true; hoverCard.Hover(true);
            Check(hoverCard.Settled && !Motion.Enabled, "reduce motion wins over explicit animation override");
            var metalPeg = PaperStyle.Clip(Snapline.Core.HangingKind.Window);
            var metalCard = new HangingCard(new Grid(), metalPeg, 0, Snapline.Core.HangingKind.Window);
            metalCard.Hover(true); metalCard.Settle();
            Check(((SolidColorBrush)metalPeg.Background).Color == (Color)ColorConverter.ConvertFromString(Snapline.Core.HangingTheme.For(Snapline.Core.HangingKind.Window).Clip),
                "metal clip motion reset retains its material instead of turning wood-colored");
            Check(metalPeg.Width > PaperStyle.Clip(Snapline.Core.HangingKind.Image).Width, "window clip silhouette differs from narrow photo clothespin");
            host.Hide();
            var dragFixture = new Grid { Width = 170, Height = 165, Opacity = 1 };
            var dragPeg = PaperStyle.Peg(); dragPeg.Tag = "SnaplinePeg"; dragFixture.Children.Add(dragPeg);
            host.Content = dragFixture; host.Show();
            DragPreviewWindow dragged;
            using (var pickup = new DragVisualSession(dragFixture, ImageIO.TestImage()))
            {
                dragged = pickup.Preview;
                Check(dragFixture.Opacity == 0.3 && dragPeg.Visibility == Visibility.Hidden,
                    "picked-up photo leaves a faint unclipped placeholder");
                Check(dragged.IsVisible && dragged.DoesNotInterceptInput, "drag thumbnail is visible and native input-transparent");
                var screen = Forms.Screen.PrimaryScreen!;
                var scale = Native.Scale(screen);
                var probe = new System.Drawing.Point(screen.Bounds.Left + 80, screen.Bounds.Top + 80);
                dragged.PlaceAt(probe);
                var bounds = Native.Bounds(new System.Windows.Interop.WindowInteropHelper(dragged).Handle);
                Check(Math.Abs(bounds.Left - (probe.X + Math.Round(18 * scale))) <= 2,
                    "preview follows supplied cursor position without injecting pointer input");
                dragged.PlaceAt(new System.Drawing.Point(screen.Bounds.Right - 2, screen.Bounds.Bottom - 2));
                bounds = Native.Bounds(new System.Windows.Interop.WindowInteropHelper(dragged).Handle);
                Check(bounds.Right <= screen.Bounds.Right && bounds.Bottom <= screen.Bounds.Bottom, "drag preview flips at display edges");
                pickup.Preview.Feedback(DragDropEffects.Copy);
            }
            Check(!dragged.IsVisible && dragFixture.Opacity == 1 && dragFixture.IsHitTestVisible && dragPeg.Visibility == Visibility.Visible,
                "drop/cancel cleanup removes preview and restores source visual state");
            var cancelled = false;
            try
            {
                using var pickup = new DragVisualSession(dragFixture, ImageIO.TestImage());
                throw new InvalidOperationException("synthetic drag failure");
            }
            catch (InvalidOperationException) { cancelled = true; }
            Check(cancelled && dragFixture.Opacity == 1 && dragPeg.Visibility == Visibility.Visible,
                "exception path also restores dragged photo and peg");
            host.Hide();

            var data = Path.Combine(output, "isolated-data");
            var app = new SnaplineApplication(Application.Current, data, smoke: true, allowOwnPocketTests: true);
            app.Start();
            try
            {
                var shelf = Application.Current.Windows.OfType<ShelfWindow>().Single();
                await WindowPocketVerification.RunAsync(app, shelf, Check, Path.Combine(output, "hanging-types.png"));
                Motion.Reduced = false; Motion.OverrideSystem = true;
                shelf.Reveal(Forms.Screen.PrimaryScreen!);
                await Task.Delay(250);
                shelf.SaveTestRender(Path.Combine(output, "shelf.png"));
                foreach (var fraction in new[] { 0.15, 0.5, 0.85 })
                    Check(shelf.WindowDropTarget(shelf.RopeTargetPoint(fraction)), $"rope accepts window approach at {fraction:P0}, not only Logo");
                var ropeCenter = shelf.RopeTargetPoint(0.5);
                shelf.WindowDropTarget(ropeCenter); shelf.WindowMoveHint(true, true);
                Check(shelf.RopeFeedbackVisible, "window approach shows empty peg and release label immediately");
                await Task.Delay(300);
                shelf.SaveTestRender(Path.Combine(output, "rope-target.png"));
                var offRope = new System.Drawing.Point(ropeCenter.X, ropeCenter.Y + 100);
                Check(!shelf.WindowDropTarget(offRope), "window body below rope is outside reception strip");
                shelf.WindowMoveHint(true, false);
                Check(!shelf.RopeFeedbackVisible, "moving away clears ready peg without stashing");
                Motion.Reduced = true;
                shelf.WindowDropTarget(ropeCenter); shelf.WindowMoveHint(true, true);
                Check(shelf.RopeFeedbackVisible, "rope readiness remains discoverable even with reduced motion");
                shelf.WindowMoveHint(false, false); Motion.Reduced = false;
                var captureButton = Descendants(shelf).OfType<Button>().Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "截图");
                var feedback = PaperStyle.Feedback(captureButton);
                Check(feedback != null, "paper action button has loaded hover animation binding");
                captureButton.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseEnterEvent });
                await Task.Delay(230);
                Check(feedback!.Responding, "button mouse-enter event animates visual independently of hit target");
                captureButton.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseLeaveEvent });
                await Task.Delay(650);
                Check(feedback.AtRest, "button mouse-leave event restores resting geometry");
                feedback.Hover(true); await Task.Delay(220);
                feedback.Press(true); await Task.Delay(180);
                Check(feedback.VerticalScale < 0.96, "paper button compresses vertically on press");
                feedback.Press(false); await Task.Delay(650);
                Check(feedback.VerticalScale == 1, "paper button release rebounds to uncompressed size");
                Motion.Reduced = true; shelf.FinishMotion();
                Check(feedback.AtRest, "reduce-motion reset clears an active button hover");
                Motion.Reduced = false;
                var entryPath = Path.Combine(data, "Inbox", "new-image.png");
                ImageIO.SavePng(ImageIO.TestImage(), entryPath);
                shelf.Refresh(new[] { Path.Combine(data, "Inbox", "smoke.png"), entryPath });
                await Task.Delay(1200);
                var animatedCards = Descendants(shelf).OfType<FrameworkElement>().Where(e => Equals(e.Tag, "SnaplineCard")).ToArray();
                Check(animatedCards.Length == 2 && shelf.HangingSettled,
                    "new screenshot entry settles with both cards fully visible");
                Check(Descendants(shelf).OfType<FrameworkElement>().Any(e => Equals(e.Tag, "SnaplineRope")), "approved rope remains in expanded shelf");
                Check(Descendants(shelf).OfType<FrameworkElement>().Count(e => Equals(e.Tag, "SnaplinePeg")) == 2, "both screenshots keep clothespins");
                shelf.SaveTestRender(Path.Combine(output, "hanging-cards.png"));
                Check(TransparentFraction(Path.Combine(output, "hanging-cards.png")) > 0.65, "rope/cards/buttons retain mostly transparent shelf pixels");
                shelf.Copied(entryPath); await Task.Delay(700);
                Check(shelf.HangingSettled, "copy response settles without moving screenshot off its peg");
                shelf.FinishMotion();
                Check(shelf.HangingSettled, "interrupting hanging motion restores stable card positions");
                for (var cycle = 0; cycle < 8; cycle++)
                {
                    shelf.Conceal(); await Task.Delay(25);
                    shelf.Reveal(Forms.Screen.PrimaryScreen!);
                    await Task.Delay(25);
                }
                await Task.Delay(300);
                Check(shelf.IsVisible && shelf.IsRevealed, "rapid actual shelf reveal cancels stale conceal callbacks");
                shelf.Conceal(); await Task.Delay(300);
                Check(!shelf.IsVisible, "actual shelf hides after short exit transition");
                shelf.Reveal(Forms.Screen.PrimaryScreen!); await Task.Delay(250);
                Check(shelf.IsVisible && shelf.IsRevealed, "actual shelf can be recalled after conceal");
                shelf.PrepareForMove();
                Check(shelf.IsVisible, "component move clears motion without hiding shelf");
                var remove = Descendants(shelf).OfType<Button>().First(b => System.Windows.Automation.AutomationProperties.GetAutomationId(b) == "RemoveCard");
                remove.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                await Task.Delay(260);
                Check(shelf.IsVisible && File.Exists(entryPath), "animated remove keeps shelf accessible and source image untouched");
                Motion.Reduced = true; shelf.FinishMotion(); shelf.Conceal();
                Check(!shelf.IsVisible, "reduced-motion actual shelf conceals immediately");
                await GroupVerification.RunAsync(app, shelf, data, output, Check);
            }
            finally { app.Dispose(); Directory.Delete(data, true); }

            var handle = new HandleWindow(() => { }, () => { }, () => { });
            windows.Add(handle);
            Motion.Reduced = false;
            handle.Reveal(Forms.Screen.PrimaryScreen!, 3);
            await Task.Delay(250);
            Check(handle.IsVisible, "new identity handle reveals normally");
            handle.FinishMotion();
            Check(Brand.Icon(16).Source != null, "shared logo resource loads at tray-sized dimensions");
            Check(Descendants(handle).OfType<TextBlock>().Any(t => t.Text == "3 张"), "collapsed photo entry shows a separate count tab");
            handle.SaveTestRender(Path.Combine(output, "handle.png"));
            var handleButton = Descendants(handle).OfType<Button>().Single();
            handleButton.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseEnterEvent });
            var handleResponded = false; var separatePhases = false;
            for (var frame = 0; frame < 18; frame++)
            {
                await Task.Delay(25); Pulse(handle);
                handleResponded |= handle.HoverFeedback.Responding;
                separatePhases |= handle.HoverFeedback.Angle < -0.2 && handle.CountFeedback.Angle > 0.2;
            }
            Check(handleResponded, "collapsed photo entrance receives a short spring impulse");
            Check(separatePhases, "photo and count tag respond independently with different phases");
            handle.SaveTestRender(Path.Combine(output, "handle-hover.png"));
            handleButton.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseLeaveEvent });
            await Task.Delay(1250);
            Check(handle.HoverFeedback.AtRest && handle.CountFeedback.AtRest, "photo and tag both settle without continuous looping");
            handle.HoverFeedback.Hover(true); handle.CountFeedback.Hover(true); await Task.Delay(40);
            handle.HoverFeedback.Suspend(true); handle.CountFeedback.Suspend(true);
            handle.HoverFeedback.Press(false);
            Check(handle.HoverFeedback.AtRest && handle.CountFeedback.AtRest, "drag suspension cancels motion and suppresses release bounce");
            handle.HoverFeedback.Suspend(false); handle.CountFeedback.Suspend(false);
            await EditorVerification.RunAsync(output, Check);
            Motion.OverrideSystem = false;
            Check(Motion.Enabled == SystemParameters.ClientAreaAnimation, "default behavior still respects Windows motion setting");
        }
        catch (Exception exception) { failure = exception.GetType().Name + ": " + exception.Message; }
        finally
        {
            foreach (var window in windows) window.Close();
            Motion.Reduced = false; Motion.OverrideSystem = false;
            File.WriteAllText(Path.Combine(output, "motion-report.json"), JsonSerializer.Serialize(new
            { passed = checks.Count, checks, failure, spatialAnimationsExplicitlyTested = true, systemAnimationsEnabled = SystemParameters.ClientAreaAnimation, windowPocketTests = "synthetic fixtures with own-process-only WinEvent hooks; no external app observation", actualDesktopCaptured = false, clipboardAccessed = false, inputInjected = false }, new JsonSerializerOptions { WriteIndented = true }));
        }
        return failure == null;
    }

    private static void Pulse(Window window)
    {
        window.UpdateLayout();
        var renderer = new System.Windows.Media.Imaging.RenderTargetBitmap(220, 180, 96, 96, PixelFormats.Pbgra32);
        renderer.Render(window);
    }

    private static double TransparentFraction(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(stream, System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
            System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
        var frame = new System.Windows.Media.Imaging.FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
        var bytes = new byte[frame.PixelWidth * frame.PixelHeight * 4]; frame.CopyPixels(bytes, frame.PixelWidth * 4, 0);
        var count = 0; for (var i = 3; i < bytes.Length; i += 4) if (bytes[i] == 0) count++;
        return (double)count / (frame.PixelWidth * frame.PixelHeight);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
