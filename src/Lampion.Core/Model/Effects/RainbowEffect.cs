using Lampion.Core.Color;

namespace Lampion.Core.Model.Effects;

/// <summary>Hue rotation, optionally spread across the fixtures.</summary>
public sealed class RainbowEffect : Effect
{
    /// <summary>Full hue cycles per second.</summary>
    public double Speed { get; set; } = 0.25;

    /// <summary>How much of the wheel is spread across the fixtures (0 = all the same hue, 1 = the whole wheel).</summary>
    public double Spread { get; set; } = 1;

    /// <summary>Saturation, 0..1.</summary>
    public double Saturation { get; set; } = 1;

    /// <summary>Hue at time 0 for the first fixture, in turns (0 = red).</summary>
    public double StartHue { get; set; }

    private const int SpeedSlot = 0;
    private const int SpreadSlot = 1;
    private const int SaturationSlot = 2;
    private const int StartHueSlot = 3;

    private static readonly AnimatableParameter[] Animatable =
    [
        AnimatableParameter.Frequency<RainbowEffect>("speed", "Speed", e => e.Speed, (e, v) => e.Speed = v),
        AnimatableParameter.Plain<RainbowEffect>("spread", "Spread", e => e.Spread, (e, v) => e.Spread = v),
        AnimatableParameter.Plain<RainbowEffect>("saturation", "Saturation", e => e.Saturation, (e, v) => e.Saturation = v),
        AnimatableParameter.Plain<RainbowEffect>("startHue", "Start hue", e => e.StartHue, (e, v) => e.StartHue = v),
    ];

    private protected override IReadOnlyList<AnimatableParameter> DeclaredParameters => Animatable;

    /// <inheritdoc />
    public override string DisplayName => "Rainbow";

    /// <inheritdoc />
    public override string SwatchColor => "#FF00FF";

    /// <inheritdoc />
    public override EffectSample Sample(in EffectContext ctx)
    {
        var hue = ctx.Parameter(StartHueSlot, StartHue) + ctx.Time * ctx.Parameter(SpeedSlot, Speed) + ctx.Spread * ctx.Parameter(SpreadSlot, Spread) * (ctx.Count <= 1 ? 0 : (ctx.Count - 1.0) / ctx.Count);
        return new(Rgb.FromHsv(hue, ctx.Parameter(SaturationSlot, Saturation), 1), Intensity);
    }
}
