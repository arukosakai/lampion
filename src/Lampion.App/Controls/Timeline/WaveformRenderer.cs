using Avalonia;
using Avalonia.Media;
using Lampion.Core.Media;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Controls.Timeline;

/// <summary>An audio clip's peak envelope, one vertical stroke every <see cref="Step"/> pixels, scaled by the clip's volume.</summary>
internal sealed class WaveformRenderer(TimelineViewport viewport)
{
    /// <summary>Pixels between strokes.</summary>
    public const double Step = 1.5;

    /// <summary>Draws the visible part of <paramref name="peaks"/> for <paramref name="clip"/> in <paramref name="rect"/>, below the name bar. Allocation-free.</summary>
    public void Render(DrawingContext ctx, MediaClip clip, AudioPeaks peaks, Rect rect)
    {
        var mid = rect.Top + 16 + (rect.Height - 16) / 2;
        var half = (rect.Height - 18) / 2;
        var buckets = peaks.Peaks;
        var perSecond = peaks.PeaksPerSecond;
        var stepSeconds = Step / viewport.PixelsPerSecond;
        var right = Math.Min(rect.Right, viewport.Width);
        for (var x = Math.Max(rect.Left, TimelineViewport.HeaderWidth); x < right; x += Step)
        {
            var sourceTime = viewport.XToTime(x) - clip.Start + clip.MediaOffset;
            var a = (int)(sourceTime * perSecond);
            var b = (int)((sourceTime + stepSeconds) * perSecond);
            var peak = 0f;
            for (var i = Math.Max(0, a); i <= Math.Min(buckets.Length - 1, b); i++)
                peak = Math.Max(peak, buckets[i]);
            var h = peak * half * clip.Volume;
            ctx.DrawLine(TimelinePalette.WaveLine, new Point(x, mid - h), new Point(x, mid + h));
        }
    }
}
