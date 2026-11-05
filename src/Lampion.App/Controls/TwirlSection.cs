using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Lampion.App.Controls;

/// <summary>A twirl-down group, like the sections in After Effects' Effect Controls.</summary>
internal sealed class TwirlSection : StackPanel
{
    private const string OpenGlyph = "▾";
    private const string ClosedGlyph = "▸";

    private static readonly IBrush BarBrush = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2E));
    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);

    // Open/closed state survives the panel being rebuilt on every selection change.
    private static readonly Dictionary<string, bool> Remembered = [];

    /// <summary>Creates a section whose open state is remembered per <paramref name="key"/> for the app's lifetime.</summary>
    /// <param name="key">Identifies the section across rebuilds, e.g. "transform".</param>
    /// <param name="header">Shown after the twirl arrow; clicking it toggles the section.</param>
    /// <param name="openByDefault">Initial state the first time this key is shown.</param>
    /// <param name="trailing">Right-aligned buttons in the header bar.</param>
    public TwirlSection(string key, Control header, bool openByDefault = true, params Control[] trailing)
    {
        var open = Remembered.GetValueOrDefault(key, openByDefault);
        Body = new StackPanel { Spacing = 1, Margin = new Thickness(16, 2, 0, 6), IsVisible = open };
        var arrow = new TextBlock { Text = open ? OpenGlyph : ClosedGlyph, Width = 14, Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center };
        var bar = new DockPanel { Background = BarBrush, Height = 24, Cursor = HandCursor };
        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(4, 0), Children = { arrow, header } };
        header.VerticalAlignment = VerticalAlignment.Center;
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(0, 0, 4, 0) };
        foreach (var control in trailing)
        {
            control.VerticalAlignment = VerticalAlignment.Center;
            right.Children.Add(control);
        }
        DockPanel.SetDock(right, Dock.Right);
        bar.Children.Add(right);
        bar.Children.Add(left);
        left.PointerPressed += (_, e) =>
        {
            open = !Body.IsVisible;
            Body.IsVisible = open;
            arrow.Text = open ? OpenGlyph : ClosedGlyph;
            Remembered[key] = open;
            e.Handled = true;
        };
        Children.Add(bar);
        Children.Add(Body);
        Margin = new Thickness(0, 0, 0, 2);
    }

    /// <summary>The twirled-down content; add rows here. Hidden while the section is closed.</summary>
    public StackPanel Body { get; }

    /// <summary>A section with a plain semi-bold title.</summary>
    /// <param name="key">Identifies the section across rebuilds.</param>
    /// <param name="title">Header text.</param>
    /// <param name="openByDefault">Initial state the first time this key is shown.</param>
    /// <param name="trailing">Right-aligned buttons in the header bar.</param>
    public static TwirlSection Titled(string key, string title, bool openByDefault = true, params Control[] trailing) =>
        new(key, new TextBlock { Text = title, FontWeight = FontWeight.SemiBold }, openByDefault, trailing);
}
