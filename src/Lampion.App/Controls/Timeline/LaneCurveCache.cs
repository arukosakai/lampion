using Avalonia;
using Avalonia.Media;
using Lampion.Core.Model.Animation;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// Automation curves as polylines sampled every <see cref="SampleStep"/> pixels, built in clip-local
/// coordinates so scrolling only translates them. A curve is rebuilt when its keys, the zoom, the row size
/// or the value range change, or when scrolling leaves the stretch it was sampled over. UI thread only.
/// </summary>
internal sealed partial class LaneCurveCache
{
    /// <summary>Pixels between curve samples.</summary>
    public const double SampleStep = 2;

    private const int Limit = 512;

    private readonly Dictionary<AutomationLane, Entry> _entries = [];

    /// <summary>Strokes <paramref name="lane"/>'s curve across the part of its clip that is on screen.</summary>
    /// <param name="ctx">Where to draw.</param>
    /// <param name="lane">The lane; times are seconds into its clip.</param>
    /// <param name="scale">Value mapping; its bounds are the clip's span on the row.</param>
    /// <param name="visibleFrom">Left edge of what is on screen, in pixels from the clip's left edge.</param>
    /// <param name="visibleTo">Right edge of what is on screen, in pixels from the clip's left edge.</param>
    /// <param name="pixelsPerSecond">The timeline zoom.</param>
    public void Draw(DrawingContext ctx, AutomationLane lane, in LaneValueScale scale, double visibleFrom, double visibleTo, double pixelsPerSecond)
    {
        if (visibleFrom > visibleTo)
            return;
        if (!_entries.TryGetValue(lane, out var entry))
        {
            if (_entries.Count >= Limit)
                _entries.Clear();
            entry = new Entry();
            _entries[lane] = entry;
        }
        if (!entry.Covers(lane, scale, visibleFrom, visibleTo, pixelsPerSecond))
            entry.Rebuild(lane, scale, visibleFrom, visibleTo, pixelsPerSecond);
        if (entry.Geometry is not { } geometry)
            return;
        using (ctx.PushTransform(Matrix.CreateTranslation(scale.Bounds.Left, scale.Bounds.Top)))
            ctx.DrawGeometry(null, TimelinePalette.Curve, geometry);
    }
}
