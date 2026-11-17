using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Lampion.App.Controls.Icons;

/// <summary>Draws <see cref="Lucide"/> icons straight into a <see cref="DrawingContext"/>, for custom-drawn controls. Allocation-free.</summary>
internal static class IconPainter
{
    private const double ViewBox = 24;

    /// <summary>A Lucide-style stroke in <paramref name="brush"/>; create it once and reuse it, it's in icon units so it scales with the icon.</summary>
    public static IPen Pen(IBrush brush) => new ImmutablePen(brush.ToImmutable(), 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

    /// <summary>Strokes <paramref name="icon"/> scaled into the square <paramref name="bounds"/>, optionally filling it first.</summary>
    /// <param name="context">Where to draw.</param>
    /// <param name="icon">A <see cref="Lucide"/> icon.</param>
    /// <param name="bounds">Where the icon goes; its width sets the size.</param>
    /// <param name="pen">From <see cref="Pen"/>.</param>
    /// <param name="fill">Fills closed shapes, e.g. a keyframe diamond with a key; null leaves them hollow.</param>
    public static void Draw(DrawingContext context, Geometry icon, Rect bounds, IPen pen, IBrush? fill = null)
    {
        var scale = bounds.Width / ViewBox;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(bounds.X, bounds.Y)))
            context.DrawGeometry(fill, pen, icon);
    }
}
