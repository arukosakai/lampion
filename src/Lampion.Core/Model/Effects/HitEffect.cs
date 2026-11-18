using System.Text.Json.Serialization;
using Lampion.Core.Color;

namespace Lampion.Core.Model.Effects;

/// <summary>A sharp flash that decays quadratically, once or on repeat, in one colour or a new one every hit.</summary>
public sealed class HitEffect : Effect
{
    private List<string> _colors = ["#FF2A6D", "#00E5FF", "#FFD700"];

    /// <summary>Flash colour, <c>#RRGGBB</c>, when <see cref="ColorMode"/> is <see cref="HitColorMode.Single"/>.</summary>
    public string Color { get; set; } = "#FFFFFF";

    /// <summary>Whether every hit is <see cref="Color"/> or steps through <see cref="Colors"/>.</summary>
    public HitColorMode ColorMode { get; set; } = HitColorMode.Single;

    /// <summary>
    /// Colours the hits step through in <see cref="HitColorMode.Cycle"/> mode, as <c>#RRGGBB</c>; empty falls back
    /// to <see cref="Color"/>. Initializers and JSON replace the default rather than appending to it.
    /// </summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Replace)]
    public List<string> Colors
    {
        get => _colors;
        init => _colors = value;
    }

    /// <summary>Seconds a hit takes to fade out (at least 0.01).</summary>
    public double Decay { get; set; } = 0.4;

    /// <summary>Seconds between hits; 0.01 or less fires once at the start of the clip.</summary>
    public double Interval { get; set; }

    /// <summary>Seconds each fixture's hit lands after the previous one's, for a rolling hit.</summary>
    public double Offset { get; set; }

    private const int DecaySlot = 0;
    private const int IntervalSlot = 1;
    private const int OffsetSlot = 2;

    private static readonly AnimatableParameter[] Animatable =
    [
        AnimatableParameter.Plain<HitEffect>("decay", "Decay", e => e.Decay, (e, v) => e.Decay = v),
        AnimatableParameter.Plain<HitEffect>("interval", "Interval", e => e.Interval, (e, v) => e.Interval = v),
        AnimatableParameter.Plain<HitEffect>("offset", "Offset", e => e.Offset, (e, v) => e.Offset = v),
    ];

    private protected override IReadOnlyList<AnimatableParameter> DeclaredParameters => Animatable;

    /// <inheritdoc />
    public override string DisplayName => "Hit";

    /// <inheritdoc />
    public override string SwatchColor => ColorMode == HitColorMode.Cycle && Colors.Count > 0 ? Colors[0] : Color;

    /// <inheritdoc />
    public override EffectSample Sample(in EffectContext ctx)
    {
        var interval = ctx.Parameter(IntervalSlot, Interval);
        var age = ctx.Time - ctx.Parameter(OffsetSlot, Offset) * ctx.Index;
        if (age < 0)
            return new(HitColor(0), 0);
        var hit = 0;
        if (interval > 0.01)
        {
            hit = EffectMath.Slot(age / interval);
            age %= interval;
        }
        var fall = Math.Max(0, 1 - age / Math.Max(0.01, ctx.Parameter(DecaySlot, Decay)));
        return new(HitColor(hit), Intensity * fall * fall);
    }

    private Rgb HitColor(int hit)
    {
        var colors = Colors;
        return ColorMode == HitColorMode.Cycle && colors.Count > 0 ? Rgb.FromHex(colors[EffectMath.Mod(hit, colors.Count)]) : Rgb.FromHex(Color);
    }

    /// <inheritdoc />
    protected override Effect CloneCore()
    {
        var copy = (HitEffect)base.CloneCore();
        copy._colors = [.. _colors];
        return copy;
    }
}
