using Avalonia;
using Avalonia.Media;
using Lampion.App.Editor;

namespace Lampion.App.Controls.Timeline;

/// <summary>Beat and bar lines behind the clips, thinned to whole bars when beats would be closer than 6 pixels.</summary>
internal sealed class BeatGridRenderer(EditorState state, TimelineViewport viewport)
{
    private const double MinSpacing = 6;

    /// <summary>Draws the grid over <paramref name="area"/> (the clip area). Allocation-free.</summary>
    public void Render(DrawingContext ctx, Rect area)
    {
        var pps = viewport.PixelsPerSecond;
        var secondsPerBeat = state.SecondsPerBeat;
        var beat = secondsPerBeat;
        if (beat * pps < MinSpacing)
            beat *= 4;
        if (beat * pps < MinSpacing)
            return;
        var offset = state.Project.BeatOffset;
        var first = Math.Floor((viewport.ScrollX - offset) / beat);
        var barBeats = beat == secondsPerBeat ? 4 : 1;
        for (var k = first; ; k++)
        {
            var x = viewport.TimeToX(offset + k * beat);
            if (x > area.Right)
                break;
            if (x < area.Left)
                continue;
            ctx.DrawLine((long)k % barBeats == 0 ? TimelinePalette.GridBar : TimelinePalette.GridBeat, new Point(x, area.Top), new Point(x, area.Bottom));
        }
    }
}
