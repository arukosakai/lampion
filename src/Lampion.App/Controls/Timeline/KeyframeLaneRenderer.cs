using Avalonia;
using Avalonia.Media;
using Lampion.App.Editor;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Controls.Timeline;

/// <summary>A keyframe row: each clip's span, its automation curve and its keys as diamonds (the selected one larger and white).</summary>
internal sealed class KeyframeLaneRenderer(EditorState state, TimelineViewport viewport, TimelineShapes shapes, LaneCurveCache curves)
{
    /// <summary>Draws keyframe row <paramref name="row"/> whose top is at <paramref name="y"/>. Allocation-free while the curves are cached.</summary>
    public void Render(DrawingContext ctx, in TimelineRow row, double y)
    {
        var selectedKey = state.SelectedKey?.Key;
        foreach (var clip in row.Track.Clips)
        {
            if (clip is not LightClip light || row.Lane.FindIn(light) is not { } lane)
                continue;
            var x0 = viewport.TimeToX(clip.Start);
            var x1 = viewport.TimeToX(clip.End);
            if (x1 < TimelineViewport.HeaderWidth || x0 > viewport.Width)
                continue;
            var rect = new Rect(x0, y + ClipRenderer.Inset, x1 - x0, row.Height - ClipRenderer.Inset * 2);
            ctx.DrawRectangle(lane.Enabled ? TimelinePalette.LaneSpanEnabled : TimelinePalette.LaneSpanDisabled, null, rect, 2, 2);
            var scale = LaneValueScale.For(lane, rect);
            var visibleFrom = Math.Max(x0, TimelineViewport.HeaderWidth) - x0;
            var visibleTo = Math.Min(x1, viewport.Width) - x0;
            curves.Draw(ctx, lane, scale, visibleFrom, visibleTo, viewport.PixelsPerSecond);
            foreach (var key in lane.Keys)
            {
                var isSelected = ReferenceEquals(key, selectedKey);
                var center = new Point(viewport.TimeToX(clip.Start + key.Time), scale.ValueToY(key.Value));
                using (ctx.PushTransform(Matrix.CreateTranslation(center.X, center.Y)))
                    ctx.DrawGeometry(isSelected ? Brushes.White : TimelinePalette.Key, TimelinePalette.KeyOutline, isSelected ? shapes.SelectedKey : shapes.Key);
            }
        }
    }
}
