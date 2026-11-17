using Lampion.Core.Color;

namespace Lampion.Core.Model.Effects;

/// <summary>A breathing intensity wave in one colour.</summary>
public sealed class PulseEffect : Effect
{
    /// <summary>Colour, <c>#RRGGBB</c>.</summary>
    public string Color { get; set; } = "#FF2A6D";

    /// <summary>Pulses per second.</summary>
    public double Rate { get; set; } = 1;

    /// <summary>Intensity in the troughs, 0..1.</summary>
    public double Minimum { get; set; }

    /// <summary>Shape of each pulse.</summary>
    public Waveform Wave { get; set; } = Waveform.Sine;

    /// <summary>Phase offset between neighbouring fixtures, in cycles.</summary>
    public double Phase { get; set; }

    private const int RateSlot = 0;
    private const int MinimumSlot = 1;
    private const int PhaseSlot = 2;

    private static readonly AnimatableParameter[] Animatable =
    [
        AnimatableParameter.Frequency<PulseEffect>("rate", "Rate", e => e.Rate, (e, v) => e.Rate = v),
        AnimatableParameter.Plain<PulseEffect>("minimum", "Minimum", e => e.Minimum, (e, v) => e.Minimum = v),
        AnimatableParameter.Plain<PulseEffect>("phase", "Phase", e => e.Phase, (e, v) => e.Phase = v),
    ];

    private protected override IReadOnlyList<AnimatableParameter> DeclaredParameters => Animatable;

    /// <inheritdoc />
    public override string DisplayName => "Pulse";

    /// <inheritdoc />
    public override string SwatchColor => Color;

    /// <inheritdoc />
    public override EffectSample Sample(in EffectContext ctx)
    {
        var minimum = ctx.Parameter(MinimumSlot, Minimum);
        var shape = EffectMath.Shape(Wave, EffectMath.Frac(ctx.Time * ctx.Parameter(RateSlot, Rate) + ctx.Parameter(PhaseSlot, Phase) * ctx.Index));
        return new(Rgb.FromHex(Color), Intensity * (minimum + (1 - minimum) * shape));
    }
}
