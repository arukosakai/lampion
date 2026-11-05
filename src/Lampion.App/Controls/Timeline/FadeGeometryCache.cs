using Avalonia;
using Avalonia.Media;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// The triangular fade ramps, one geometry per (width, height, side) with its corner at the origin.
/// Scrolling and repainting reuse them; only dragging a fade makes new ones. UI thread only.
/// </summary>
internal sealed class FadeGeometryCache
{
    private const int Limit = 256;

    private readonly Dictionary<(double Width, double Height, bool FadeIn), StreamGeometry> _ramps = [];

    /// <summary>
    /// A right triangle with its square corner at the origin: legs run <paramref name="width"/> pixels
    /// horizontally (right for a fade-in, left for a fade-out) and <paramref name="height"/> pixels down.
    /// </summary>
    public StreamGeometry Get(double width, double height, bool fadeIn)
    {
        var key = (width, height, fadeIn);
        if (_ramps.TryGetValue(key, out var geometry))
            return geometry;
        if (_ramps.Count >= Limit)
            _ramps.Clear();
        geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(new Point(0, 0), true);
            g.LineTo(new Point(fadeIn ? width : -width, 0));
            g.LineTo(new Point(0, height));
            g.EndFigure(true);
        }
        _ramps[key] = geometry;
        return geometry;
    }
}
