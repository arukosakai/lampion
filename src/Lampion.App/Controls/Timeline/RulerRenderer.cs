using Avalonia;
using Avalonia.Media;
using Lampion.App.Editor;

namespace Lampion.App.Controls.Timeline;

/// <summary>The time ruler: labelled ticks at a spacing that suits the zoom, five sub-ticks each, markers and the end of the show.</summary>
internal sealed class RulerRenderer(EditorState state, TimelineViewport viewport, MarkerRenderer markers)
{
    private const double MinLabelSpacing = 80;
    private const int SubTicks = 5;

    private static readonly double[] Steps = [0.1, 0.25, 0.5, 1, 2, 5, 10, 15, 30, 60, 120, 300, 600];

    private readonly RulerLabelCache _labels = new();

    /// <summary>Draws the ruler across the top of the control. Allocation-free once the visible labels are cached.</summary>
    public void Render(DrawingContext ctx)
    {
        const double h = TimelineViewport.RulerHeight;
        var width = viewport.Width;
        ctx.FillRectangle(TimelinePalette.Ruler, new Rect(0, 0, width, h));
        ctx.FillRectangle(TimelinePalette.Header, new Rect(0, 0, TimelineViewport.HeaderWidth, h));

        var step = NiceStep(MinLabelSpacing / viewport.PixelsPerSecond);
        using (ctx.PushClip(new Rect(TimelineViewport.HeaderWidth, 0, Math.Max(0, width - TimelineViewport.HeaderWidth), h)))
        {
            for (var n = (long)Math.Floor(viewport.ScrollX / step); ; n++)
            {
                var t = n * step;
                var x = viewport.TimeToX(t);
                if (x >= width)
                    break;
                ctx.DrawLine(TimelinePalette.Tick, new Point(x, h - 10), new Point(x, h));
                TimelineText.Draw(ctx, _labels.Get(n, step), new Point(x + 3, 3), TimelinePalette.DimText, 10);
                for (var s = 1; s < SubTicks; s++)
                {
                    var sx = viewport.TimeToX(t + step * s / SubTicks);
                    ctx.DrawLine(TimelinePalette.Tick, new Point(sx, h - 4), new Point(sx, h));
                }
            }
            markers.Render(ctx);
            var endX = viewport.TimeToX(state.Project.Duration);
            ctx.DrawLine(TimelinePalette.ShowEnd, new Point(endX, 0), new Point(endX, h));
        }
    }

    private static double NiceStep(double minimum)
    {
        foreach (var step in Steps)
            if (step >= minimum)
                return step;
        return 1200;
    }
}
