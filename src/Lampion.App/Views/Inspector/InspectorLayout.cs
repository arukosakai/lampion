using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lampion.App.Controls;

namespace Lampion.App.Views.Inspector;

/// <summary>The inspector's structural pieces: the title block, twirl-down sections, property rows and hints.</summary>
internal static class InspectorLayout
{
    /// <summary>Adds the bold title and grey subtitle that head every selection.</summary>
    /// <param name="root">The inspector's content panel.</param>
    /// <param name="title">Usually the selection's name.</param>
    /// <param name="subtitle">A line of context (kind, address, time range); null leaves it blank.</param>
    public static void Title(Panel root, string title, string? subtitle)
    {
        root.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeight.SemiBold, Margin = new Thickness(4, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
        root.Children.Add(new TextBlock { Text = subtitle ?? "", Foreground = Brushes.Gray, FontSize = 11, Margin = new Thickness(4, 0, 0, 8) });
    }

    /// <summary>Adds a titled twirl-down section to <paramref name="root"/>; see <see cref="TwirlSection.Titled"/>.</summary>
    public static TwirlSection Section(Panel root, string key, string title, bool open = true, params Control[] trailing)
    {
        var section = TwirlSection.Titled(key, title, open, trailing);
        root.Children.Add(section);
        return section;
    }

    /// <summary>Adds a plain property row (no stopwatch) to the section's body.</summary>
    public static void Row(TwirlSection section, string name, Control value, string? tip = null) =>
        section.Body.Children.Add(new PropertyRow(name, value, tooltip: tip));

    /// <summary>A small grey explanatory line.</summary>
    /// <param name="text">The hint.</param>
    /// <param name="margin">Placement within the section body.</param>
    /// <param name="wrap">Wraps long hints; off for one-liners such as readouts.</param>
    public static TextBlock Hint(string text, Thickness margin, bool wrap = true) =>
        new() { Text = text, Foreground = Brushes.Gray, FontSize = 11, TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap, Margin = margin };
}
