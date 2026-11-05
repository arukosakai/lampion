using System.Text.Json.Serialization;
using Lampion.Core.Color;

namespace Lampion.Core.Model.Effects;

/// <summary>Steps through a palette, crossfading between colours.</summary>
public sealed class ColorCycleEffect : Effect
{
    private List<string> _colors = ["#FF0000", "#00FF00", "#0000FF"];

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

    /// <summary>Seconds per colour (at least 0.01).</summary>
    public double StepTime { get; set; } = 1;

    /// <summary>Portion of each step spent crossfading, 0..1.</summary>
    public double FadeRatio { get; set; } = 0.5;

    /// <summary>Steps each fixture is offset from the previous one.</summary>
    public double FixtureOffset { get; set; }

    /// <inheritdoc />
    public override string DisplayName => "Color Cycle";

    /// <inheritdoc />
    public override string SwatchColor => Colors.Count > 0 ? Colors[0] : "#FFFFFF";

    /// <inheritdoc />
    public override EffectSample Sample(in EffectContext ctx)
    {
        var colors = Colors;
        if (colors.Count == 0)
            return new(Rgb.Black, 0);
        var position = ctx.Time / Math.Max(0.01, StepTime) + FixtureOffset * ctx.Index;
        var step = (int)Math.Floor(position);
        var within = position - step;
        var current = Rgb.FromHex(colors[EffectMath.Mod(step, colors.Count)]);
        var fadeStart = 1 - Rgb.Clamp01(FadeRatio);
        if (within <= fadeStart || FadeRatio <= 0)
            return new(current, Intensity);
        var next = Rgb.FromHex(colors[EffectMath.Mod(step + 1, colors.Count)]);
        return new(Rgb.Lerp(current, next, (within - fadeStart) / (1 - fadeStart)), Intensity);
    }

    /// <inheritdoc />
    protected override Effect CloneCore()
    {
        var copy = (ColorCycleEffect)base.CloneCore();
        copy._colors = [.. _colors];
        return copy;
    }
}
