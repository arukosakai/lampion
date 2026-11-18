using Avalonia;
using Avalonia.Media;
using Lampion.App.Controls.Icons;
using Lampion.App.Editor;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Controls.Timeline;

/// <summary>Track headers (kind accent, twirl-down, name, targets, mute and solo) and keyframe-row captions.</summary>
internal sealed class TrackHeaderRenderer(EditorState state)
{
    /// <summary>Draws the header of <paramref name="row"/> with its top at <paramref name="y"/>. Allocation-free.</summary>
    public void Render(DrawingContext ctx, in TimelineRow row, double y)
    {
        var rect = new Rect(0, y, TimelineViewport.HeaderWidth, row.Height);
        var selected = !row.IsLane && state.SelectedTrackId == row.Track.Id;
        ctx.FillRectangle(selected ? TimelinePalette.HeaderSelected : TimelinePalette.Header, rect);
        ctx.DrawLine(TimelinePalette.Divider, rect.BottomLeft, rect.BottomRight);
        if (row.IsLane)
        {
            IconPainter.Draw(ctx, Lucide.Diamond, new Rect(16, y + 12, 10, 10), TimelinePalette.KeyIcon, TimelinePalette.Key);
            TimelineText.Draw(ctx, row.Caption, new Point(31, y + 11), TimelinePalette.Key, 11);
            return;
        }
        var track = row.Track;
        var isLight = track.Kind == TrackKind.Light;
        var accent = track.Kind switch
        {
            TrackKind.Audio => TimelinePalette.Audio,
            TrackKind.Video => TimelinePalette.Video,
            _ => TimelinePalette.Playhead,
        };
        ctx.FillRectangle(accent, new Rect(0, y, 4, row.Height));
        if (isLight)
            IconPainter.Draw(ctx, track.Expanded ? Lucide.ChevronDown : Lucide.ChevronRight, new Rect(8, y + 9, 13, 13), TimelinePalette.TextIcon);
        TimelineText.Draw(ctx, track.Name, new Point(TrackHeaderLayout.NameX, y + 7), TimelinePalette.Text, TrackHeaderLayout.NameSize, TrackHeaderLayout.NameWeight);
        if (!track.Compact)
            TimelineText.Draw(ctx, row.Caption, new Point(26, y + 25), TimelinePalette.DimText, 10);
        DrawToggle(ctx, TrackHeaderLayout.MuteRect(y), "M", track.Muted, TimelinePalette.MuteOn);
        if (isLight)
            DrawToggle(ctx, TrackHeaderLayout.SoloRect(y), "S", track.Solo, TimelinePalette.SoloOn);
    }

    private static void DrawToggle(DrawingContext ctx, Rect rect, string label, bool on, IBrush onBrush)
    {
        ctx.DrawRectangle(on ? onBrush : TimelinePalette.ToggleOff, null, rect, 3, 3);
        TimelineText.Draw(ctx, label, new Point(rect.X + 5, rect.Y + 2), on ? Brushes.Black : TimelinePalette.DimText, 10, FontWeight.Bold);
    }
}
