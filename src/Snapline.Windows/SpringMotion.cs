using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Animation;
using Snapline.Core;

namespace Snapline.Windows;

/// <summary>A sampled finite WPF spring, retargeted with current velocity instead of restarting a canned easing.</summary>
internal sealed class SpringMotion
{
    private readonly Animatable target;
    private readonly DependencyProperty property;
    private readonly double rest;
    private double from;
    private double velocity;
    private double destination;
    private double frequency;
    private double damping;
    private double duration;
    private long started;
    private bool active;

    internal SpringMotion(Animatable target, DependencyProperty property, double rest)
    {
        this.target = target; this.property = property; this.rest = rest;
        target.SetValue(property, rest);
    }

    internal void To(double value, double impulse = 0, double frequency = 20, double damping = 0.58, double duration = 0.7)
    {
        if (!Motion.Enabled) { Reset(); return; }
        var current = Current();
        this.from = current.Position; this.velocity = current.Velocity + impulse;
        this.destination = value; this.frequency = frequency; this.damping = damping; this.duration = duration;
        started = Stopwatch.GetTimestamp(); active = true;
        target.BeginAnimation(property, null); target.SetValue(property, value);
        var animation = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
        var samples = (int)Math.Ceiling(duration * 60);
        for (var index = 0; index <= samples; index++)
        {
            var time = duration * index / samples;
            var sample = SpringCurve.Sample(from, velocity, value, time, frequency, damping);
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(index == samples ? value : sample.Position,
                KeyTime.FromTimeSpan(TimeSpan.FromSeconds(time))));
        }
        target.BeginAnimation(property, animation);
    }

    private (double Position, double Velocity) Current()
    {
        var elapsed = active ? Stopwatch.GetElapsedTime(started).TotalSeconds : duration;
        if (!active || elapsed >= duration) return ((double)target.GetValue(property), 0);
        return SpringCurve.Sample(from, velocity, destination, elapsed, frequency, damping);
    }

    internal void Reset()
    {
        active = false; target.BeginAnimation(property, null); target.SetValue(property, rest);
    }

    internal bool AtRest => Math.Abs((double)target.GetValue(property) - rest) < 0.001;
}
