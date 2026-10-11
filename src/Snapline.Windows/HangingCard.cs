using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Snapline.Windows;

/// <summary>Physical, damped gestures about the peg; stable base values preserve interaction.</summary>
internal sealed class HangingCard
{
    private readonly FrameworkElement card;
    private readonly Border peg;
    private readonly TranslateTransform drop = new();
    private readonly RotateTransform swing;
    private readonly SpringMotion pendulum;
    private readonly SolidColorBrush pegTint = PaperStyle.Brush("#EAAF75");
    private bool hovering;
    private bool dragging;
    private readonly double rest;

    internal HangingCard(FrameworkElement card, Border peg, double rest)
    {
        this.card = card; this.peg = peg; this.rest = rest;
        swing = new RotateTransform(rest);
        var group = new TransformGroup(); group.Children.Add(swing); group.Children.Add(drop);
        card.RenderTransform = group; card.RenderTransformOrigin = new Point(0.5, 0);
        pendulum = new SpringMotion(swing, RotateTransform.AngleProperty, rest);
        peg.Background = pegTint;
    }

    internal void Settle()
    {
        Motion.Reset(card, drop);
        pendulum.Reset(); hovering = false;
        pegTint.BeginAnimation(SolidColorBrush.ColorProperty, null); pegTint.Color = (Color)ColorConverter.ConvertFromString("#EAAF75");
    }

    internal void Hang(int delayMs = 0)
    {
        Settle(); Motion.Enter(card, drop, delayMs);
        if (!Motion.Enabled) return;
        pendulum.To(rest, impulse: -65, frequency: 15, damping: 0.34, duration: 1.0);
    }

    internal void Hover(bool on)
    {
        if (dragging) return;
        if (!Motion.Enabled) { Settle(); return; }
        var entering = on && !hovering;
        hovering = on;
        // Touch supplies momentum; gravity brings it back. No held tilt or enlargement.
        if (entering) pendulum.To(rest, impulse: rest < 0 ? -72 : 72, frequency: 16, damping: 0.28, duration: 1.1);
        Tint(on ? "#F5C18D" : "#EAAF75");
    }

    internal void SetDragging(bool on)
    {
        Settle(); dragging = on;
    }

    internal void Copied()
    {
        if (!Motion.Enabled) return;
        pegTint.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation((Color)ColorConverter.ConvertFromString("#FFE1AA"), pegTint.Color,
            TimeSpan.FromMilliseconds(380)) { FillBehavior = FillBehavior.Stop });
        pendulum.To(rest, impulse: -32, frequency: 22, damping: 0.46, duration: 0.6);
    }

    internal void Release(Action complete)
    {
        Settle();
        if (!Motion.Enabled) { complete(); return; }
        var duration = TimeSpan.FromMilliseconds(160);
        drop.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, 20, duration) { FillBehavior = FillBehavior.Stop });
        var fade = new DoubleAnimation(1, 0, duration) { FillBehavior = FillBehavior.Stop };
        fade.Completed += (_, _) => complete(); card.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    private void Tint(string hex)
    {
        var from = pegTint.Color;
        pegTint.BeginAnimation(SolidColorBrush.ColorProperty, null); pegTint.Color = (Color)ColorConverter.ConvertFromString(hex);
        pegTint.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(from, pegTint.Color, TimeSpan.FromMilliseconds(180))
        { FillBehavior = FillBehavior.Stop });
    }

    internal double Angle => swing.Angle;
    internal bool Settled => card.Opacity == 1 && drop.Y == 0 && pendulum.AtRest;
}
