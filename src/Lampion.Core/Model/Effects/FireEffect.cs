using Lampion.Core.Color;

namespace Lampion.Core.Model.Effects;

/// <summary>Flickering embers and flames: two octaves of repeatable value noise per fixture.</summary>
public sealed class FireEffect : Effect
{
    /// <summary>Colour when the fire is coolest, <c>#RRGGBB</c>.</summary>
    public string Ember { get; set; } = "#B31200";

    /// <summary>Colour when the fire is hottest, <c>#RRGGBB</c>.</summary>
    public string Flame { get; set; } = "#FFB238";

    /// <summary>Flickers per second (at least 0.05).</summary>
    public double Rate { get; set; } = 8;

    /// <summary>Brightness the embers never drop below, 0..1.</summary>
    public double Minimum { get; set; } = 0.35;

    /// <summary>Picks a different, but repeatable, flicker.</summary>
    public int Seed { get; set; } = 3;

    /// <inheritdoc />
    public override string DisplayName => "Fire";

    /// <inheritdoc />
    public override string SwatchColor => Flame;

    /// <inheritdoc />
    public override EffectSample Sample(in EffectContext ctx)
    {
        var x = ctx.Time * Math.Max(0.05, Rate);
        var heat = 0.65 * EffectMath.Noise(x, ctx.Index, Seed) + 0.35 * EffectMath.Noise(x * 2.3, ctx.Index, Seed + 1);
        return new(Rgb.Lerp(Rgb.FromHex(Ember), Rgb.FromHex(Flame), heat), Intensity * (Minimum + (1 - Minimum) * heat));
    }
}
