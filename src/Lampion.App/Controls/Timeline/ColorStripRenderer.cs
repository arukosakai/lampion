using Avalonia;
using Avalonia.Media;
using Lampion.Core.Color;
using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// The preview strip on a light clip: what its effect actually outputs over time (fades included) for the
/// middle fixture of the track, sampled every <see cref="Step"/> pixels.
/// </summary>
internal sealed class ColorStripRenderer(TimelineViewport viewport, ColorBrushCache brushes)
{
    /// <summary>Width of one colour sample, in pixels.</summary>
    public const double Step = 4;

    /// <summary>Height of the name bar the strip sits under, in pixels.</summary>
    public const double NameBarHeight = 16;

    /// <summary>
    /// Fills the visible part of <paramref name="rect"/> below the name bar. Allocation-free once the colours
    /// are in the brush cache. The caller clips to the clip's rounded outline.
    /// </summary>
    /// <param name="ctx">Where to draw.</param>
    /// <param name="clip">The clip whose effect is sampled.</param>
    /// <param name="rect">The clip's outline in control coordinates.</param>
    /// <param name="fixtureCount">Fixtures the track drives; fewer than 1 counts as 1.</param>
    public void Render(DrawingContext ctx, LightClip clip, Rect rect, int fixtureCount)
    {
        var count = Math.Max(1, fixtureCount);
        var visibleLeft = Math.Max(rect.Left, TimelineViewport.HeaderWidth);
        var visibleRight = Math.Min(rect.Right, viewport.Width);
        var span = clip.EffectSpan ?? clip.Duration;
        var top = rect.Top + NameBarHeight;
        var height = rect.Height - NameBarHeight;
        for (var x = visibleLeft; x < visibleRight; x += Step)
        {
            var local = viewport.XToTime(x + Step / 2) - clip.Start;
            var sample = clip.Effect.Sample(new EffectContext(local + clip.EffectOffset, span, count / 2, count));
            var color = (sample.Color * Math.Clamp(sample.Intensity, 0, 1) * clip.Envelope(local)).Clamped();
            var brush = brushes.Get(Rgb.ToByte(color.R), Rgb.ToByte(color.G), Rgb.ToByte(color.B));
            ctx.FillRectangle(brush, new Rect(x, top, Step + 0.5, height));
        }
    }
}
