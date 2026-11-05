using Avalonia;
using Avalonia.Media;

namespace Lampion.App.Controls;

/// <summary>
/// The readout under the DMX grid: the hovered channel ("1.17: 255  —  Par 3 · Red") or a hint. The
/// changing value is drawn from cached labels between cached prefix and suffix strings, so a value that
/// changes every frame during playback formats nothing. UI thread only.
/// </summary>
internal sealed class DmxFooter
{
    private const double Size = 11;

    private string _hint = "";
    private int _hintUniverse = int.MinValue;
    private string _prefix = "";
    private string _suffix = "";
    private (int Universe, int Channel, int MapVersion) _labelled = (int.MinValue, -1, -1);

    /// <summary>Draws the readout with its top-left at <paramref name="at"/>.</summary>
    /// <param name="ctx">Where to draw.</param>
    /// <param name="at">Top-left corner.</param>
    /// <param name="universe">The universe on show.</param>
    /// <param name="channel">The hovered 0-based channel, or -1 for the hint.</param>
    /// <param name="value">The hovered channel's DMX value.</param>
    /// <param name="map">Who is patched where, for the hovered channel's description.</param>
    /// <param name="byteLabels">Cached "0".."255" labels.</param>
    public void Render(DrawingContext ctx, Point at, int universe, int channel, byte value, DmxChannelMap map, string[] byteLabels)
    {
        if (channel < 0)
        {
            ctx.DrawText(TextCache.Get(Hint(universe), Size, Brushes.Gray), at);
            return;
        }
        if (_labelled != (universe, channel, map.Version))
        {
            _prefix = $"{universe}.{channel + 1}: ";
            _suffix = map.Owner(channel) is { } owner ? $"  —  {owner} · {map.Role(channel)}" : " (unpatched)";
            _labelled = (universe, channel, map.Version);
        }
        var x = Piece(ctx, _prefix, at.X, at.Y);
        x = Piece(ctx, byteLabels[value], x, at.Y);
        Piece(ctx, _suffix, x, at.Y);
    }

    private string Hint(int universe)
    {
        if (universe != _hintUniverse)
        {
            _hint = $"Universe {universe} · hover a channel for details";
            _hintUniverse = universe;
        }
        return _hint;
    }

    private static double Piece(DrawingContext ctx, string text, double x, double y)
    {
        var formatted = TextCache.Get(text, Size, Brushes.Gray);
        ctx.DrawText(formatted, new Point(x, y));
        return x + formatted.WidthIncludingTrailingWhitespace;
    }
}
