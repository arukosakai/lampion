using Avalonia;
using Avalonia.Media;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Controls.Timeline;

/// <summary>A light clip: colour-strip preview, name bar with the effect's swatch, fades and transition.</summary>
internal sealed class LightClipRenderer(
    TimelineViewport viewport,
    ColorStripRenderer strip,
    FadeRenderer fades,
    TransitionRenderer transitions,
    ColorBrushCache brushes,
    ClipLabelCache labels)
{
    /// <summary>Draws <paramref name="clip"/> in <paramref name="rect"/>; allocation-free once its colours and label are cached.</summary>
    /// <param name="ctx">Where to draw.</param>
    /// <param name="clip">The clip.</param>
    /// <param name="rect">The clip's outline in control coordinates.</param>
    /// <param name="fixtureCount">Fixtures the clip's track drives.</param>
    public void Render(DrawingContext ctx, LightClip clip, Rect rect, int fixtureCount)
    {
        const double bar = ColorStripRenderer.NameBarHeight;
        using (ctx.PushClip(new RoundedRect(rect, 3)))
        {
            ctx.FillRectangle(TimelinePalette.LightClipBody, rect);
            strip.Render(ctx, clip, rect, fixtureCount);
            ctx.FillRectangle(TimelinePalette.NameBar, new Rect(rect.Left, rect.Top, rect.Width, bar));
            ctx.FillRectangle(brushes.Get(clip.Effect.SwatchColor), new Rect(rect.Left, rect.Top, 3, bar));
            var label = labels.Light(clip, CountKeyed(clip));
            TimelineText.Draw(ctx, label, new Point(Math.Max(rect.Left, TimelineViewport.HeaderWidth) + 6, rect.Top + 1), TimelinePalette.Text, 10.5);
            fades.Render(ctx, rect, clip.FadeIn * viewport.PixelsPerSecond, true);
            fades.Render(ctx, rect, clip.FadeOut * viewport.PixelsPerSecond, false);
        }
        if (clip.TransitionIn is { } transition)
            transitions.Render(ctx, transition, rect);
    }

    private static int CountKeyed(LightClip clip)
    {
        var keyed = 0;
        foreach (var lane in clip.Automation)
            if (lane.Animated)
                keyed++;
        return keyed;
    }
}
