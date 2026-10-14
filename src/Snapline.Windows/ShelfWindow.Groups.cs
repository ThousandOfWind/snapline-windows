using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Snapline.Core;
using Forms = System.Windows.Forms;

namespace Snapline.Windows;

internal sealed partial class ShelfWindow
{
    private readonly HashSet<Guid> expandedGroups = [];
    private static string GroupKey(Guid id) => "group:" + id.ToString("N");

    private void AddImageView(string key, FrameworkElement card, HashSet<string> existing)
    {
        cardViews[key] = card; cards.Children.Add(card);
        if (existing.Contains(key)) return;
        pendingEntry.Add(key);
        if (IsRevealed)
        {
            var motion = hanging[key];
            card.Loaded += (_, _) => { if (IsRevealed && pendingEntry.Remove(key)) motion.Hang(); };
        }
    }

    internal void ToggleGroup(Guid id)
    {
        if (!expandedGroups.Add(id)) expandedGroups.Remove(id);
        Refresh(imagePaths); ShowStatus(expandedGroups.Contains(id) ? "截图组已展开 · 拖组卡片带出整组，拖子图带出单张" : "截图组已收起");
    }

    private FrameworkElement CreateGroupCard(ScreenshotGroup group, string key)
    {
        var theme = HangingTheme.For(HangingKind.ImageGroup);
        ImageIO.TryLoad(group.Members[0], out var cover);
        var size = cover == null ? new ImageCardSize(170, 100) : ImageCardSize.Fit(cover.PixelWidth, cover.PixelHeight, 170, 118);
        var card = new Grid { Width = Math.Max(size.Width + 3, 70) + 12, Height = size.Height + 45, Margin = new Thickness(9, 15, 9, 0), Tag = "SnaplineCard",
            ToolTip = $"截图组 · {group.Members.Count} 张 · 点击展开，拖动整组复制" };
        card.Children.Add(new Border { CornerRadius = new CornerRadius(10), Background = Brush("#EADFF2"), BorderBrush = Brush(theme.Edge),
            BorderThickness = new Thickness(1), Margin = new Thickness(14, 20, 0, 2), RenderTransform = new RotateTransform(4), RenderTransformOrigin = new Point(0.5, 0.1), IsHitTestVisible = false });
        card.Children.Add(new Border { CornerRadius = new CornerRadius(10), Background = Brush("#F5EDFA"), BorderBrush = Brush(theme.Edge),
            BorderThickness = new Thickness(1), Margin = new Thickness(7, 17, 7, 4), RenderTransform = new RotateTransform(-3), RenderTransformOrigin = new Point(0.5, 0.1), IsHitTestVisible = false });
        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(size.Height) });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
        var thumbnails = new Grid { Width = size.Width, Height = size.Height };
        if (cover != null) thumbnails.Children.Add(new Image { Source = cover, Width = size.Width, Height = size.Height, Stretch = Stretch.Fill, Tag = "GroupCoverPixels" });
        thumbnails.Children.Add(new Border { Background = Brush(theme.Header), CornerRadius = new CornerRadius(7), Padding = new Thickness(7, 3, 7, 3),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(4),
            Child = new TextBlock { Text = $"{group.Members.Count} 张", Foreground = Brush(theme.Ink), FontSize = 11 } });
        content.Children.Add(thumbnails);
        var actions = new DockPanel(); Grid.SetRow(actions, 1);
        var toggle = new Button { Content = expandedGroups.Contains(group.Id) ? "收起组" : "展开组", FontSize = 10,
            Foreground = Brush(theme.Ink), Background = Brush(theme.Header), Padding = new Thickness(6, 3, 6, 3),
            BorderThickness = new Thickness(0), Template = PaperStyle.Template(radius: 6), Cursor = Cursors.Hand };
        System.Windows.Automation.AutomationProperties.SetAutomationId(toggle, "GroupExpand");
        toggle.Click += (_, e) => { e.Handled = true; ToggleGroup(group.Id); };
        DockPanel.SetDock(toggle, Dock.Right); actions.Children.Add(toggle);
        actions.Children.Add(new TextBlock { Text = "截图组", FontSize = 11, Foreground = Brush(theme.Ink), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 4, 0) });
        content.Children.Add(actions);
        var frame = new Border { Child = content, CornerRadius = new CornerRadius(12), Background = Brush(theme.Paper), BorderBrush = Brush(theme.Edge),
            BorderThickness = new Thickness(1.5), Padding = new Thickness(0), Margin = new Thickness(0, 14, 12, 0),
            Width = size.Width + 3, Height = size.Height + 31, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Tag = "ScreenshotGroupFrame",
            Effect = new DropShadowEffect { BlurRadius = 9, ShadowDepth = 3, Opacity = 0.2 } };
        card.Children.Add(frame);
        var peg = PaperStyle.Clip(HangingKind.ImageGroup); peg.Tag = "SnaplinePeg"; peg.Resources["HangingKind"] = HangingKind.ImageGroup;
        peg.HorizontalAlignment = HorizontalAlignment.Center; peg.VerticalAlignment = VerticalAlignment.Top; card.Children.Add(peg);
        var movement = new HangingCard(card, peg, StableTilt(key), HangingKind.ImageGroup); hanging[key] = movement;
        card.MouseEnter += (_, _) => movement.Hover(true); card.MouseLeave += (_, _) => movement.Hover(false);
        var menu = new ContextMenu();
        AddMenu(menu, "展开 / 收起截图组", () => ToggleGroup(group.Id));
        AddMenu(menu, "复制整组图片文件", () => controller.CopyGroup(group.Id));
        AddMenu(menu, "解散组（保留全部图片）", () => { expandedGroups.Remove(group.Id); controller.Ungroup(group.Id); });
        card.ContextMenu = menu;
        System.Windows.Automation.AutomationProperties.SetAutomationId(card, "ScreenshotGroup");
        BindImageActions(card, () => controller.ImageGroup(group.Id)?.Members.ToArray() ?? [], movement,
            () => ToggleGroup(group.Id), () => ToggleGroup(group.Id));
        BindGroupDrop(card, frame, group.Members[0]);
        return card;
    }

    private void BindGroupDrop(FrameworkElement target, Border frame, string targetPath)
    {
        target.AllowDrop = true;
        var edge = frame.BorderBrush; var thickness = frame.BorderThickness;
        bool Accept(DragEventArgs e)
        {
            var paths = ImageDragData.SelectionFrom(e.Data);
            return paths != null && controller.CanGroup(targetPath, paths);
        }
        void Feedback(DragEventArgs e)
        {
            var accepted = Accept(e);
            e.Effects = accepted ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true;
            frame.BorderBrush = accepted ? Brush("#9C78BD") : edge;
            frame.BorderThickness = accepted ? new Thickness(3) : thickness;
            if (accepted) ShowStatus(controller.ImageGroupFor(targetPath) == null ? "松手创建截图组" : "松手加入截图组");
        }
        target.DragEnter += (_, e) => Feedback(e);
        target.DragOver += (_, e) => Feedback(e);
        target.DragLeave += (_, _) => { frame.BorderBrush = edge; frame.BorderThickness = thickness; };
        target.Drop += (_, e) =>
        {
            frame.BorderBrush = edge; frame.BorderThickness = thickness;
            var paths = ImageDragData.SelectionFrom(e.Data);
            e.Effects = paths != null && controller.GroupImages(targetPath, paths) != null ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
    }

    private void BindImageActions(FrameworkElement card, Func<string[]> selection, HangingCard movement, Action click, Action doubleClick)
    {
        _ = new ImageCardInput(card, click, doubleClick, () =>
        {
            cardDragging = true; movement.SetDragging(true);
            try { controller.DragImages(card, selection()); }
            finally
            {
                movement.SetDragging(false); cardDragging = false;
                if (deferredPaths != null) { var paths = deferredPaths; deferredPaths = null; Refresh(paths); }
            }
        });
    }
}
