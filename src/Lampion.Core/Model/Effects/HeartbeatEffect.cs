using Lampion.Core.Color;

namespace Lampion.Core.Model.Effects;

/// <summary>A lub-dub double pulse.</summary>
public sealed class HeartbeatEffect : Effect
{
    /// <summary>Colour, <c>#RRGGBB</c>.</summary>
    public string Color { get; set; } = "#FF1744";

    /// <summary>Beats per second (1.2 ≈ 72 bpm).</summary>
    public double Rate { get; set; } = 1.2;

    /// <summary>Intensity between beats, 0..1.</summary>
    public double Minimum { get; set; } = 0.05;

    /// <summary>Phase offset between neighbouring fixtures, in cycles.</summary>
    public double Phase { get; set; }

    /// <inheritdoc />
    public override string DisplayName => "Heartbeat";

    /// <inheritdoc />
    public override string SwatchColor => Color;

    /// <inheritdoc />
    public override EffectSample Sample(in EffectContext ctx)
    {
        var p = EffectMath.Frac(ctx.Time * Rate + Phase * ctx.Index);
        var shape = Math.Max(Beat(p, 0.08, 0.05), 0.6 * Beat(p, 0.3, 0.06));
        return new(Rgb.FromHex(Color), Intensity * (Minimum + (1 - Minimum) * shape));
    }

    /// <summary>A Gaussian bump at <paramref name="center"/> on the wrapped 0..1 phase circle.</summary>
    private static double Beat(double phase, double center, double width)
    {
        var d = Math.Abs(phase - center);
        d = Math.Min(d, 1 - d);
        return Math.Exp(-(d / width) * (d / width));
    }
}
