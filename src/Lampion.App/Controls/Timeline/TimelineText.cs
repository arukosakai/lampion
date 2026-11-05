using Avalonia;
using Avalonia.Media;

namespace Lampion.App.Controls.Timeline;

/// <summary>Draws labels through <see cref="TextCache"/>, so a label that repeats is shaped once.</summary>
internal static class TimelineText
{
    /// <summary>Draws <paramref name="text"/> with its top-left at <paramref name="at"/>; allocation-free once the label is cached.</summary>
    /// <param name="ctx">Where to draw.</param>
    /// <param name="text">The label; pass a cached string, not a freshly formatted one.</param>
    /// <param name="at">Top-left corner.</param>
    /// <param name="brush">A shared brush (it's part of the cache key).</param>
    /// <param name="size">Font size in pixels.</param>
    /// <param name="weight">Font weight.</param>
    public static void Draw(DrawingContext ctx, string text, Point at, IBrush brush, double size, FontWeight weight = FontWeight.Normal) =>
        ctx.DrawText(TextCache.Get(text, size, brush, weight), at);
}
