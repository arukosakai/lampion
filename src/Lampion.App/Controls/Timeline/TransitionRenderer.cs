using Avalonia;
using Avalonia.Media;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Controls.Timeline;

/// <summary>The transition box straddling a light clip's left edge, as wide as the transition lasts.</summary>
internal sealed class TransitionRenderer(TimelineViewport viewport)
{
    /// <summary>Height of the transition box, in pixels.</summary>
    public const double BoxHeight = 13;

    /// <summary>Draws <paramref name="transition"/> for a clip outlined by <paramref name="rect"/>. Allocation-free.</summary>
    public void Render(DrawingContext ctx, Transition transition, Rect rect)
    {
        var half = transition.Duration / 2 * viewport.PixelsPerSecond;
        var box = new Rect(rect.Left - half, rect.Bottom - 15, Math.Max(6, half * 2), BoxHeight);
        ctx.DrawRectangle(TimelinePalette.Transition, TimelinePalette.TransitionOutline, box, 2, 2);
        var label = transition.Kind switch
        {
            TransitionKind.DipToBlack => "Dip",
            TransitionKind.Wipe => "Wipe",
            _ => "Xfade",
        };
        TimelineText.Draw(ctx, label, new Point(box.Left + 3, box.Top), Brushes.White, 9);
    }
}
