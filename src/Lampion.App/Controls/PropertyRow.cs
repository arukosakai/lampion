using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Lampion.App.Controls;

/// <summary>One property line: [stopwatch] name ........ value [◀ ◆ ▶].</summary>
internal sealed class PropertyRow : Grid
{
    private static readonly IBrush LabelBrush = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xC8));

    /// <summary>Lays out a property line in the fixed Effect Controls columns.</summary>
    /// <param name="name">Property label.</param>
    /// <param name="value">The editor, usually a <see cref="ScrubValue"/>.</param>
    /// <param name="stopwatch">Optional stopwatch in the leading column; null leaves it empty.</param>
    /// <param name="navigator">Optional trailing keyframe navigator or actions.</param>
    /// <param name="tooltip">Optional tooltip on the label.</param>
    public PropertyRow(string name, Control value, Control? stopwatch = null, Control? navigator = null, string? tooltip = null)
    {
        ColumnDefinitions = new ColumnDefinitions("20,118,*,Auto");
        MinHeight = 24;
        if (stopwatch is not null)
            Add(stopwatch, 0);
        var label = new TextBlock { Text = name, Foreground = LabelBrush, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        if (tooltip is not null)
            ToolTip.SetTip(label, tooltip);
        Add(label, 1);
        value.VerticalAlignment = VerticalAlignment.Center;
        Add(value, 2);
        if (navigator is not null)
            Add(navigator, 3);
    }

    private void Add(Control control, int column)
    {
        SetColumn(control, column);
        Children.Add(control);
    }
}
