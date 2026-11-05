using Avalonia;
using Avalonia.Media;
using Lampion.App.Editor;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Controls.Timeline;

/// <summary>An audio or video clip: its body, the waveform once it has loaded, and the name bar.</summary>
internal sealed class MediaClipRenderer(EditorState state, WaveformRenderer waveform, ClipLabelCache labels)
{
    /// <summary>Draws <paramref name="clip"/> on a track of <paramref name="kind"/> in <paramref name="rect"/>. Allocation-free once its label is cached.</summary>
    public void Render(DrawingContext ctx, MediaClip clip, Rect rect, TrackKind kind)
    {
        using (ctx.PushClip(new RoundedRect(rect, 3)))
        {
            ctx.FillRectangle(kind == TrackKind.Audio ? TimelinePalette.Audio : TimelinePalette.Video, rect);
            if (kind == TrackKind.Audio && state.Waveforms.TryGetValue(clip.Path, out var peaks) && peaks is not null)
                waveform.Render(ctx, clip, peaks, rect);
            ctx.FillRectangle(TimelinePalette.NameBar, new Rect(rect.Left, rect.Top, rect.Width, ColorStripRenderer.NameBarHeight));
            TimelineText.Draw(ctx, labels.Media(clip, kind), new Point(Math.Max(rect.Left, TimelineViewport.HeaderWidth) + 6, rect.Top + 1), TimelinePalette.Text, 10.5);
        }
    }
}
