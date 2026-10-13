using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Snapline.Windows;

/// <summary>Generated images only. Payload/controls tested without touching clipboard or injecting pointer input.</summary>
internal static class GroupVerification
{
    internal static async Task RunAsync(SnaplineApplication app, ShelfWindow shelf, string data, string output, Action<bool, string> check)
    {
        var a = Path.Combine(data, "Inbox", "group-a.png"); var b = Path.Combine(data, "Inbox", "group-b.png");
        var c = Path.Combine(data, "Inbox", "group-c.png");
        foreach (var path in new[] { a, b, c }) { ImageIO.SavePng(ImageIO.TestImage(), path); app.AddFixtureImage(path); }
        Motion.Reduced = false; Motion.OverrideSystem = true;
        shelf.Reveal(System.Windows.Forms.Screen.PrimaryScreen!); await Task.Delay(300);
        var source = ImageDragData.Create(new[] { a });
        check(((string[])source.GetData(DataFormats.FileDrop)!).SequenceEqual(new[] { a }), "image drag retains standard FileDrop for other apps");
        check(ImageDragData.SelectionFrom(source)!.Single() == a, "in-app drag carries bounded session selection");
        var foreign = new DataObject(DataFormats.FileDrop, new[] { a });
        check(ImageDragData.SelectionFrom(foreign) == null, "external file drop cannot silently group library items");
        var corrupt = new DataObject(); corrupt.SetData(ImageDragData.Format, "{bad-json");
        check(ImageDragData.SelectionFrom(corrupt) == null, "malformed group drag payload is rejected");
        check(app.CanGroup(b, new[] { a }), "image grouping target accepts valid other image");
        var id = app.GroupImages(b, new[] { a })!.Value;
        await Task.Delay(400); shelf.FinishMotion();
        check(app.ImageGroup(id)!.Members.Count == 2, "drop command creates a two-image group");
        var frame = Descendants(shelf).OfType<FrameworkElement>().Single(e => Equals(e.Tag, "ScreenshotGroupFrame"));
        check(frame != null, "collapsed group renders stacked photo frame");
        check(Descendants(shelf).OfType<FrameworkElement>().Count(e => Equals(e.Tag, "PhotoPaperFrame")) >= 1,
            "unrelated screenshots remain outside the new group");
        check(Descendants(shelf).OfType<Button>().Any(but => System.Windows.Automation.AutomationProperties.GetAutomationId(but) == "GroupExpand"),
            "group has explicit expand control");
        var expand = Descendants(shelf).OfType<Button>().Single(but => System.Windows.Automation.AutomationProperties.GetAutomationId(but) == "GroupExpand");
        expand.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); await Task.Delay(300);
        check(Descendants(shelf).OfType<FrameworkElement>().Count(e => Equals(e.Tag, "PhotoPaperFrame")) >= 3, "expand exposes individual member image cards");
        check(app.ImageGroup(id)!.Members.Count == 2, "expansion does not dissolve group membership");
        check(app.GroupImages(b, new[] { c }) == id && app.ImageGroup(id)!.Members.Count == 3, "dropping another image appends to existing group");
        var stored = new Snapline.Core.SettingsStore(data).Load();
        check(stored.Groups.Any(g => g.Id == id && g.Members.Count == 3), "application grouping commands persist ID and membership to isolated settings");
        var copyData = app.GroupCopyData(id)!;
        check(((string[])copyData.GetData(DataFormats.FileDrop)!).Length == 3 && !copyData.GetDataPresent(DataFormats.Bitmap),
            "copy-group payload contains all files rather than silently copying only the cover image");
        var pickupFixture = new Border { Width = 170, Height = 165, AllowDrop = true };
        using (var pickup = new DragVisualSession(pickupFixture, ImageIO.TestImage(), 3, keepSourceHitTarget: true))
            check(pickupFixture.IsHitTestVisible, "group pickup retains source hit target so self-drop cannot fall through to another app");
        var groupData = ImageDragData.Create(app.ImageGroup(id)!.Members.ToArray());
        check(((string[])groupData.GetData(DataFormats.FileDrop)!).Length == 3 && ImageDragData.SelectionFrom(groupData)!.Length == 3,
            "whole-group drag contains every image for export and group merge");
        await Task.Delay(300); shelf.FinishMotion(); shelf.SaveTestRender(Path.Combine(output, "groups-expanded.png"));
        shelf.ToggleGroup(id); await Task.Delay(250); shelf.FinishMotion(); shelf.SaveTestRender(Path.Combine(output, "groups-collapsed.png"));
        check(Descendants(shelf).OfType<FrameworkElement>().Count(e => Equals(e.Tag, "ScreenshotGroupFrame")) == 1, "collapse keeps one group cover");
        check(Descendants(shelf).OfType<FrameworkElement>().Where(e => e.AllowDrop).Count() >= 1, "image/group cards are explicit in-app drop targets");
        app.TakeOutImage(id, c);
        check(app.ImageGroup(id)!.Members.Count == 2 && app.ImageGroupFor(c) == null && File.Exists(c), "take member out retains its file and makes it independent");
        app.Ungroup(id);
        check(app.ImageGroup(id) == null && new[] { a, b, c }.All(File.Exists), "ungroup keeps all image sources and library entries");
    }

    private static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var item in Descendants(child)) yield return item;
        }
    }
}
