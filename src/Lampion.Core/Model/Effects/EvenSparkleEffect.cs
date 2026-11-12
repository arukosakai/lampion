using Lampion.Core.Color;

namespace Lampion.Core.Model.Effects;

/// <summary>
/// Twinkles like <see cref="SparkleEffect"/>, but evenly spaced in time: hits land every 1/<see cref="Rate"/>
/// seconds, and each fixture is hit exactly once per round of <see cref="EffectContext.Count"/> hits.
/// </summary>
public sealed class EvenSparkleEffect : Effect
{
    /// <summary>Twinkle colour, <c>#RRGGBB</c>.</summary>
    public string Color { get; set; } = "#FFFFFF";

    /// <summary>Hits per second across all fixtures (at least 0.05); each fixture flashes every Count / Rate seconds.</summary>
    public double Rate { get; set; } = 8;

    /// <summary>Seconds a twinkle takes to fade out (at least 0.01).</summary>
    public double Decay { get; set; } = 0.3;

    /// <summary>Which fixture each hit lands on.</summary>
    public SparkleOrder Order { get; set; } = SparkleOrder.Shuffled;

    /// <summary>Picks a different, but repeatable, shuffle. Ignored when <see cref="Order"/> is sequential.</summary>
    public int Seed { get; set; } = 7;

    /// <inheritdoc />
    public override string DisplayName => "Even Sparkle";

    /// <inheritdoc />
    public override string SwatchColor => Color;

    /// <inheritdoc />
    public override EffectSample Sample(in EffectContext ctx)
    {
        var count = Math.Max(1, ctx.Count);
        var index = Math.Clamp(ctx.Index, 0, count - 1);
        var interval = 1 / Math.Max(0.05, Rate);
        var round = (int)Math.Floor(ctx.Time / (interval * count));

        // Only the latest flash matters: it is the brightest. It is in this round, or the previous one if
        // this fixture's turn hasn't come yet.
        var flashAt = FlashTime(round, index, count, interval);
        if (flashAt > ctx.Time)
            flashAt = FlashTime(round - 1, index, count, interval);
        var level = Math.Max(0, 1 - (ctx.Time - flashAt) / Math.Max(0.01, Decay));
        return new(Rgb.FromHex(Color), Intensity * level);
    }

    private double FlashTime(int round, int index, int count, double interval) =>
        ((double)round * count + Turn(round, index, count)) * interval;

    // A fixture's place in the round; shuffled by ranking every fixture's hash, so it's a true permutation.
    private int Turn(int round, int index, int count)
    {
        if (Order == SparkleOrder.Sequential)
            return index;
        var own = EffectMath.Hash(round, index, Seed);
        var turn = 0;
        for (var j = 0; j < count; j++)
        {
            var other = EffectMath.Hash(round, j, Seed);
            if (other < own || (other == own && j < index))
                turn++;
        }
        return turn;
    }
}
