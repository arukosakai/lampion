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

    /// <inheritdoc />
    public override string DisplayName => "Pulse";

    /// <inheritdoc />
    public override string SwatchColor => Color;

    /// <inheritdoc />
    public override EffectSample Sample(in EffectContext ctx)
    {
        var shape = EffectMath.Shape(Wave, EffectMath.Frac(ctx.Time * Rate + Phase * ctx.Index));
        return new(Rgb.FromHex(Color), Intensity * (Minimum + (1 - Minimum) * shape));
    }
}
