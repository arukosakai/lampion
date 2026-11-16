using Avalonia;
using Avalonia.Media;
using Lampion.App.Editor;

namespace Lampion.App.Controls.Timeline;

/// <summary>Marker flags and labels along the bottom of the ruler.</summary>
internal sealed class MarkerRenderer(EditorState state, TimelineViewport viewport, TimelineShapes shapes)
{
    /// <summary>Top of the band the flags and labels sit in, in control y; hit testing uses the same band.</summary>
    public const double Top = TimelineViewport.RulerHeight - 15;

    /// <summary>Pixels from a marker's time to the left edge of its label.</summary>
    public const double LabelOffset = 6;

    private const double LabelSize = 9;

    /// <summary>Width of <paramref name="label"/> as drawn, in pixels. Allocation-free once the label is cached.</summary>
    public static double LabelWidth(string label) =>
        label.Length == 0 ? 0 : TextCache.Get(label, LabelSize, TimelinePalette.Marker).Width;

    /// <summary>Draws every marker; the caller clips to the ruler. Allocation-free.</summary>
    public void Render(DrawingContext ctx)
    {
        foreach (var marker in state.Project.Markers)
        {
            var x = viewport.TimeToX(marker.Time);
            using (ctx.PushTransform(Matrix.CreateTranslation(x, 0)))
                ctx.DrawGeometry(TimelinePalette.Marker, null, shapes.Marker);
            TimelineText.Draw(ctx, marker.Label, new Point(x + LabelOffset, Top), TimelinePalette.Marker, LabelSize);
        }
    }
}
