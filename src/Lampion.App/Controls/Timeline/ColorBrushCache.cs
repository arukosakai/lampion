using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lampion.App.Views;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// Shared solid brushes for colours that come out of the model: 8-bit RGB from effect previews, and
/// <c>#RRGGBB</c> swatches. Each colour gets one brush, so drawing the same light again allocates nothing.
/// UI thread only.
/// </summary>
internal sealed class ColorBrushCache
{
    // 24-bit colours are unbounded in principle; a full clear is cheap and rare in practice.
    private const int Limit = 16384;

    private readonly Dictionary<int, IBrush> _rgb = [];
    private readonly Dictionary<string, IBrush> _hex = new(StringComparer.Ordinal);

    /// <summary>The brush for an opaque 8-bit colour.</summary>
    public IBrush Get(byte r, byte g, byte b)
    {
        var key = (r << 16) | (g << 8) | b;
        if (_rgb.TryGetValue(key, out var brush))
            return brush;
        if (_rgb.Count >= Limit)
            _rgb.Clear();
        brush = new ImmutableSolidColorBrush(Color.FromRgb(r, g, b));
        _rgb[key] = brush;
        return brush;
    }

    /// <summary>The brush for a <c>#RRGGBB</c> colour, parsed like <see cref="Ui.Brush"/>.</summary>
    public IBrush Get(string hex)
    {
        if (_hex.TryGetValue(hex, out var brush))
            return brush;
        if (_hex.Count >= Limit)
            _hex.Clear();
        brush = Ui.Brush(hex);
        _hex[hex] = brush;
        return brush;
    }
}
