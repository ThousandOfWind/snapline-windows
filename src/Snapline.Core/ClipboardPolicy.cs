namespace Snapline.Core;

/// <summary>Inspect format names only; never inspect text, files or password payloads.</summary>
public static class ClipboardPolicy
{
    private static readonly HashSet<string> Rejected = new(StringComparer.OrdinalIgnoreCase)
    {
        "Text", "UnicodeText", "OEMText", "HTML Format", "Rich Text Format",
        "FileDrop", "FileName", "FileNameW", "FileGroupDescriptor", "FileGroupDescriptorW",
        "ExcludeClipboardContentFromMonitorProcessing", "Clipboard Viewer Ignore",
        "CanIncludeInClipboardHistory", "CanUploadToCloudClipboard",
        "application/x-keepassxc", "application/x-kde-passwordManagerHint"
    };

    public static bool AllowsImage(IEnumerable<string> formats) => !formats.Any(Rejected.Contains);
}
