using Lampion.Core.Color;

namespace Lampion.Core.Model.Effects;

/// <summary>A sharp flash that decays quadratically, once or on repeat.</summary>
public sealed class HitEffect : Effect
{
    /// <summary>Flash colour, <c>#RRGGBB</c>.</summary>
    public string Color { get; set; } = "#FFFFFF";

    /// <summary>Seconds a hit takes to fade out (at least 0.01).</summary>
    public double Decay { get; set; } = 0.4;

    /// <summary>Seconds between hits; 0.01 or less fires once at the start of the clip.</summary>
    public double Interval { get; set; }

    /// <summary>Seconds each fixture's hit lands after the previous one's, for a rolling hit.</summary>
    public double Offset { get; set; }

    private const int DecaySlot = 0;
    private const int IntervalSlot = 1;
    private const int OffsetSlot = 2;

    private static readonly AnimatableParameter[] Animatable =
    [
        AnimatableParameter.Plain<HitEffect>("decay", "Decay", e => e.Decay, (e, v) => e.Decay = v),
        AnimatableParameter.Plain<HitEffect>("interval", "Interval", e => e.Interval, (e, v) => e.Interval = v),
        AnimatableParameter.Plain<HitEffect>("offset", "Offset", e => e.Offset, (e, v) => e.Offset = v),
    ];

    private protected override IReadOnlyList<AnimatableParameter> DeclaredParameters => Animatable;

    /// <inheritdoc />
    public override string DisplayName => "Hit";

    /// <inheritdoc />
    public override string SwatchColor => Color;

    /// <inheritdoc />
    public override EffectSample Sample(in EffectContext ctx)
    {
        var interval = ctx.Parameter(IntervalSlot, Interval);
        var age = ctx.Time - ctx.Parameter(OffsetSlot, Offset) * ctx.Index;
        if (age < 0)
            return new(Rgb.FromHex(Color), 0);
        if (interval > 0.01)
            age %= interval;
        var fall = Math.Max(0, 1 - age / Math.Max(0.01, ctx.Parameter(DecaySlot, Decay)));
        return new(Rgb.FromHex(Color), Intensity * fall * fall);
    }
}
