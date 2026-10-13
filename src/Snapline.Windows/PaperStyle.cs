using System.Windows;
using Snapline.Core;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Snapline.Windows;

internal static class PaperStyle
{
    internal static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));

    internal static FrameworkElement Symbol(string geometry, double size = 18) => new Path
    {
        Data = Geometry.Parse(geometry), Stroke = Brush("#436B59"), StrokeThickness = 1.7,
        StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round, Stretch = Stretch.Uniform, Width = size, Height = size,
        IsHitTestVisible = false
    };

    internal const string Capture = "M0,6 L0,0 6,0 M12,0 L18,0 18,6 M18,12 L18,18 12,18 M6,18 L0,18 0,12";
    internal const string Fold = "M0,6 L6,0 12,6";
    internal const string Cross = "M0,0 L8,8 M8,0 L0,8";

    internal static Button Action(string label, string icon, string hint, bool primary, System.Action action)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(Symbol(icon, primary ? 16 : 12));
        content.Children.Add(new TextBlock { Text = label, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        var button = new Button
        {
            Content = content, ToolTip = hint, FontSize = 12, FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#355748"), Background = Brush(primary ? "#DAEDDF" : "#F3F6F0"),
            BorderBrush = Brush(primary ? "#B2D3BD" : "#D4DFD4"), BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(6, 0, 0, 0),
            Cursor = Cursors.Hand, Template = Template(),
            Effect = new DropShadowEffect { Color = Color.FromRgb(25, 54, 41), BlurRadius = 6, ShadowDepth = 2, Opacity = 0.12 }
        };
        System.Windows.Automation.AutomationProperties.SetName(button, label);
        button.Click += (_, _) => action();
        AttachHover(button);
        return button;
    }

    internal static void AttachHover(Button button)
    {
        button.Loaded += (_, _) =>
        {
            button.ApplyTemplate();
            if (button.Resources["SnaplineHover"] is HoverMotion) return;
            if (button.Template.FindName("Paper", button) is FrameworkElement paper)
                button.Resources["SnaplineHover"] = new HoverMotion(button, paper);
        };
    }

    internal static HoverMotion? Feedback(Button button) => button.Resources["SnaplineHover"] as HoverMotion;

    internal static ControlTemplate Template(bool transparent = false, double radius = 12,
        string hoverColor = "#E7F3E8", string pressedColor = "#A8D1BE")
    {
        var border = new FrameworkElementFactory(typeof(Border), "Paper");
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        if (!transparent)
        {
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Border.BackgroundProperty, Brush(hoverColor), "Paper"));
            template.Triggers.Add(hover);
            var pressed = new Trigger { Property = System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty, Value = true };
            pressed.Setters.Add(new Setter(Border.BackgroundProperty, Brush(pressedColor), "Paper"));
            template.Triggers.Add(pressed);
        }
        return template;
    }

    internal static Border Clip(HangingKind kind)
    {
        if (kind == HangingKind.Image) return Peg();
        var theme = HangingTheme.For(kind);
        if (kind == HangingKind.ImageGroup)
        {
            var band = new Grid { IsHitTestVisible = false };
            band.Children.Add(new Border { Width = 14, Height = 2, Background = Brush("#EADFF4"), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 7, 0, 0) });
            band.Children.Add(new Border { Width = 14, Height = 2, Background = Brush("#EADFF4"), VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 7) });
            return new Border { Width = 22, Height = 25, CornerRadius = new CornerRadius(4), Background = Brush(theme.Clip),
                BorderBrush = Brush(theme.Edge), BorderThickness = new Thickness(1), Child = band, IsHitTestVisible = false };
        }
        var details = new Grid { IsHitTestVisible = false };
        var wire = new Path { Data = Geometry.Parse("M4,14 L4,6 C4,-1 14,-1 14,6 L14,14"),
            Stroke = Brush(kind == HangingKind.Window ? "#EAF2F7" : "#FFF0C2"), StrokeThickness = 1.5,
            Width = 18, Height = 18, Stretch = Stretch.Fill, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(2, 0, 2, 0) };
        details.Children.Add(wire);
        details.Children.Add(new Border { Height = 8, Margin = new Thickness(1, 15, 1, 1), Background = Brush(kind == HangingKind.Window ? "#536E80" : "#856637"), CornerRadius = new CornerRadius(2) });
        return new Border { Width = 22, Height = 25, CornerRadius = new CornerRadius(3), BorderThickness = new Thickness(1),
            BorderBrush = Brush(theme.Edge), Background = Brush(theme.Clip), Child = details, IsHitTestVisible = false };
    }

    internal static Border Peg(double width = 12, double height = 27)
    {
        var marks = new Grid { IsHitTestVisible = false };
        marks.Children.Add(new Border { Height = 1.4, Width = width * 0.5, Background = Brush("#A76B42"), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, height * 0.35, 0, 0) });
        marks.Children.Add(new Border { Height = height * 0.33, Width = 1, Background = Brush("#C78B54"), VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 3) });
        return new Border { Width = width, Height = height, CornerRadius = new CornerRadius(4), Background = Brush("#EAAF75"), Child = marks, IsHitTestVisible = false };
    }
}
