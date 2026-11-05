using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Lampion.App.Views.ProfileEditor;

/// <summary>The profile editor's colours and small layout helpers, shared by its sections.</summary>
internal static class ProfileEditorUi
{
    /// <summary>Background of a channel row.</summary>
    public static readonly IBrush RowBackground = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x29));

    /// <summary>Background of the preview side and the footer.</summary>
    public static readonly IBrush PanelBackground = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x22));

    /// <summary>Secondary text: labels, hints, readouts.</summary>
    public static readonly IBrush Muted = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A));

    /// <summary>Warnings that don't stop Save: duplicate roles, a changed footprint.</summary>
    public static readonly IBrush Warning = new SolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x20));

    /// <summary>An all-caps section heading with 8 px above it.</summary>
    public static TextBlock Heading(string text) =>
        new() { Text = text.ToUpperInvariant(), Foreground = Muted, FontSize = 10.5, FontWeight = FontWeight.SemiBold, LetterSpacing = 0.8, Margin = new Thickness(0, 8, 0, 0) };

    /// <summary><paramref name="editor"/> with a small label above it.</summary>
    public static Control Labelled(string label, Control editor) =>
        new StackPanel { Spacing = 3, Children = { new TextBlock { Text = label, Foreground = Muted, FontSize = 11 }, editor } };

    /// <summary>
    /// Hides the spinner, which makes number boxes taller than the text boxes beside them; scroll and arrow
    /// keys still step. Returns <paramref name="box"/>.
    /// </summary>
    public static NumericUpDown Plain(NumericUpDown box)
    {
        box.ShowButtonSpinner = false;
        return box;
    }

    /// <summary>
    /// A text box that writes every keystroke through <paramref name="set"/>, rather than on blur: the working
    /// copy is only kept if Save is pressed anyway.
    /// </summary>
    public static TextBox LiveText(string value, Action<string> set)
    {
        var box = new TextBox { Text = value };
        box.TextChanged += (_, _) => set(box.Text ?? "");
        return box;
    }

    /// <summary>Adds <paramref name="control"/> to <paramref name="grid"/> in <paramref name="column"/>.</summary>
    public static void Place(Grid grid, Control control, int column)
    {
        Grid.SetColumn(control, column);
        grid.Children.Add(control);
    }

    /// <summary>A small square glyph button; when not <paramref name="enabled"/> it is faded and ignores clicks.</summary>
    public static Button IconButton(string glyph, string tip, bool enabled, Action onClick)
    {
        var button = new Button
        {
            Content = glyph,
            Width = 24,
            MinHeight = 24,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Background = Brushes.Transparent,
            // Faded instead of disabled: Fluent paints disabled buttons a solid grey block.
            Opacity = enabled ? 1 : 0.2,
            IsHitTestVisible = enabled,
        };
        ToolTip.SetTip(button, tip);
        button.Click += (_, _) => onClick();
        return button;
    }
}
