using Snapline.Core;

internal static class GroupCoreChecks
{
    internal static void Run(string directory, Action<bool, string> check)
    {
        var paths = Enumerable.Range(1, 5).Select(n => Path.Combine(directory, $"group-{n}.png")).ToArray();
        foreach (var path in paths) File.WriteAllText(path, "synthetic fixture");
        var groups = new ImageGroups(null, paths);
        check(groups.Items.Count == 0, "old settings restore with no image groups");
        check(!groups.CanMerge(paths[0], [paths[0]], paths), "self drop cannot create singleton group");
        var first = groups.Merge(paths[0], [paths[1]], paths)!.Value;
        check(groups.Get(first)!.Members.SequenceEqual(paths.Take(2)), "image-on-image creates ordered two-member group");
        check(groups.For(paths[1].ToUpperInvariant())!.Id == first, "group ownership is case-insensitive");
        check(!groups.CanMerge(paths[0], [paths[1]], paths), "drop already-owned member into same group is a no-op");
        check(groups.Merge(paths[0], [paths[2]], paths) == first && groups.Get(first)!.Members.Count == 3, "drop on group appends while preserving identity");
        var second = groups.Merge(paths[3], [paths[4]], paths)!.Value;
        check(groups.Items.Count == 2, "independent screenshot groups coexist");
        check(groups.Merge(paths[0], groups.Get(second)!.Members, paths) == first && groups.Items.Count == 1
            && groups.Get(first)!.Members.Count == 5, "group-on-group merges all members without nesting");
        check(paths.All(File.Exists), "grouping never moves or deletes source images");
        var view = groups.Get(first)!; view.Members.Clear();
        check(groups.Get(first)!.Members.Count == 5, "returned group snapshot cannot mutate model");
        check(!groups.CanMerge(paths[0], [Path.Combine(directory, "foreign.png")], paths), "foreign image path not in library is rejected");
        check(groups.Merge(paths[0], [paths[1], Path.Combine(directory, "non-image.txt")], paths) == null
            && groups.Get(first)!.Members.Count == 5, "invalid mixed payload is rejected atomically");
        check(groups.TakeOut(first, paths[4]) && groups.For(paths[4]) == null && groups.Get(first)!.Members.Count == 4, "member can leave group without leaving library");
        groups.Prune(paths.Take(2));
        check(groups.Get(first)!.Members.Count == 2, "library eviction prunes corresponding group references");
        groups.Prune(paths.Take(1));
        check(groups.Items.Count == 0 && File.Exists(paths[0]), "one remaining member dissolves group safely");
        first = groups.Merge(paths[0], [paths[1]], paths)!.Value;
        check(groups.Ungroup(first) && paths.All(File.Exists), "ungroup keeps every original file");
        first = groups.Merge(paths[0], [paths[1]], paths)!.Value;
        var store = new SettingsStore(Path.Combine(directory, "group-state"));
        store.Save(new Preferences { Items = paths.ToList(), Groups = groups.Items.ToList() });
        var restored = new ImageGroups(store.Load().Groups, paths);
        check(restored.Get(first)!.Members.SequenceEqual(paths.Take(2)), "group GUID and members survive settings round trip");
        var malformed = new ImageGroups(new[]
        {
            new ScreenshotGroup(Guid.Empty, "bad", paths.ToList()),
            new ScreenshotGroup(first, "valid", [paths[0], paths[1], paths[1]]),
            new ScreenshotGroup(Guid.NewGuid(), "overlap", [paths[1], paths[2], paths[3]]),
            new ScreenshotGroup(Guid.NewGuid(), "missing", [paths[4], Path.Combine(directory, "deleted.png")])
        }, paths);
        check(malformed.Items.Count == 2 && malformed.Items.Sum(g => g.Members.Count) == 4, "restore repairs duplicates, missing paths and cross-group ownership");
        groups.Clear(); check(groups.Items.Count == 0 && paths.All(File.Exists), "clear group metadata preserves files");
    }
}
