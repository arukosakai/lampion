using Avalonia;
using Avalonia.Media;
using Lampion.App.Editor;

namespace Lampion.App.Controls.Timeline;

/// <summary>The playhead line and head, and the timecode readout in the ruler's left corner.</summary>
internal sealed class PlayheadRenderer(EditorState state, TimelineViewport viewport, TimelineShapes shapes)
{
    private static readonly Point TimecodeAt = new(10, 6);

    /// <summary>Draws the playhead over the whole control. Allocation-free; runs every frame during playback.</summary>
    public void Render(DrawingContext ctx)
    {
        var playhead = state.Playhead;
        TimecodeText.Draw(ctx, playhead, TimecodeAt, TimelinePalette.Playhead, 14, FontWeight.SemiBold);
        var x = viewport.TimeToX(playhead);
        if (x < TimelineViewport.HeaderWidth || x > viewport.Width)
            return;
        ctx.DrawLine(TimelinePalette.PlayheadLine, new Point(x, TimelineViewport.RulerHeight - 2), new Point(x, viewport.Height));
        using (ctx.PushTransform(Matrix.CreateTranslation(x, 0)))
            ctx.DrawGeometry(TimelinePalette.Playhead, null, shapes.PlayheadHead);
    }
}
