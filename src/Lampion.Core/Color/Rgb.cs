using System.Globalization;

namespace Lampion.Core.Color;

/// <summary>A colour with channels in 0..1. Not clamped until it is turned into DMX.</summary>
public readonly record struct Rgb(double R, double G, double B)
{
    public static readonly Rgb Black = new(0, 0, 0);
    public static readonly Rgb White = new(1, 1, 1);

    public double Max => Math.Max(R, Math.Max(G, B));
    public double Min => Math.Min(R, Math.Min(G, B));

    public static Rgb operator *(Rgb c, double k) => new(c.R * k, c.G * k, c.B * k);
    public static Rgb operator +(Rgb a, Rgb b) => new(a.R + b.R, a.G + b.G, a.B + b.B);

    public Rgb Clamped() => new(Clamp01(R), Clamp01(G), Clamp01(B));

    public static Rgb Lerp(Rgb a, Rgb b, double t) =>
        new(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);

    public static Rgb FromHsv(double hue, double saturation, double value)
    {
        hue = ((hue % 1) + 1) % 1 * 6;
        saturation = Clamp01(saturation);
        var sector = (int)Math.Floor(hue);
        var f = hue - sector;
        var p = value * (1 - saturation);
        var q = value * (1 - saturation * f);
        var t = value * (1 - saturation * (1 - f));
        return sector switch
        {
            0 => new Rgb(value, t, p),
            1 => new Rgb(q, value, p),
            2 => new Rgb(p, value, t),
            3 => new Rgb(p, q, value),
            4 => new Rgb(t, p, value),
            _ => new Rgb(value, p, q),
        };
    }

    /// <summary>Hue 0..1, saturation 0..1, value 0..1.</summary>
    public (double H, double S, double V) ToHsv()
    {
        var max = Max;
        var min = Min;
        var delta = max - min;
        double h = 0;
        if (delta > 1e-9)
        {
            if (max == R)
                h = (G - B) / delta % 6;
            else if (max == G)
                h = (B - R) / delta + 2;
            else
                h = (R - G) / delta + 4;
            h /= 6;
            if (h < 0)
                h += 1;
        }
        return (h, max <= 1e-9 ? 0 : delta / max, max);
    }

    public string ToHex()
    {
        var c = Clamped();
        return $"#{ToByte(c.R):X2}{ToByte(c.G):X2}{ToByte(c.B):X2}";
    }

    public static Rgb FromHex(string? hex)
    {
        if (TryParseHex(hex, out var rgb))
            return rgb;
        return White;
    }

    public static bool TryParseHex(string? hex, out Rgb rgb)
    {
        rgb = White;
        if (string.IsNullOrWhiteSpace(hex))
            return false;
        var s = hex.Trim().TrimStart('#');
        if (s.Length == 3)
            s = string.Concat(s.Select(ch => new string(ch, 2)));
        if (s.Length != 6 || !int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            return false;
        rgb = new Rgb(((value >> 16) & 0xFF) / 255.0, ((value >> 8) & 0xFF) / 255.0, (value & 0xFF) / 255.0);
        return true;
    }

    public static byte ToByte(double v) => (byte)Math.Round(Clamp01(v) * 255);

    public static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : double.IsNaN(v) ? 0 : v;
}
