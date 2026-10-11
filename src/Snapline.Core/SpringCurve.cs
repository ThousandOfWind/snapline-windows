namespace Snapline.Core;

/// <summary>Finite samples of an underdamped spring. Units are value/second; no UI timers or global state.</summary>
public static class SpringCurve
{
    public static (double Position, double Velocity) Sample(double from, double velocity, double target,
        double seconds, double frequency, double damping)
    {
        if (!double.IsFinite(from) || !double.IsFinite(velocity) || !double.IsFinite(target)
            || !double.IsFinite(seconds) || !double.IsFinite(frequency) || !double.IsFinite(damping))
            throw new ArgumentOutOfRangeException(nameof(from), "Spring inputs must be finite.");
        if (frequency <= 0 || damping <= 0 || damping >= 1)
            throw new ArgumentOutOfRangeException(nameof(frequency), "Use positive frequency and damping strictly between zero and one.");
        var t = Math.Max(0, seconds);
        var decay = frequency * damping;
        var oscillation = frequency * Math.Sqrt(1 - damping * damping);
        var a = from - target;
        var b = (velocity + decay * a) / oscillation;
        var cos = Math.Cos(oscillation * t);
        var sin = Math.Sin(oscillation * t);
        var envelope = Math.Exp(-decay * t);
        var displacement = a * cos + b * sin;
        return (target + envelope * displacement,
            envelope * (-decay * displacement - a * oscillation * sin + b * oscillation * cos));
    }
}
