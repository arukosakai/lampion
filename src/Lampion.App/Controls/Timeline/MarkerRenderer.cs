using Avalonia;
using Avalonia.Media;
using Lampion.App.Editor;

namespace Lampion.App.Controls.Timeline;

/// <summary>Marker flags and labels along the bottom of the ruler.</summary>
internal sealed class MarkerRenderer(EditorState state, TimelineViewport viewport, TimelineShapes shapes)
{
    /// <summary>Draws every marker; the caller clips to the ruler. Allocation-free.</summary>
    public void Render(DrawingContext ctx)
    {
        foreach (var marker in state.Project.Markers)
        {
            var x = viewport.TimeToX(marker.Time);
            using (ctx.PushTransform(Matrix.CreateTranslation(x, 0)))
                ctx.DrawGeometry(TimelinePalette.Marker, null, shapes.Marker);
            TimelineText.Draw(ctx, marker.Label, new Point(x + 6, TimelineViewport.RulerHeight - 15), TimelinePalette.Marker, 9);
        }
    }
}
