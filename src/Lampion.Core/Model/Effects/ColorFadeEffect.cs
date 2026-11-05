using Lampion.Core.Color;
using Lampion.Core.Model.Animation;

namespace Lampion.Core.Model.Effects;

/// <summary>Blends from one colour to another across the clip, optionally rolling across the fixtures.</summary>
public sealed class ColorFadeEffect : Effect
{
    /// <summary>Start colour, <c>#RRGGBB</c>.</summary>
    public string From { get; set; } = "#FF0000";

    /// <summary>End colour, <c>#RRGGBB</c>.</summary>
    public string To { get; set; } = "#0000FF";

    /// <summary>Seconds the fade start is delayed per fixture in track order, for a rolling fade.</summary>
    public double Offset { get; set; }

    /// <summary>Curve of the fade.</summary>
    public Easing Easing { get; set; } = Easing.Linear;

    /// <inheritdoc />
    public override string DisplayName => "Color Fade";

    /// <inheritdoc />
    public override string SwatchColor => From;

    /// <inheritdoc />
    public override EffectSample Sample(in EffectContext ctx)
    {
        var span = Math.Max(1e-6, ctx.Duration - Offset * Math.Max(0, ctx.Count - 1));
        var t = Rgb.Clamp01((ctx.Time - Offset * ctx.Index) / span);
        return new(Rgb.Lerp(Rgb.FromHex(From), Rgb.FromHex(To), EasingCurve.Apply(Easing, t)), Intensity);
    }
}
