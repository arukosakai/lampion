using Avalonia;
using Avalonia.Media;

namespace Lampion.App.Controls.Timeline;

/// <summary>Everything right of the headers and below the ruler: row stripes, beat grid, clips, keyframe rows and the drop preview.</summary>
internal sealed class TrackAreaRenderer(
    TimelineViewport viewport,
    TimelineLayout layout,
    BeatGridRenderer grid,
    ClipRenderer clips,
    KeyframeLaneRenderer lanes)
{
    /// <summary>Draws the clip area, with <paramref name="dropPreview"/> (if any) on top. Allocation-free in steady state.</summary>
    public void Render(DrawingContext ctx, Rect? dropPreview)
    {
        var area = viewport.ClipArea;
        var rows = layout.Rows;
        using (ctx.PushClip(area))
        {
            for (var i = 0; i < rows.Length; i++)
            {
                ref readonly var row = ref rows[i];
                if (row.IsLane)
                    ctx.FillRectangle(TimelinePalette.LaneRow, new Rect(TimelineViewport.HeaderWidth, viewport.ContentToY(row.Top), area.Width, row.Height));
                else if (i % 2 == 1)
                    ctx.FillRectangle(TimelinePalette.RowAlt, new Rect(TimelineViewport.HeaderWidth, viewport.ContentToY(row.Top), area.Width, row.Height));
            }
            grid.Render(ctx, area);
            var height = viewport.Height;
            for (var i = 0; i < rows.Length; i++)
            {
                ref readonly var row = ref rows[i];
                var y = viewport.ContentToY(row.Top);
                if (y > height || y + row.Height < TimelineViewport.RulerHeight)
                    continue;
                if (row.IsLane)
                    lanes.Render(ctx, row, y);
                else
                    clips.Render(ctx, row, y);
                var bottom = y + row.Height;
                ctx.DrawLine(TimelinePalette.Divider, new Point(TimelineViewport.HeaderWidth, bottom), new Point(viewport.Width, bottom));
            }
            if (dropPreview is { } drop)
                ctx.DrawRectangle(TimelinePalette.Drop, TimelinePalette.DropOutline, drop, 3, 3);
        }
    }
}
