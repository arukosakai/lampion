using Lampion.Core.Color;

namespace Lampion.Core.Model.Effects;

/// <summary>Irregular bursts of bright flashes, repeatable for a given seed.</summary>
public sealed class LightningEffect : Effect
{
    /// <summary>Flash colour, <c>#RRGGBB</c>.</summary>
    public string Color { get; set; } = "#E8F0FF";

    /// <summary>Strikes per second, on average (at least 0.02).</summary>
    public double Rate { get; set; } = 0.5;

    /// <summary>Seconds each flash in a strike takes to fade (at least 0.01).</summary>
    public double Decay { get; set; } = 0.12;

    /// <summary>Whether every fixture strikes at once or each on its own.</summary>
    public LightningMode Mode { get; set; } = LightningMode.Together;

    /// <summary>Picks a different, but repeatable, storm.</summary>
    public int Seed { get; set; } = 11;

    /// <inheritdoc />
    public override string DisplayName => "Lightning";

    /// <inheritdoc />
    public override string SwatchColor => Color;

    /// <inheritdoc />
    public override EffectSample Sample(in EffectContext ctx)
    {
        // One strike per 1/Rate slot at a hashed instant; each strike is 1–3 quick flashes, the later ones dimmer.
        var slotLength = 1 / Math.Max(0.02, Rate);
        var decay = Math.Max(0.01, Decay);
        var key = Mode == LightningMode.Together ? 0 : ctx.Index;
        var slot = (int)Math.Floor(ctx.Time / slotLength);
        var level = 0.0;
        for (var s = slot - 1; s <= slot; s++)
        {
            var at = (s + 0.7 * EffectMath.Hash(s, key, Seed)) * slotLength;
            var flashes = 1 + (int)(EffectMath.Hash(s, key, Seed + 1) * 3);
            for (var k = 0; k < flashes; k++)
            {
                var age = ctx.Time - at;
                var brightness = k == 0 ? 1 : 0.4 + 0.6 * EffectMath.Hash(s, key, Seed + 2 + k);
                if (age >= 0)
                    level = Math.Max(level, brightness * Math.Max(0, 1 - age / decay));
                at += 0.06 + 0.1 * EffectMath.Hash(s, key, Seed + 5 + k);
            }
        }
        return new(Rgb.FromHex(Color), Intensity * level);
    }
}
