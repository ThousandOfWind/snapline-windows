using System;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace Snapline.Windows;

/// <summary>Standard FileDrop for other apps; bounded session-tagged selection for explicit in-app grouping.</summary>
internal static class ImageDragData
{
    internal const string Format = "Snapline.ImageSelection.v1";
    private static readonly string Session = Guid.NewGuid().ToString("N");
    private sealed record Selection(string Session, string[] Paths);

    internal static DataObject Create(string[] paths)
    {
        var data = new DataObject(DataFormats.FileDrop, paths);
        data.SetData(Format, JsonSerializer.Serialize(new Selection(Session, paths)), autoConvert: false);
        return data;
    }

    internal static string[]? SelectionFrom(IDataObject data)
    {
        try
        {
            if (!data.GetDataPresent(Format, autoConvert: false) || data.GetData(Format, autoConvert: false) is not string text || text.Length > 32768) return null;
            var selection = JsonSerializer.Deserialize<Selection>(text);
            if (selection?.Session != Session || selection.Paths == null || selection.Paths.Length is < 1 or > 100
                || selection.Paths.Any(string.IsNullOrWhiteSpace)) return null;
            return selection.Paths;
        }
        catch (Exception e) when (e is JsonException or System.Runtime.InteropServices.ExternalException or ArgumentException) { return null; }
    }
}
