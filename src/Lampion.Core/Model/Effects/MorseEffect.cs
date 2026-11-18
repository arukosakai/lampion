using Lampion.Core.Color;

namespace Lampion.Core.Model.Effects;

/// <summary>Blinks a text as Morse code on every fixture at once, once or on repeat.</summary>
public sealed class MorseEffect : Effect
{
    private string _text = "SOS";
    private MorseCode _code = MorseCode.Encode("SOS");

    /// <summary>The message. Letters, digits and common punctuation are keyed; anything else is skipped.</summary>
    public string Text
    {
        get => _text;
        set
        {
            _text = value ?? "";
            _code = MorseCode.Encode(_text);
        }
    }

    /// <summary>Colour of the blinks, <c>#RRGGBB</c>.</summary>
    public string Color { get; set; } = "#FFFFFF";

    /// <summary>Seconds one dot lasts (at least 0.01); a dash is three dots.</summary>
    public double DotTime { get; set; } = 0.15;

    /// <summary>Whether the message repeats, after a word-length gap, until the clip ends.</summary>
    public bool Repeat { get; set; } = true;

    /// <summary>Intensity between blinks, 0..1.</summary>
    public double Background { get; set; }

    private const int DotTimeSlot = 0;
    private const int BackgroundSlot = 1;

    private static readonly AnimatableParameter[] Animatable =
    [
        AnimatableParameter.Period<MorseEffect>("dotTime", "Dot time", e => e.DotTime, (e, v) => e.DotTime = v, minimum: 0.01),
        AnimatableParameter.Plain<MorseEffect>("background", "Background", e => e.Background, (e, v) => e.Background = v),
    ];

    private protected override IReadOnlyList<AnimatableParameter> DeclaredParameters => Animatable;

    /// <inheritdoc />
    public override string DisplayName => "Morse Code";

    /// <inheritdoc />
    public override string SwatchColor => Color;

    /// <inheritdoc />
    public override EffectSample Sample(in EffectContext ctx)
    {
        var code = _code;
        var unit = ctx.Time / Math.Max(0.01, ctx.Parameter(DotTimeSlot, DotTime));
        if (Repeat && code.Length > 0)
            unit = EffectMath.Frac(unit / (code.Length + MorseCode.WordGap)) * (code.Length + MorseCode.WordGap);
        var level = unit >= 0 && code.IsOn(unit) ? 1 : ctx.Parameter(BackgroundSlot, Background);
        return new(Rgb.FromHex(Color), Intensity * level);
    }
}
