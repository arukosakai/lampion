using System.Globalization;

namespace Lampion.Core.Color;

/// <summary>
/// A colour with linear channels nominally in 0..1. Values are not clamped until they are turned into DMX,
/// so light can be summed and scaled past 1 while blending. Every member is allocation-free except
/// <see cref="ToHex"/>.
/// </summary>
/// <param name="R">Red, 0..1 (may exceed the range mid-blend).</param>
/// <param name="G">Green, 0..1 (may exceed the range mid-blend).</param>
/// <param name="B">Blue, 0..1 (may exceed the range mid-blend).</param>
public readonly record struct Rgb(double R, double G, double B)
{
    /// <summary>All channels 0.</summary>
    public static readonly Rgb Black = new(0, 0, 0);

    /// <summary>All channels 1; also what unparseable hex colours fall back to.</summary>
    public static readonly Rgb White = new(1, 1, 1);

    /// <summary>The largest channel.</summary>
    public double Max => Math.Max(R, Math.Max(G, B));

    /// <summary>The smallest channel; for a light, the part that is white.</summary>
    public double Min => Math.Min(R, Math.Min(G, B));

    /// <summary>Scales every channel by <paramref name="k"/>.</summary>
    public static Rgb operator *(Rgb c, double k) => new(c.R * k, c.G * k, c.B * k);

    /// <summary>Adds channel by channel, the way two lights on the same surface add up.</summary>
    public static Rgb operator +(Rgb a, Rgb b) => new(a.R + b.R, a.G + b.G, a.B + b.B);

    /// <summary>Each channel clamped to 0..1, with NaN becoming 0.</summary>
    public Rgb Clamped() => new(Clamp01(R), Clamp01(G), Clamp01(B));

    /// <summary>Linear interpolation; <paramref name="t"/> is not clamped, so values outside 0..1 extrapolate.</summary>
    public static Rgb Lerp(Rgb a, Rgb b, double t) => new(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);

    /// <summary>A colour from hue, saturation and value.</summary>
    /// <param name="hue">Turns around the colour wheel; any value, wrapped to 0..1 (0 = red, 1/3 = green).</param>
    /// <param name="saturation">0..1, clamped.</param>
    /// <param name="value">Brightness, normally 0..1; not clamped.</param>
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

    /// <summary>Hue 0..1 (0 for greys), saturation 0..1 and value (the largest channel).</summary>
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

    /// <summary>The clamped colour as <c>#RRGGBB</c> in upper case. Allocates the string.</summary>
    public string ToHex()
    {
        var c = Clamped();
        return $"#{ToByte(c.R):X2}{ToByte(c.G):X2}{ToByte(c.B):X2}";
    }

    /// <summary>Parses <c>#RRGGBB</c> or <c>#RGB</c> (the <c>#</c> is optional); anything else gives <see cref="White"/>.</summary>
    public static Rgb FromHex(string? hex) => TryParseHex(hex, out var rgb) ? rgb : White;

    /// <summary>
    /// Parses <c>#RRGGBB</c> or <c>#RGB</c>, ignoring surrounding whitespace and any number of leading
    /// <c>#</c>. Allocation-free, so effects can call it every frame.
    /// </summary>
    /// <param name="hex">The text to parse; null or blank fails.</param>
    /// <param name="rgb">The colour, or <see cref="White"/> when parsing fails.</param>
    /// <returns>Whether <paramref name="hex"/> was a colour.</returns>
    public static bool TryParseHex(string? hex, out Rgb rgb)
    {
        rgb = White;
        if (string.IsNullOrWhiteSpace(hex))
            return false;
        scoped ReadOnlySpan<char> s = hex.AsSpan().Trim().TrimStart('#');
        Span<char> expanded = stackalloc char[6];
        if (s.Length == 3)
        {
            for (var i = 0; i < 3; i++)
                expanded[i * 2] = expanded[i * 2 + 1] = s[i];
            s = expanded;
        }
        if (s.Length != 6 || !int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            return false;
        rgb = new Rgb(((value >> 16) & 0xFF) / 255.0, ((value >> 8) & 0xFF) / 255.0, (value & 0xFF) / 255.0);
        return true;
    }

    /// <summary>A 0..1 level as a DMX byte (clamped, rounded to nearest, NaN as 0).</summary>
    public static byte ToByte(double v) => (byte)Math.Round(Clamp01(v) * 255);

    /// <summary><paramref name="v"/> clamped to 0..1, with NaN becoming 0.</summary>
    public static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : double.IsNaN(v) ? 0 : v;
}
