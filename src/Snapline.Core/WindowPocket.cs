namespace Snapline.Core;

/// <summary>Session-only identity: never serialize HWNDs or reopen an application from a path.</summary>
public readonly record struct WindowIdentity(long Handle, uint ProcessId, uint ThreadId, long ProcessStarted);

public sealed class WindowPocket(int capacity = 20)
{
    private readonly HashSet<WindowIdentity> items = [];
    public int Capacity { get; } = Math.Clamp(capacity, 1, 50);
    public int Count => items.Count;
    public bool Contains(WindowIdentity identity) => items.Contains(identity);
    public bool CanAdd(WindowIdentity identity) => Valid(identity) && !Contains(identity) && Count < Capacity;
    public bool Add(WindowIdentity identity) => CanAdd(identity) && items.Add(identity);
    public bool Remove(WindowIdentity identity) => items.Remove(identity);
    private static bool Valid(WindowIdentity identity) => identity.Handle != 0 && identity.ProcessId != 0
        && identity.ThreadId != 0 && identity.ProcessStarted > 0;
}

public static class WindowDropPolicy
{
    // Only a completed, deliberate title-bar move over the rope/entry is a drop. Hover never commits.
    public static bool Accept(bool enabled, bool eligible, bool cancelled, bool moved, bool onTarget,
        bool titleBarAtTarget) => enabled && eligible && !cancelled && moved && onTarget && titleBarAtTarget;
}
