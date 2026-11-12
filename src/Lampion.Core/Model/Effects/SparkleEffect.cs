using Lampion.Core.Color;

namespace Lampion.Core.Model.Effects;

/// <summary>Random-looking twinkles that are a pure function of time, fixture and seed.</summary>
public sealed class SparkleEffect : Effect
{
    /// <summary>Twinkle colour, <c>#RRGGBB</c>.</summary>
    public string Color { get; set; } = "#FFFFFF";

    /// <summary>Twinkles per fixture per second (at least 0.05).</summary>
    public double Rate { get; set; } = 2;

    /// <summary>Seconds a twinkle takes to fade out (at least 0.01).</summary>
    public double Decay { get; set; } = 0.3;

    /// <summary>Picks a different, but repeatable, random pattern.</summary>
    public int Seed { get; set; } = 7;

    /// <inheritdoc />
    public override string DisplayName => "Sparkle";

    /// <inheritdoc />
    public override string SwatchColor => Color;

    /// <inheritdoc />
    public override EffectSample Sample(in EffectContext ctx)
    {
        // Each fixture's timeline is split into 1/Rate slots with one twinkle at a hashed instant per slot,
        // so the result depends only on time — scrubbing and export see exactly what playback does.
        var slotLength = 1 / Math.Max(0.05, Rate);
        var decay = Math.Max(0.01, Decay);
        var slot = EffectMath.Slot(ctx.Time / slotLength);
        var level = 0.0;
        for (var s = slot - 2; s <= slot; s++)
        {
            var flashAt = (s + EffectMath.Hash(s, ctx.Index, Seed)) * slotLength;
            var age = ctx.Time - flashAt;
            if (age >= 0)
                level = Math.Max(level, Math.Max(0, 1 - age / decay));
        }
        return new(Rgb.FromHex(Color), Intensity * level);
    }
}
