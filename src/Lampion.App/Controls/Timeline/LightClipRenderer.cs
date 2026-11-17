using Avalonia;
using Avalonia.Media;
using Lampion.App.Controls.Icons;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Controls.Timeline;

/// <summary>A light clip: colour-strip preview, name bar with the effect's swatch, fades and transition.</summary>
internal sealed class LightClipRenderer(
    TimelineViewport viewport,
    ColorStripRenderer strip,
    FadeRenderer fades,
    TransitionRenderer transitions,
    ColorBrushCache brushes)
{
    /// <summary>Draws <paramref name="clip"/> in <paramref name="rect"/>; allocation-free once its colours and name are cached.</summary>
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
            DrawCaption(ctx, clip, rect);
            fades.Render(ctx, rect, clip.FadeIn * viewport.PixelsPerSecond, true);
            fades.Render(ctx, rect, clip.FadeOut * viewport.PixelsPerSecond, false);
        }
        if (clip.TransitionIn is { } transition)
            transitions.Render(ctx, transition, rect);
    }

    // The name, then a key diamond and the number of animated lanes when there are any.
    private static void DrawCaption(DrawingContext ctx, LightClip clip, Rect rect)
    {
        var x = Math.Max(rect.Left, TimelineViewport.HeaderWidth) + 6;
        x += TimelineText.Draw(ctx, clip.Name, new Point(x, rect.Top + 1), TimelinePalette.Text, 10.5) + 8;
        var keyed = CountKeyed(clip);
        if (keyed == 0)
            return;
        IconPainter.Draw(ctx, Lucide.Diamond, new Rect(x, rect.Top + 3.5, 9, 9), TimelinePalette.TextIcon, TimelinePalette.Text);
        TimelineText.Draw(ctx, CountLabels.Of(keyed), new Point(x + 11, rect.Top + 1), TimelinePalette.Text, 10.5);
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
