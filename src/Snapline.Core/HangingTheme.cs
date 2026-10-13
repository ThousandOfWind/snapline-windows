namespace Snapline.Core;

/// <summary>Semantic materials, not just different captions; future hanging types get one theme entry.</summary>
public sealed record HangingTheme(string Label, string Paper, string Edge, string Header, string Ink,
    string Clip, string Highlight, string ClipShape)
{
    public static HangingTheme For(HangingKind kind) => kind switch
    {
        HangingKind.Image => new("图片", "#FFFDF7", "#DDDECC", "#EFEFDF", "#657260", "#EAAF75", "#F5C18D", "wood"),
        HangingKind.ImageGroup => new("截图组", "#FCF7FF", "#B8A4CC", "#EEE2F6", "#6A5282", "#AD9BC0", "#D9C9E8", "band"),
        HangingKind.Window => new("软件窗口", "#F1F6FA", "#89A5B9", "#D8E4EF", "#43657C", "#91A7B7", "#C7DCEB", "metal"),
        HangingKind.FolderWindow => new("文件夹窗口", "#FFF9E9", "#C8AC63", "#F5E7B8", "#80673D", "#C8A464", "#E9D09A", "brass"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
