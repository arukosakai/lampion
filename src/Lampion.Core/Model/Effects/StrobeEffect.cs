using Lampion.Core.Color;

namespace Lampion.Core.Model.Effects;

/// <summary>Hard on/off flashes.</summary>
public sealed class StrobeEffect : Effect
{
    /// <summary>Flash colour, <c>#RRGGBB</c>.</summary>
    public string Color { get; set; } = "#FFFFFF";

    /// <summary>Flashes per second (at least 0.01).</summary>
    public double Rate { get; set; } = 8;

    /// <summary>Fraction of each period the light is on, 0..1.</summary>
    public double Duty { get; set; } = 0.3;

    private const int RateSlot = 0;
    private const int DutySlot = 1;

    private static readonly AnimatableParameter[] Animatable =
    [
        AnimatableParameter.Frequency<StrobeEffect>("rate", "Rate", e => e.Rate, (e, v) => e.Rate = v, minimum: 0.01),
        AnimatableParameter.Plain<StrobeEffect>("duty", "Duty", e => e.Duty, (e, v) => e.Duty = v),
    ];

    private protected override IReadOnlyList<AnimatableParameter> DeclaredParameters => Animatable;

    /// <inheritdoc />
    public override string DisplayName => "Strobe";

    /// <inheritdoc />
    public override string SwatchColor => Color;

    /// <inheritdoc />
    public override EffectSample Sample(in EffectContext ctx)
    {
        var on = EffectMath.Frac(ctx.Time * Math.Max(0.01, ctx.Parameter(RateSlot, Rate))) < ctx.Parameter(DutySlot, Duty);
        return new(Rgb.FromHex(Color), on ? Intensity : 0);
    }
}
