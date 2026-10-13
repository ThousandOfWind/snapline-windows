using System.Text.Json;

namespace Snapline.Core;

public sealed class Preferences
{
    public bool WatchClipboard { get; set; }
    public bool RevealAtTop { get; set; } = true;
    public bool PeekOnNew { get; set; } = true;
    public bool ReduceMotion { get; set; }
    public bool OverrideSystemAnimations { get; set; }
    public bool StashWindows { get; set; } = true;
    public int Capacity { get; set; } = 50;
    public bool WatchFoldersInitialized { get; set; }
    public List<string> WatchFolders { get; set; } = [];
    public List<string> Items { get; set; } = [];
    public WindowPosition? ShelfPosition { get; set; }
    public WindowPosition? HandlePosition { get; set; }
}

public static class ImageFiles
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tif", ".tiff" };

    public static bool IsSupported(string path) => Extensions.Contains(Path.GetExtension(path));

    public static bool IsInside(string path, string folder)
    {
        var parent = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(parent, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>A bounded list of file references. Removing a card never deletes its source file.</summary>
public sealed class ImageLibrary
{
    private readonly List<string> items = [];
    public IReadOnlyList<string> Items => items.AsReadOnly();
    public int Capacity { get; }

    public ImageLibrary(int capacity, IEnumerable<string>? restored = null)
    {
        Capacity = Math.Clamp(capacity, 1, 100);
        foreach (var path in restored ?? [])
            if (File.Exists(path)) Add(path);
    }

    public bool Add(string path)
    {
        var full = Path.GetFullPath(path);
        if (!ImageFiles.IsSupported(full) || items.Contains(full, StringComparer.OrdinalIgnoreCase)) return false;
        items.Add(full);
        while (items.Count > Capacity) items.RemoveAt(0);
        return true;
    }

    public bool Remove(string path) => items.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)) > 0;
    public void Clear() => items.Clear();
    public void Prune() => items.RemoveAll(p => !File.Exists(p));
}

public sealed class SettingsStore(string directory)
{
    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    public string InboxPath => Path.Combine(DirectoryPath, "Inbox");
    public string SettingsPath => Path.Combine(DirectoryPath, "settings.json");

    public Preferences Load()
    {
        Directory.CreateDirectory(InboxPath);
        if (!File.Exists(SettingsPath)) return new Preferences();
        try
        {
            var value = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(SettingsPath)) ?? new Preferences();
            value.Capacity = Math.Clamp(value.Capacity, 1, 100);
            value.Items ??= [];
            value.WatchFolders ??= [];
            return value;
        }
        catch (JsonException) { return new Preferences(); }
    }

    public void Save(Preferences settings)
    {
        Directory.CreateDirectory(DirectoryPath);
        var temp = SettingsPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, SettingsPath, overwrite: true);
    }
}
