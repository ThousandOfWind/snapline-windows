using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Snapline.Windows;

/// <summary>Small, finite transitions. Final values never depend on a completion callback.</summary>
internal static class Motion
{
    internal static bool Reduced { get; set; }
    internal static bool OverrideSystem { get; set; }
    internal static bool Enabled => !Reduced && (OverrideSystem || SystemParameters.ClientAreaAnimation);

    internal static void Reset(UIElement target, TranslateTransform transform)
    {
        target.BeginAnimation(UIElement.OpacityProperty, null); target.Opacity = 1;
        transform.BeginAnimation(TranslateTransform.YProperty, null); transform.Y = 0;
    }

    internal static void Enter(UIElement target, TranslateTransform transform, int delayMs = 0)
    {
        Reset(target, transform);
        if (!Enabled) return;
        var end = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delayMs + 180));
        var start = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delayMs));
        var shift = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
        shift.KeyFrames.Add(new DiscreteDoubleKeyFrame(-10, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        shift.KeyFrames.Add(new DiscreteDoubleKeyFrame(-10, start));
        shift.KeyFrames.Add(new EasingDoubleKeyFrame(0, end) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        var fade = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
        fade.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        fade.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, start));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, end));
        transform.BeginAnimation(TranslateTransform.YProperty, shift);
        target.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    internal static void Exit(UIElement target, TranslateTransform transform, Action complete)
    {
        Reset(target, transform);
        if (!Enabled) { complete(); return; }
        var duration = TimeSpan.FromMilliseconds(130);
        transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -6, duration)
        { FillBehavior = FillBehavior.Stop, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } });
        var fade = new DoubleAnimation(1, 0, duration) { FillBehavior = FillBehavior.Stop };
        fade.Completed += (_, _) => complete();
        target.BeginAnimation(UIElement.OpacityProperty, fade);
    }
}
