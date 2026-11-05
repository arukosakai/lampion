using Avalonia.Media;

namespace Lampion.App.Controls.Stage3D;

/// <summary>
/// Brushes and pens for the navigation gizmo's balls, one per axis colour and opacity, made on first use
/// and kept, since the balls fade continuously as the camera turns. UI thread only.
/// </summary>
internal static class GizmoPaint
{
    /// <summary>X, Y and Z ball colours (Blender's).</summary>
    private static readonly Color[] AxisColors = [Color.FromRgb(0xFF, 0x33, 0x52), Color.FromRgb(0x8B, 0xDC, 0x00), Color.FromRgb(0x28, 0x90, 0xFF)];

    private static readonly SolidColorBrush?[,] Fills = new SolidColorBrush?[3, 256];
    private static readonly Pen?[,] Lines = new Pen?[3, 256];
    private static readonly Pen?[,] Rings = new Pen?[3, 256];

    /// <summary>Solid fill of axis colour <paramref name="color"/> at alpha <paramref name="alpha"/>.</summary>
    public static IBrush Fill(int color, byte alpha)
    {
        var c = AxisColors[color];
        return Fills[color, alpha] ??= new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
    }

    /// <summary>The 2 px line from the centre to a positive ball.</summary>
    public static Pen Line(int color, byte alpha) => Lines[color, alpha] ??= new Pen(Fill(color, alpha), 2);

    /// <summary>The 1.5 px outline of a negative ball.</summary>
    public static Pen Ring(int color, byte alpha) => Rings[color, alpha] ??= new Pen(Fill(color, alpha), 1.5);
}
