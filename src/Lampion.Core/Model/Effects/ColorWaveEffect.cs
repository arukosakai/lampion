using Lampion.Core.Color;

namespace Lampion.Core.Model.Effects;

/// <summary>Two colours rolling across the fixtures in a smooth wave.</summary>
public sealed class ColorWaveEffect : Effect
{
    /// <summary>Colour at the wave's crest, <c>#RRGGBB</c>.</summary>
    public string Color { get; set; } = "#00E5FF";

    /// <summary>Colour in the wave's trough, <c>#RRGGBB</c>.</summary>
    public string Base { get; set; } = "#3A00FF";

    /// <summary>Crests passing each fixture per second.</summary>
    public double Rate { get; set; } = 0.5;

    /// <summary>Fixtures from one crest to the next (at least 0.1).</summary>
    public double Wavelength { get; set; } = 4;

    /// <summary>Shape of the wave.</summary>
    public Waveform Wave { get; set; } = Waveform.Sine;

    private const int RateSlot = 0;
    private const int WavelengthSlot = 1;

    private static readonly AnimatableParameter[] Animatable =
    [
        AnimatableParameter.Frequency<ColorWaveEffect>("rate", "Rate", e => e.Rate, (e, v) => e.Rate = v),
        AnimatableParameter.Plain<ColorWaveEffect>("wavelength", "Wavelength", e => e.Wavelength, (e, v) => e.Wavelength = v),
    ];

    private protected override IReadOnlyList<AnimatableParameter> DeclaredParameters => Animatable;

    /// <inheritdoc />
    public override string DisplayName => "Color Wave";

    /// <inheritdoc />
    public override string SwatchColor => Color;

    /// <inheritdoc />
    public override EffectSample Sample(in EffectContext ctx)
    {
        var shape = EffectMath.Shape(Wave, EffectMath.Frac(ctx.Time * ctx.Parameter(RateSlot, Rate) - ctx.Index / Math.Max(0.1, ctx.Parameter(WavelengthSlot, Wavelength))));
        return new(Rgb.Lerp(Rgb.FromHex(Base), Rgb.FromHex(Color), shape), Intensity);
    }
}
