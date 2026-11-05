using Avalonia;
using Avalonia.Media;
using Lampion.App.Rendering;

namespace Lampion.App.Controls.Stage3D;

/// <summary>The bottom-right badge with frames per second, render scale and anti-aliasing.</summary>
internal sealed class FrameRateBadge
{
    private readonly CachedText _text = new();

    /// <summary>Draws the badge in the bottom-right corner of <paramref name="bounds"/>.</summary>
    /// <param name="ctx">Where to draw.</param>
    /// <param name="bounds">The overlay's size.</param>
    /// <param name="framesPerSecond">Smoothed frame rate.</param>
    /// <param name="fidelity">The quality settings in use.</param>
    /// <param name="text">Text colour (shared instance).</param>
    /// <param name="panel">Backdrop (shared instance).</param>
    public void Draw(DrawingContext ctx, Size bounds, double framesPerSecond, StageFidelity fidelity, IBrush text, IBrush panel)
    {
        _text.Clear().Append(framesPerSecond, "0").Append(" fps  ·  ").Append(fidelity.RenderScale, "0%").Append("  ·  ");
        if (fidelity.Samples < 2)
            _text.Append("no AA");
        else
            _text.Append(fidelity.Samples).Append("× AA");
        var fps = TextCache.Get(_text.ToText(), 11.5, text);
        var corner = new Point(bounds.Width - fps.Width - 16, bounds.Height - fps.Height - 14);
        ctx.DrawRectangle(panel, null, new Rect(corner.X - 6, corner.Y - 4, fps.Width + 12, fps.Height + 8), 4, 4);
        ctx.DrawText(fps, corner);
    }
}
