namespace Snapline.Core;

public sealed record ScreenshotGroup(Guid Id, string Name, List<string> Members);

/// <summary>Groups reference the existing image library. Every member has one owner; no file is moved/deleted.</summary>
public sealed class ImageGroups
{
    private readonly List<ScreenshotGroup> groups = [];
    public IReadOnlyList<ScreenshotGroup> Items => groups.Select(Clone).ToArray();
    public ImageGroups(IEnumerable<ScreenshotGroup>? restored, IEnumerable<string> available)
    {
        var known = Known(available); var owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in restored ?? [])
        {
            if (group == null || group.Id == Guid.Empty || groups.Any(g => g.Id == group.Id)) continue;
            var members = Normalize(group.Members ?? []).Where(p => known.Contains(p) && !owned.Contains(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (members.Count < 2) continue;
            groups.Add(new ScreenshotGroup(group.Id, string.IsNullOrWhiteSpace(group.Name) ? "截图组" : group.Name[..Math.Min(group.Name.Length, 60)], members));
            owned.UnionWith(members);
        }
    }

    public ScreenshotGroup? For(string path) => groups.FirstOrDefault(g => g.Members.Contains(Path.GetFullPath(path), StringComparer.OrdinalIgnoreCase)) is { } g ? Clone(g) : null;
    public ScreenshotGroup? Get(Guid id) => groups.FirstOrDefault(g => g.Id == id) is { } g ? Clone(g) : null;

    public bool CanMerge(string target, IEnumerable<string> source, IEnumerable<string> available)
    {
        var known = Known(available); var full = Path.GetFullPath(target);
        var raw = source.ToArray(); var normalized = Normalize(raw).ToArray();
        if (normalized.Length != raw.Length) return false;
        var paths = normalized.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (!known.Contains(full) || paths.Length == 0 || paths.Any(p => !known.Contains(p))) return false;
        var destination = For(full)?.Members ?? [full];
        return paths.Any(p => !destination.Contains(p, StringComparer.OrdinalIgnoreCase));
    }

    public Guid? Merge(string target, IEnumerable<string> source, IEnumerable<string> available)
    {
        var raw = source.ToArray();
        if (!CanMerge(target, raw, available)) return null;
        var paths = Normalize(raw).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var full = Path.GetFullPath(target);
        var destination = groups.FirstOrDefault(g => g.Members.Contains(full, StringComparer.OrdinalIgnoreCase));
        var joined = (destination?.Members ?? [full]).Concat(paths).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var identity = destination?.Id ?? Guid.NewGuid();
        foreach (var group in groups.ToArray())
        {
            if (group.Id == identity) continue;
            group.Members.RemoveAll(p => joined.Contains(p, StringComparer.OrdinalIgnoreCase));
            if (group.Members.Count < 2) groups.Remove(group);
        }
        if (destination != null) groups.Remove(destination);
        groups.Add(new ScreenshotGroup(identity, destination?.Name ?? "截图组", joined));
        return identity;
    }

    public bool TakeOut(Guid id, string path)
    {
        var group = groups.FirstOrDefault(g => g.Id == id);
        if (group == null) return false;
        var removed = group.Members.RemoveAll(p => string.Equals(p, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)) > 0;
        if (group.Members.Count < 2) groups.Remove(group);
        return removed;
    }
    public bool Ungroup(Guid id) => groups.RemoveAll(g => g.Id == id) > 0;
    public void Clear() => groups.Clear();
    public void Prune(IEnumerable<string> available)
    {
        var known = Known(available);
        foreach (var group in groups.ToArray())
        {
            group.Members.RemoveAll(p => !known.Contains(p));
            if (group.Members.Count < 2) groups.Remove(group);
        }
    }
    private static ScreenshotGroup Clone(ScreenshotGroup group) => group with { Members = group.Members.ToList() };
    private static HashSet<string> Known(IEnumerable<string> paths) => new(Normalize(paths), StringComparer.OrdinalIgnoreCase);
    private static IEnumerable<string> Normalize(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            string? full = null;
            try { if (!string.IsNullOrWhiteSpace(path) && ImageFiles.IsSupported(path)) full = Path.GetFullPath(path); }
            catch (Exception e) when (e is ArgumentException or NotSupportedException) { }
            if (full != null) yield return full;
        }
    }
}
