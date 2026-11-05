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

    /// <inheritdoc />
    public override string DisplayName => "Rainbow";

    /// <inheritdoc />
    public override string SwatchColor => "#FF00FF";

    /// <inheritdoc />
    public override EffectSample Sample(in EffectContext ctx)
    {
        var hue = StartHue + ctx.Time * Speed + ctx.Spread * Spread * (ctx.Count <= 1 ? 0 : (ctx.Count - 1.0) / ctx.Count);
        return new(Rgb.FromHsv(hue, Saturation, 1), Intensity);
    }
}
