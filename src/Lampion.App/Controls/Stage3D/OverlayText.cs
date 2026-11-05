using Avalonia;
using Avalonia.Media;

namespace Lampion.App.Controls.Stage3D;

/// <summary>Draws a label over the stage through <see cref="TextCache"/>, optionally on a rounded backdrop.</summary>
internal static class OverlayText
{
    /// <summary>Draws <paramref name="text"/>; pass shared brush instances, as the text cache keys on them.</summary>
    /// <param name="ctx">Where to draw.</param>
    /// <param name="text">The label.</param>
    /// <param name="at">Top-left of the text, or top-centre with <paramref name="center"/>.</param>
    /// <param name="brush">Text colour.</param>
    /// <param name="size">Font size in pixels.</param>
    /// <param name="background">Backdrop with 6×4 px padding, or null for none.</param>
    /// <param name="center">Centre the text horizontally on <paramref name="at"/>.</param>
    public static void Draw(DrawingContext ctx, string text, Point at, IBrush brush, double size, IBrush? background, bool center = false)
    {
        var formatted = TextCache.Get(text, size, brush);
        var origin = center ? new Point(at.X - formatted.Width / 2, at.Y) : at;
        if (background is not null)
            ctx.DrawRectangle(background, null, new Rect(origin.X - 6, origin.Y - 4, formatted.Width + 12, formatted.Height + 8), 4, 4);
        ctx.DrawText(formatted, origin);
    }
}
