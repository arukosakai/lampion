using Avalonia;
using Avalonia.Media;

namespace Lampion.App.Controls.Timeline;

/// <summary>The shaded fade-in/fade-out ramps on a light clip and the square handles used to drag them.</summary>
internal sealed class FadeRenderer(FadeGeometryCache ramps)
{
    /// <summary>Size of the square fade handle, in pixels.</summary>
    public const double HandleSize = 6;

    /// <summary>Draws one fade of a clip outlined by <paramref name="rect"/>; the caller clips to the clip.</summary>
    /// <param name="ctx">Where to draw.</param>
    /// <param name="rect">The clip's outline in control coordinates.</param>
    /// <param name="width">Fade length in pixels; ramps under half a pixel only show the handle.</param>
    /// <param name="fadeIn">True for the fade-in at the left edge, false for the fade-out at the right.</param>
    public void Render(DrawingContext ctx, Rect rect, double width, bool fadeIn)
    {
        var handleX = fadeIn ? rect.Left + width : rect.Right - width;
        if (width > 0.5)
        {
            var corner = new Point(fadeIn ? rect.Left : rect.Right, rect.Top + ColorStripRenderer.NameBarHeight);
            var ramp = ramps.Get(width, rect.Bottom - corner.Y, fadeIn);
            using (ctx.PushTransform(Matrix.CreateTranslation(corner.X, corner.Y)))
                ctx.DrawGeometry(TimelinePalette.FadeShade, TimelinePalette.FadeOutline, ramp);
        }
        ctx.DrawRectangle(Brushes.White, null, new Rect(handleX - HandleSize / 2, rect.Top + 1, HandleSize, HandleSize), 1, 1);
    }
}
