using System.Numerics;
using Avalonia;
using Avalonia.Media;
using Lampion.App.Rendering;

namespace Lampion.App.Controls.Stage3D;

/// <summary>Metre ticks along the X and Z axes, drawn over the top view every 2 m out to ±20 m.</summary>
internal static class MetreRuler
{
    private const int Reach = 20;
    private const int Spacing = 2;

    private static readonly Pen TickPen = new(new SolidColorBrush(Color.FromArgb(120, 140, 160, 190)), 1);
    private static readonly string[] XLabels = Labels("{0} m");
    private static readonly string[] ZLabels = Labels("z {0}");

    /// <summary>Draws the ruler through <paramref name="projector"/>.</summary>
    public static void Draw(DrawingContext ctx, in ScreenProjector projector)
    {
        for (var m = -Reach; m <= Reach; m += Spacing)
        {
            var label = (m + Reach) / Spacing;
            if (projector.TryProject(new Vector3(m, 0, 0), out var x))
            {
                ctx.DrawLine(TickPen, new Point(x.X, x.Y - 4), new Point(x.X, x.Y + 4));
                OverlayText.Draw(ctx, XLabels[label], new Point(x.X, x.Y + 6), Brushes.Gray, 9, null, center: true);
            }
            if (m != 0 && projector.TryProject(new Vector3(0, 0, m), out var z))
                OverlayText.Draw(ctx, ZLabels[label], new Point(z.X + 4, z.Y - 6), Brushes.Gray, 9, null);
        }
    }

    private static string[] Labels(string format)
    {
        var labels = new string[Reach * 2 / Spacing + 1];
        for (var i = 0; i < labels.Length; i++)
            labels[i] = string.Format(System.Globalization.CultureInfo.CurrentCulture, format, -Reach + i * Spacing);
        return labels;
    }
}
