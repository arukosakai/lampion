using Avalonia;
using Avalonia.Media;
using Lampion.App.Editor;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Controls.Timeline;

/// <summary>The clips of one track row: culls off-screen ones, dims muted tracks and outlines the selection.</summary>
internal sealed class ClipRenderer(EditorState state, TimelineViewport viewport, LightClipRenderer lights, MediaClipRenderer media)
{
    /// <summary>Gap between a clip and the edges of its row, in pixels.</summary>
    public const double Inset = 3;

    private const double MutedOpacity = 0.45;

    /// <summary>Draws the clips of track row <paramref name="row"/> whose top is at <paramref name="y"/>. Allocation-free once labels and colours are cached.</summary>
    public void Render(DrawingContext ctx, in TimelineRow row, double y)
    {
        var track = row.Track;
        using var opacity = ctx.PushOpacity(track.Muted ? MutedOpacity : 1);
        foreach (var clip in track.Clips)
        {
            var x0 = viewport.TimeToX(clip.Start);
            var x1 = viewport.TimeToX(clip.End);
            if (x1 < TimelineViewport.HeaderWidth || x0 > viewport.Width)
                continue;
            var rect = new Rect(x0, y + Inset, Math.Max(2, x1 - x0), row.Height - Inset * 2);
            switch (clip)
            {
                case LightClip light:
                    lights.Render(ctx, light, rect, row.TargetCount);
                    break;
                case MediaClip mediaClip:
                    media.Render(ctx, mediaClip, rect, track.Kind);
                    break;
            }
            ctx.DrawRectangle(null, state.SelectedClipIds.Contains(clip.Id) ? TimelinePalette.Selected : TimelinePalette.ClipOutline, rect, 3, 3);
        }
    }
}
