using System.Globalization;
using Avalonia.Media;

namespace Lampion.App.Controls;

/// <summary>
/// Shaped text for the custom-drawn controls. Shaping is the costly part of drawing a label, and labels
/// (fixture names, DMX values, ruler times) repeat frame after frame, so each one is shaped once.
/// </summary>
internal static class TextCache
{
    private const int Limit = 4096;

    /// <summary>Inter, registered by <c>WithInterFont</c> as the default family; asking for it by name can miss.</summary>
    public static readonly Typeface Regular = new(FontFamily.Default);

    private static readonly Dictionary<(string Text, Typeface Face, double Size, IBrush Brush), FormattedText> Entries = [];

    /// <remarks>Brushes are part of the key by reference, so pass shared brush instances, not fresh ones.</remarks>
    public static FormattedText Get(string text, double size, IBrush brush, FontWeight weight = FontWeight.Normal)
    {
        var face = weight == FontWeight.Normal ? Regular : new Typeface(FontFamily.Default, FontStyle.Normal, weight);
        var key = (text, face, size, brush);
        if (Entries.TryGetValue(key, out var cached))
            return cached;
        // A crude bound: labels that keep changing (readouts) would otherwise grow it forever.
        if (Entries.Count >= Limit)
            Entries.Clear();
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, size, brush);
        Entries[key] = formatted;
        return formatted;
    }
}
