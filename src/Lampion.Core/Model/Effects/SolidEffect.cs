using Lampion.Core.Color;

namespace Lampion.Core.Model.Effects;

/// <summary>One static colour on every fixture.</summary>
public sealed class SolidEffect : Effect
{
    /// <summary>The colour as <c>#RRGGBB</c>; unparseable text shows white.</summary>
    public string Color { get; set; } = "#FFFFFF";

    /// <inheritdoc />
    public override string DisplayName => "Solid Color";

    /// <inheritdoc />
    public override string SwatchColor => Color;

    /// <inheritdoc />
    public override EffectSample Sample(in EffectContext ctx) => new(Rgb.FromHex(Color), Intensity);
}
