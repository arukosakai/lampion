using Avalonia;
using Avalonia.Media;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// The transition box straddling a light clip's left edge, as wide as the transition lasts. On compact tracks there's
/// no room under the name bar, so it shrinks to an unlabelled strip along the bottom.
/// </summary>
internal sealed class TransitionRenderer(TimelineViewport viewport)
{
    /// <summary>Height of the transition box, in pixels.</summary>
    public const double BoxHeight = 13;

    private const double SlimHeight = 4;

    /// <summary>Draws <paramref name="transition"/> for a clip outlined by <paramref name="rect"/>. Allocation-free.</summary>
    public void Render(DrawingContext ctx, Transition transition, Rect rect)
    {
        var half = transition.Duration / 2 * viewport.PixelsPerSecond;
        var width = Math.Max(6, half * 2);
        var roomForBox = rect.Height - ColorStripRenderer.NameBarHeight >= BoxHeight + 2;
        if (!roomForBox)
        {
            ctx.DrawRectangle(TimelinePalette.Transition, null, new Rect(rect.Left - half, rect.Bottom - SlimHeight, width, SlimHeight), 1, 1);
            return;
        }
        var box = new Rect(rect.Left - half, rect.Bottom - 15, width, BoxHeight);
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
