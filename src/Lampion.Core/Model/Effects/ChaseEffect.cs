using System.Text.Json.Serialization;
using Lampion.Core.Color;

namespace Lampion.Core.Model.Effects;

/// <summary>A light running across the fixtures with a fading tail; the colour advances every lap.</summary>
public sealed class ChaseEffect : Effect
{
    private List<string> _colors = ["#FFFFFF"];

    /// <summary>
    /// Colours used lap by lap, as <c>#RRGGBB</c>; empty means white. Edit it in place; initializers and JSON
    /// replace the default rather than appending to it.
    /// </summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Replace)]
    public List<string> Colors
    {
        get => _colors;
        init => _colors = value;
    }

    /// <summary>Seconds the head spends on each fixture (at least 0.01).</summary>
    public double StepTime { get; set; } = 0.25;

    /// <summary>Length of the fading tail, in fixtures; 0 or less is no tail.</summary>
    public double Tail { get; set; } = 1;

    /// <summary>Which way the head runs.</summary>
    public ChaseDirection Direction { get; set; } = ChaseDirection.Forward;

    /// <summary>Intensity of fixtures the chase is not on, 0..1.</summary>
    public double Background { get; set; }

    /// <inheritdoc />
    public override string DisplayName => "Chase";

    /// <inheritdoc />
    public override string SwatchColor => Colors.Count > 0 ? Colors[0] : "#FFFFFF";

    /// <inheritdoc />
    public override EffectSample Sample(in EffectContext ctx)
    {
        var count = Math.Max(1, ctx.Count);
        var position = ctx.Time / Math.Max(0.01, StepTime);
        var laps = position / count;
        var lap = (int)Math.Floor(laps);
        var head = Direction switch
        {
            ChaseDirection.Backward => count - 1 - EffectMath.Frac(laps) * count,
            ChaseDirection.Bounce when count > 1 => Bounce(position, count),
            _ => EffectMath.Frac(laps) * count,
        };
        // Distance behind the head, wrapping so the tail runs off one end and back in the other.
        var behind = Direction == ChaseDirection.Backward ? ctx.Index - head : head - ctx.Index;
        if (Direction != ChaseDirection.Bounce)
            behind = ((behind % count) + count) % count;
        else
            behind = Math.Abs(behind);
        var level = behind < 1 ? 1 : Tail <= 0 ? 0 : Math.Max(0, 1 - (behind - 1) / Tail);
        level = Math.Max(level, Background);
        var colors = Colors;
        var color = colors.Count == 0 ? Rgb.White : Rgb.FromHex(colors[EffectMath.Mod(lap, colors.Count)]);
        return new(color, Intensity * level);
    }

    private static double Bounce(double position, int count)
    {
        var period = 2 * (count - 1);
        var p = position % period;
        return p <= count - 1 ? p : period - p;
    }

    /// <inheritdoc />
    protected override Effect CloneCore()
    {
        var copy = (ChaseEffect)base.CloneCore();
        copy._colors = [.. _colors];
        return copy;
    }
}
