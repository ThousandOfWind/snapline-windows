using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Snapline.Windows;

/// <summary>Different material responses: paper buttons compress; photo pendants swing then rest.</summary>
internal sealed class HoverMotion
{
    private readonly TranslateTransform lift = new();
    private readonly ScaleTransform scale = new(1, 1);
    private readonly RotateTransform turn = new();
    private readonly SpringMotion liftSpring;
    private readonly SpringMotion scaleX;
    private readonly SpringMotion scaleY;
    private readonly SpringMotion turnSpring;
    private readonly bool pendant;
    private readonly bool secondary;
    private bool hovering;
    private bool pressing;
    private bool suspended;

    internal HoverMotion(FrameworkElement hitTarget, FrameworkElement visual, bool pendant = false, bool secondary = false)
    {
        this.pendant = pendant; this.secondary = secondary;
        var transforms = new TransformGroup(); transforms.Children.Add(scale); transforms.Children.Add(turn); transforms.Children.Add(lift);
        visual.RenderTransform = transforms; visual.RenderTransformOrigin = pendant ? new Point(0.5, 0) : new Point(0.5, 0.5);
        liftSpring = new SpringMotion(lift, TranslateTransform.YProperty, 0);
        scaleX = new SpringMotion(scale, ScaleTransform.ScaleXProperty, 1);
        scaleY = new SpringMotion(scale, ScaleTransform.ScaleYProperty, 1);
        turnSpring = new SpringMotion(turn, RotateTransform.AngleProperty, 0);
        hitTarget.MouseEnter += (_, _) => Hover(true);
        hitTarget.MouseLeave += (_, _) => { pressing = false; hovering = false; if (!suspended) Update(); };
        hitTarget.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler((_, _) => Press(true)), true);
        hitTarget.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler((_, _) => Press(false)), true);
        hitTarget.LostMouseCapture += (_, _) => { if (pressing) Press(false); };
    }

    internal void Hover(bool on)
    {
        var entering = on && !hovering;
        hovering = on;
        if (!suspended) Update(entering);
    }

    internal void Press(bool on) { pressing = on; if (!suspended) Update(); }
    internal void Suspend(bool on) { suspended = on; Reset(); }
    internal void Reset()
    {
        hovering = false; pressing = false;
        liftSpring.Reset(); scaleX.Reset(); scaleY.Reset(); turnSpring.Reset();
    }

    private void Update(bool entering = false)
    {
        if (!Motion.Enabled) { Reset(); return; }
        if (pendant)
        {
            // A nudge, not a permanent tilt. No scaling the rope/peg together with the photo.
            if (entering) turnSpring.To(0, secondary ? 28 : -65, frequency: secondary ? 11 : 16,
                damping: secondary ? 0.4 : 0.28, duration: 1.1);
            return;
        }
        var compressed = pressing;
        liftSpring.To(compressed ? 0.8 : hovering ? -1.7 : 0, frequency: 24, damping: compressed ? 0.8 : 0.48, duration: 0.55);
        scaleX.To(compressed ? 1.025 : 1, frequency: 25, damping: 0.58, duration: 0.55);
        scaleY.To(compressed ? 0.94 : 1, frequency: 25, damping: 0.58, duration: 0.55);
    }

    internal bool AtRest => liftSpring.AtRest && scaleX.AtRest && scaleY.AtRest && turnSpring.AtRest;
    internal bool Responding => Math.Abs(lift.Y) > 0.8 || Math.Abs(scale.ScaleY - 1) > 0.02 || Math.Abs(turn.Angle) > 0.6;
    internal double Angle => turn.Angle;
    internal double VerticalScale => scale.ScaleY;
}
