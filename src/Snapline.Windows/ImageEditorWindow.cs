using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snapline.Core;

namespace Snapline.Windows;

internal enum EditorTool { Pen, Highlight, Arrow, Rectangle, Ellipse, Text, Crop, Mosaic, Obscure }

internal sealed class ImageEditorWindow : Window
{
    private readonly string sourcePath;
    private readonly BitmapSource original;
    private BitmapSource rendered;
    private readonly Func<string> destination;
    private readonly Action<BitmapSource> copy;
    private readonly bool confirmDiscard;
    private readonly ImageEditorCanvas surface = new() { ClipToBounds = true };
    private readonly ScrollViewer viewport = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBlock status = new();
    private readonly TextBox text = new() { Text = "标注文字", Width = 160, MaxLength = 500, Margin = new Thickness(5), VerticalContentAlignment = VerticalAlignment.Center };
    private readonly ComboBox colors = new() { Width = 82, Margin = new Thickness(5) };
    private readonly ComboBox fontSize = new() { Width = 64, Margin = new Thickness(5) };
    private readonly Slider thickness = new() { Minimum = 1, Maximum = 36, Value = 4, Width = 90, Margin = new Thickness(5) };
    private readonly Slider zoom = new() { Minimum = 0.1, Maximum = 3, Value = 1, Width = 110, Margin = new Thickness(5) };
    private readonly Dictionary<EditorTool, Button> tools = [];
    private readonly List<ImagePoint> points = [];
    private readonly Button undo;
    private readonly Button redo;
    private EditorTool tool;
    private bool drawing;
    internal ImageEditorDocument Document { get; }
    internal string? ResultPath { get; private set; }
    internal BitmapSource Rendered => rendered;

    internal ImageEditorWindow(string path, Func<string> destination, Action<BitmapSource> copy, bool confirmDiscard = true)
    {
        sourcePath = Path.GetFullPath(path); this.destination = destination; this.copy = copy; this.confirmDiscard = confirmDiscard;
        original = ImageIO.Load(sourcePath, 0); Document = new ImageEditorDocument(original.PixelWidth, original.PixelHeight);
        rendered = AnnotationRenderer.Render(original, Document.State);
        Title = Path.GetFileName(path) + " — 编辑 / 标注 · Snapline";
        Width = Math.Min(1140, SystemParameters.WorkArea.Width); Height = Math.Min(780, SystemParameters.WorkArea.Height);
        MinWidth = Math.Min(780, SystemParameters.WorkArea.Width); MinHeight = Math.Min(500, SystemParameters.WorkArea.Height);
        WindowStartupLocation = WindowStartupLocation.CenterScreen; Background = PaperStyle.Brush("#F3F6EF");
        var layout = new DockPanel { Margin = new Thickness(12) };
        var header = new StackPanel(); DockPanel.SetDock(header, Dock.Top); layout.Children.Add(header);
        var toolbar = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        foreach (var (mode, label) in new[] { (EditorTool.Pen,"画笔"),(EditorTool.Highlight,"高亮"),(EditorTool.Arrow,"箭头"),
            (EditorTool.Rectangle,"矩形框"),(EditorTool.Ellipse,"圆框"),(EditorTool.Text,"文字"),(EditorTool.Crop,"裁剪"),
            (EditorTool.Mosaic,"马赛克"),(EditorTool.Obscure,"遮挡") })
        {
            var select = MakeButton(label, () => SelectTool(mode));
            System.Windows.Automation.AutomationProperties.SetAutomationId(select, "EditorTool" + mode);
            toolbar.Children.Add(select); tools[mode] = select;
        }
        header.Children.Add(toolbar);
        var options = new WrapPanel { Margin = new Thickness(0, 0, 0, 8), VerticalAlignment = VerticalAlignment.Center };
        foreach (var (name, color) in new[] { ("红色",0xFFE65F55u),("橙色",0xFFFFB861u),("黄色",0xFFFFDC64u),
            ("绿色",0xFF46A980u),("蓝色",0xFF4D89CFu),("黑色",0xFF182D30u),("白色",0xFFFFFFFFu) })
            colors.Items.Add(new ComboBoxItem { Content = name, Tag = color });
        colors.SelectedIndex = 0;
        options.Children.Add(colors); options.Children.Add(new TextBlock { Text = "线宽", VerticalAlignment = VerticalAlignment.Center });
        options.Children.Add(thickness);
        foreach (var size in new[] { 18, 24, 32, 48, 64, 96, 144, 192 }) fontSize.Items.Add(size);
        fontSize.SelectedItem = 32;
        options.Children.Add(new TextBlock { Text = "字号", VerticalAlignment = VerticalAlignment.Center });
        options.Children.Add(fontSize); options.Children.Add(text);
        undo = MakeButton("撤销", () => { CancelDraft(); if (Document.Undo()) RefreshImage(); });
        redo = MakeButton("重做", () => { CancelDraft(); if (Document.Redo()) RefreshImage(); });
        options.Children.Add(undo); options.Children.Add(redo);
        options.Children.Add(MakeButton("适应窗口", Fit)); options.Children.Add(zoom);
        var copyButton = MakeButton("复制结果", CopyResult); System.Windows.Automation.AutomationProperties.SetAutomationId(copyButton, "EditorCopy"); options.Children.Add(copyButton);
        options.Children.Add(MakeButton("另存 PNG…", SaveAs));
        var done = MakeButton("完成并挂回", () => { if (SaveDefaultCopy()) Close(); });
        System.Windows.Automation.AutomationProperties.SetAutomationId(done, "EditorDone"); options.Children.Add(done);
        header.Children.Add(options);
        status.Foreground = PaperStyle.Brush("#547161"); status.FontSize = 12; status.Margin = new Thickness(4, 8, 4, 2);
        DockPanel.SetDock(status, Dock.Bottom); layout.Children.Add(status);
        viewport.Background = PaperStyle.Brush("#E1E8DD"); viewport.Content = new Border { Child = surface, Margin = new Thickness(18), Background = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        layout.Children.Add(viewport); Content = layout;
        zoom.ValueChanged += (_, _) => RefreshSurface();
        surface.MouseLeftButtonDown += Begin;
        surface.MouseMove += Move;
        surface.MouseLeftButtonUp += End;
        surface.LostMouseCapture += (_, _) => CancelDraft();
        PreviewKeyDown += Key;
        Closing += (_, e) =>
        {
            CancelDraft();
            if (this.confirmDiscard && Document.IsDirty && MessageBox.Show(this, "编辑尚未保存。关闭将丢弃本次标注，原图不变。", "放弃未保存的编辑？",
                MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) e.Cancel = true;
        };
        Loaded += (_, _) => Fit();
        SelectTool(EditorTool.Pen); RefreshImage();
    }

    internal void SelectTool(EditorTool selected)
    {
        CancelDraft(); tool = selected;
        foreach (var item in tools) item.Value.Background = PaperStyle.Brush(item.Key == selected ? "#D8EBDE" : "#F9FCF6");
        colors.IsEnabled = selected is not (EditorTool.Crop or EditorTool.Mosaic);
        text.IsEnabled = selected == EditorTool.Text; fontSize.IsEnabled = selected == EditorTool.Text;
        thickness.IsEnabled = selected is not (EditorTool.Text or EditorTool.Crop);
        if (selected == EditorTool.Obscure) colors.SelectedIndex = 5;
        surface.Cursor = selected == EditorTool.Text ? Cursors.IBeam : Cursors.Cross;
        status.Text = selected switch
        {
            EditorTool.Text => "输入文字、选择字号后，点击图片放置。",
            EditorTool.Crop => "拖出裁剪区域，松手应用；Esc 取消，撤销可恢复完整画面。",
            EditorTool.Mosaic => "拖出区域进行像素化；马赛克不保证信息不可推断，敏感内容请用不透明遮挡。",
            EditorTool.Obscure => "拖出区域进行不透明遮挡；导出结果为扁平 PNG，原图仍保留。",
            _ => "在图片上拖动标注；Ctrl+Z 撤销，Ctrl+Y 重做；保存始终是新副本。"
        };
    }

    private uint Argb => colors.SelectedItem is ComboBoxItem { Tag: uint color } ? color : 0xFFE65F55;
    private ImagePoint PointAt(MouseEventArgs e)
    {
        var p = e.GetPosition(surface); var crop = Document.State.Crop;
        return new ImagePoint(Math.Clamp(p.X / zoom.Value + crop.X, crop.X, crop.Right), Math.Clamp(p.Y / zoom.Value + crop.Y, crop.Y, crop.Bottom));
    }
    private void Begin(object sender, MouseButtonEventArgs e)
    {
        var p = PointAt(e);
        if (tool == EditorTool.Text) { Commit(new ImageMark(MarkTool.Text, new[] { p }, Argb, ((fontSize.SelectedItem is int size ? size : 32) - 12) / 5.0, text.Text)); return; }
        drawing = true; points.Clear(); points.Add(p); surface.CaptureMouse(); e.Handled = true;
    }
    private void Move(object sender, MouseEventArgs e)
    {
        if (!drawing || e.LeftButton != MouseButtonState.Pressed) return;
        var p = PointAt(e);
        if (tool is EditorTool.Pen or EditorTool.Highlight)
        {
            if (points.Count < ImageEditorDocument.MaxStrokePoints && (Math.Abs(p.X - points[^1].X) + Math.Abs(p.Y - points[^1].Y) >= 0.5)) points.Add(p);
        }
        else { if (points.Count == 1) points.Add(p); else points[^1] = p; }
        if (tool == EditorTool.Crop) surface.CropDraft = ImageRegion.From(points[0], p, Document.State.Crop);
        else if (tool != EditorTool.Mosaic) surface.Draft = new ImageMark(Enum.Parse<MarkTool>(tool.ToString()), points, Argb, thickness.Value);
        else surface.CropDraft = ImageRegion.From(points[0], p, Document.State.Crop);
        surface.InvalidateVisual();
    }
    private void End(object sender, MouseButtonEventArgs e)
    {
        if (!drawing) return;
        var captured = points.ToArray(); var p = PointAt(e); var selected = tool;
        CancelDraft();
        if (selected == EditorTool.Crop) { if (Document.Crop(captured[0], p)) { RefreshImage(); Fit(); } }
        else if (selected is EditorTool.Pen or EditorTool.Highlight)
            Commit(new ImageMark(Enum.Parse<MarkTool>(selected.ToString()), captured, Argb, thickness.Value));
        else Commit(new ImageMark(Enum.Parse<MarkTool>(selected.ToString()), new[] { captured[0], p }, Argb, thickness.Value));
        e.Handled = true;
    }
    internal void CancelDraft()
    {
        drawing = false; points.Clear(); surface.Draft = null; surface.CropDraft = null;
        if (surface.IsMouseCaptured) surface.ReleaseMouseCapture(); surface.InvalidateVisual();
    }
    internal bool Commit(ImageMark mark)
    {
        if (!Document.Add(mark)) { status.Text = "标注未添加：区域过小、无效或已达到编辑上限。"; return false; }
        RefreshImage(); return true;
    }
    internal void RefreshImage()
    {
        rendered = AnnotationRenderer.Render(original, Document.State); surface.Image = rendered; surface.Crop = Document.State.Crop;
        undo.IsEnabled = Document.CanUndo; redo.IsEnabled = Document.CanRedo; RefreshSurface();
    }
    private void RefreshSurface()
    {
        surface.Zoom = zoom.Value; surface.Width = rendered.PixelWidth * zoom.Value; surface.Height = rendered.PixelHeight * zoom.Value; surface.InvalidateVisual();
    }
    private void Fit()
    {
        if (viewport.ViewportWidth <= 0 || viewport.ViewportHeight <= 0) return;
        zoom.Value = Math.Clamp(Math.Min((viewport.ViewportWidth - 45) / rendered.PixelWidth, (viewport.ViewportHeight - 45) / rendered.PixelHeight), 0.1, 1);
    }
    private void CopyResult()
    {
        CancelDraft();
        try { copy(rendered); status.Text = "已复制编辑后的图片；尚未保存的编辑仍需保存。"; }
        catch (System.Runtime.InteropServices.ExternalException) { status.Text = "剪贴板忙碌，请重试。"; }
    }
    internal bool SaveDefaultCopy()
    {
        CancelDraft();
        try
        {
            var path = destination(); AnnotationRenderer.SaveCopy(rendered, sourcePath, path);
            ResultPath = path; Document.MarkSaved(); status.Text = "副本已保存，原图未覆盖。"; return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        { status.Text = "未保存：请使用有权限的新文件名；不能覆盖原图或已有文件。"; return false; }
    }
    private void SaveAs()
    {
        CancelDraft();
        var dialog = new Microsoft.Win32.SaveFileDialog { FileName = Path.GetFileNameWithoutExtension(sourcePath) + "-edited-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png",
            Filter = "PNG 图片|*.png", DefaultExt = ".png", AddExtension = true, OverwritePrompt = false };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            AnnotationRenderer.SaveCopy(rendered, sourcePath, dialog.FileName); ResultPath = dialog.FileName; Document.MarkSaved(); status.Text = "PNG 副本已保存。";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        { status.Text = "未保存：请换一个新文件名或可写目录，原图未改变。"; }
    }
    private void Key(object sender, KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape) { CancelDraft(); e.Handled = true; return; }
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        if (e.Key == System.Windows.Input.Key.S) { if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) SaveAs(); else SaveDefaultCopy(); e.Handled = true; }
        if (Keyboard.FocusedElement is TextBox) return;
        if (e.Key == System.Windows.Input.Key.Z) { CancelDraft(); if (Document.Undo()) RefreshImage(); e.Handled = true; }
        if (e.Key == System.Windows.Input.Key.Y) { CancelDraft(); if (Document.Redo()) RefreshImage(); e.Handled = true; }
    }
    private static Button MakeButton(string label, Action action)
    {
        var button = new Button { Content = label, Background = PaperStyle.Brush("#F9FCF6"), Foreground = PaperStyle.Brush("#355748"),
            BorderBrush = PaperStyle.Brush("#D4DFD4"), BorderThickness = new Thickness(1), Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(3), Template = PaperStyle.Template(radius: 8), Cursor = Cursors.Hand, FontSize = 12 };
        button.Click += (_, _) => action(); return button;
    }
}
