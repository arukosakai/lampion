using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lampion.App.Controls;

/// <summary>Small borderless glyph button used for stopwatches, keyframe arrows and section actions.</summary>
internal sealed class GlyphButton : Button
{
    private static readonly IBrush ActiveBrush = new SolidColorBrush(Color.FromRgb(0x5C, 0xA8, 0xFF));

    /// <summary>Creates the button; the click is marked handled so it doesn't also toggle a section header.</summary>
    /// <param name="glyph">Text or symbol shown.</param>
    /// <param name="tip">Tooltip.</param>
    /// <param name="onClick">Runs on click; pass a no-op when the button only opens a <see cref="Button.Flyout"/>.</param>
    /// <param name="active">Draws the glyph in the AE blue, e.g. a running stopwatch.</param>
    public GlyphButton(string glyph, string tip, Action onClick, bool active = false)
    {
        Content = glyph;
        Padding = new Thickness(3, 0);
        MinHeight = 20;
        MinWidth = 18;
        FontSize = 12;
        Background = Brushes.Transparent;
        BorderThickness = new Thickness(0);
        Foreground = active ? ActiveBrush : Brushes.Gray;
        ToolTip.SetTip(this, tip);
        Click += (_, e) =>
        {
            e.Handled = true;
            onClick();
        };
    }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(Button);
}
