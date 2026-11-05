using System.Text.Json.Serialization;
using Lampion.Core.Color;

namespace Lampion.Core.Model.Effects;

/// <summary>A palette laid across the fixtures, optionally scrolling.</summary>
public sealed class GradientEffect : Effect
{
    private List<string> _colors = ["#FF2A6D", "#05D9E8"];

    /// <summary>
    /// The palette as <c>#RRGGBB</c> strings; empty gives black. Edit it in place; initializers and JSON
    /// replace the default palette rather than appending to it.
    /// </summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Replace)]
    public List<string> Colors
    {
        get => _colors;
        init => _colors = value;
    }

    /// <summary>Times the palette repeats across the fixtures; it wraps, so the last colour blends back into the first.</summary>
    public double Repeat { get; set; } = 1;

    /// <summary>Palette lengths the gradient scrolls per second; 0 holds it still.</summary>
    public double Speed { get; set; }

    /// <inheritdoc />
    public override string DisplayName => "Gradient";

    /// <inheritdoc />
    public override string SwatchColor => Colors.Count > 0 ? Colors[0] : "#FFFFFF";

    /// <inheritdoc />
    public override EffectSample Sample(in EffectContext ctx)
    {
        var colors = Colors;
        if (colors.Count == 0)
            return new(Rgb.Black, 0);
        var across = ctx.Count <= 1 ? 0 : ctx.Index / (double)ctx.Count;
        var position = EffectMath.Frac(across * Repeat + ctx.Time * Speed) * colors.Count;
        var step = (int)Math.Floor(position);
        var from = Rgb.FromHex(colors[step % colors.Count]);
        var to = Rgb.FromHex(colors[(step + 1) % colors.Count]);
        return new(Rgb.Lerp(from, to, position - step), Intensity);
    }

    /// <inheritdoc />
    protected override Effect CloneCore()
    {
        var copy = (GradientEffect)base.CloneCore();
        copy._colors = [.. _colors];
        return copy;
    }
}
