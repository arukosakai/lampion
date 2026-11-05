using Avalonia;
using Avalonia.Media;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// Fixed outlines drawn many times per frame (keyframe diamonds, marker flags, the playhead head), built
/// once around the origin and placed with a translation. Create after the Avalonia platform is up.
/// </summary>
internal sealed class TimelineShapes
{
    /// <summary>Radius of an unselected keyframe diamond, in pixels.</summary>
    public const double KeyRadius = 4.5;

    /// <summary>Radius of the selected keyframe diamond, in pixels.</summary>
    public const double SelectedKeyRadius = 6;

    /// <summary>A keyframe diamond of radius <see cref="KeyRadius"/> centred on the origin.</summary>
    public StreamGeometry Key { get; } = Diamond(KeyRadius);

    /// <summary>A keyframe diamond of radius <see cref="SelectedKeyRadius"/> centred on the origin.</summary>
    public StreamGeometry SelectedKey { get; } = Diamond(SelectedKeyRadius);

    /// <summary>The downward marker flag on the ruler, with its tip at x = 0.</summary>
    public StreamGeometry Marker { get; } = Polygon(
        new Point(-5, TimelineViewport.RulerHeight - 12),
        new Point(5, TimelineViewport.RulerHeight - 12),
        new Point(0, TimelineViewport.RulerHeight - 4));

    /// <summary>The playhead's head in the ruler, with its tip at x = 0.</summary>
    public StreamGeometry PlayheadHead { get; } = Polygon(
        new Point(-7, 12),
        new Point(7, 12),
        new Point(7, 20),
        new Point(0, TimelineViewport.RulerHeight - 2),
        new Point(-7, 20));

    private static StreamGeometry Diamond(double r) => Polygon(new Point(0, -r), new Point(r, 0), new Point(0, r), new Point(-r, 0));

    private static StreamGeometry Polygon(params ReadOnlySpan<Point> points)
    {
        var geometry = new StreamGeometry();
        using var g = geometry.Open();
        g.BeginFigure(points[0], true);
        for (var i = 1; i < points.Length; i++)
            g.LineTo(points[i]);
        g.EndFigure(true);
        return geometry;
    }
}
