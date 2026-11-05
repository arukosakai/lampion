using Avalonia;
using Avalonia.Media;
using Lampion.App.Editor;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// Paints the timeline (everything but the playhead, which <see cref="PlayheadLayer"/> paints on its own):
/// the clip area, the track headers and the ruler, in that order. Owns the renderers and their caches.
/// Create after the Avalonia platform is up; UI thread only.
/// </summary>
internal sealed class TimelineRenderer
{
    private readonly TimelineViewport _viewport;
    private readonly TimelineLayout _layout;
    private readonly TrackAreaRenderer _trackArea;
    private readonly TrackHeaderRenderer _headers;
    private readonly RulerRenderer _ruler;

    /// <summary>Wires up the renderers for <paramref name="state"/>'s project as laid out by <paramref name="layout"/>.</summary>
    public TimelineRenderer(EditorState state, TimelineViewport viewport, TimelineLayout layout, TimelineShapes shapes)
    {
        _viewport = viewport;
        _layout = layout;
        var brushes = new ColorBrushCache();
        var labels = new ClipLabelCache();
        var lights = new LightClipRenderer(
            viewport,
            new ColorStripRenderer(viewport, brushes),
            new FadeRenderer(new FadeGeometryCache()),
            new TransitionRenderer(viewport),
            brushes,
            labels);
        var media = new MediaClipRenderer(state, new WaveformRenderer(viewport), labels);
        _trackArea = new TrackAreaRenderer(
            viewport,
            layout,
            new BeatGridRenderer(state, viewport),
            new ClipRenderer(state, viewport, lights, media),
            new KeyframeLaneRenderer(state, viewport, shapes, new LaneCurveCache()));
        _headers = new TrackHeaderRenderer(state);
        _ruler = new RulerRenderer(state, viewport, new MarkerRenderer(state, viewport, shapes));
    }

    /// <summary>Paints the whole timeline; <paramref name="dropPreview"/> marks where a drag from another panel would land.</summary>
    public void Render(DrawingContext ctx, Rect? dropPreview)
    {
        var width = _viewport.Width;
        var height = _viewport.Height;
        ctx.FillRectangle(TimelinePalette.Background, new Rect(0, 0, width, height));
        _trackArea.Render(ctx, dropPreview);

        var rows = _layout.Rows;
        using (ctx.PushClip(new Rect(0, TimelineViewport.RulerHeight, TimelineViewport.HeaderWidth, Math.Max(0, height - TimelineViewport.RulerHeight))))
            for (var i = 0; i < rows.Length; i++)
                _headers.Render(ctx, rows[i], _viewport.ContentToY(rows[i].Top));

        _ruler.Render(ctx);
        ctx.DrawLine(TimelinePalette.HeaderEdge, new Point(TimelineViewport.HeaderWidth, 0), new Point(TimelineViewport.HeaderWidth, height));
    }
}
