using System.Text.Json.Serialization;
using Lampion.Core.Color;

namespace Lampion.Core.Model;

/// <summary>Where one fixture sits within a clip, for effects that vary in time or across fixtures.</summary>
public readonly record struct EffectContext(double Time, double Duration, int Index, int Count)
{
    /// <summary>The fixture's place in the track's order, 0..1 (0.5 when there is only one).</summary>
    public double Spread => Count <= 1 ? 0.5 : Index / (double)(Count - 1);
}

public readonly record struct EffectSample(Rgb Color, double Intensity);

[JsonConverter(typeof(JsonStringEnumConverter<ChaseDirection>))]
public enum ChaseDirection
{
    Forward,
    Backward,
    Bounce,
}

[JsonConverter(typeof(JsonStringEnumConverter<Waveform>))]
public enum Waveform
{
    Sine,
    Triangle,
    Saw,
    Square,
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SolidEffect), "solid")]
[JsonDerivedType(typeof(ColorFadeEffect), "fade")]
[JsonDerivedType(typeof(RainbowEffect), "rainbow")]
[JsonDerivedType(typeof(ColorCycleEffect), "cycle")]
[JsonDerivedType(typeof(ChaseEffect), "chase")]
[JsonDerivedType(typeof(StrobeEffect), "strobe")]
[JsonDerivedType(typeof(PulseEffect), "pulse")]
[JsonDerivedType(typeof(SparkleEffect), "sparkle")]
public abstract class Effect
{
    public double Intensity { get; set; } = 1;

    [JsonIgnore]
    public abstract string DisplayName { get; }

    /// <summary>A representative colour for drawing the clip on the timeline.</summary>
    [JsonIgnore]
    public abstract string SwatchColor { get; }

    public abstract EffectSample Sample(in EffectContext ctx);

    public Effect Clone() => (Effect)MemberwiseCloneDeep();

    protected virtual object MemberwiseCloneDeep() => MemberwiseClone();

    protected static double Frac(double v) => v - Math.Floor(v);

    public static Effect Create(string kind) =>
        kind switch
        {
            "fade" => new ColorFadeEffect(),
            "rainbow" => new RainbowEffect(),
            "cycle" => new ColorCycleEffect(),
            "chase" => new ChaseEffect(),
            "strobe" => new StrobeEffect(),
            "pulse" => new PulseEffect(),
            "sparkle" => new SparkleEffect(),
            _ => new SolidEffect(),
        };

    public static readonly IReadOnlyList<(string Kind, string Name, string Description)> Catalog =
    [
        ("solid", "Solid Color", "One static colour"),
        ("fade", "Color Fade", "Blend from one colour to another across the clip"),
        ("rainbow", "Rainbow", "Hue rotation, optionally spread across fixtures"),
        ("cycle", "Color Cycle", "Step through a palette, crossfading"),
        ("chase", "Chase", "A light running across the fixtures"),
        ("strobe", "Strobe", "Hard on/off flashes"),
        ("pulse", "Pulse", "Breathing intensity wave"),
        ("sparkle", "Sparkle", "Random twinkles"),
    ];
}

public sealed class SolidEffect : Effect
{
    public string Color { get; set; } = "#FFFFFF";

    public override string DisplayName => "Solid Color";
    public override string SwatchColor => Color;

    public override EffectSample Sample(in EffectContext ctx) => new(Rgb.FromHex(Color), Intensity);
}

public sealed class ColorFadeEffect : Effect
{
    public string From { get; set; } = "#FF0000";
    public string To { get; set; } = "#0000FF";

    /// <summary>Seconds the fade start is delayed per fixture in track order, for a rolling fade.</summary>
    public double Offset { get; set; }

    public Easing Easing { get; set; } = Easing.Linear;

    public override string DisplayName => "Color Fade";
    public override string SwatchColor => From;

    public override EffectSample Sample(in EffectContext ctx)
    {
        var span = Math.Max(1e-6, ctx.Duration - Offset * Math.Max(0, ctx.Count - 1));
        var t = Rgb.Clamp01((ctx.Time - Offset * ctx.Index) / span);
        return new(Rgb.Lerp(Rgb.FromHex(From), Rgb.FromHex(To), AutomationLane.Ease(Easing, t)), Intensity);
    }
}

public sealed class RainbowEffect : Effect
{
    /// <summary>Full hue cycles per second.</summary>
    public double Speed { get; set; } = 0.25;

    /// <summary>How much of the wheel is spread across the fixtures (0 = all the same hue).</summary>
    public double Spread { get; set; } = 1;

    public double Saturation { get; set; } = 1;
    public double StartHue { get; set; }

    public override string DisplayName => "Rainbow";
    public override string SwatchColor => "#FF00FF";

    public override EffectSample Sample(in EffectContext ctx)
    {
        var hue = StartHue + ctx.Time * Speed + ctx.Spread * Spread * (ctx.Count <= 1 ? 0 : (ctx.Count - 1.0) / ctx.Count);
        return new(Rgb.FromHsv(hue, Saturation, 1), Intensity);
    }
}

public sealed class ColorCycleEffect : Effect
{
    public List<string> Colors { get; set; } = ["#FF0000", "#00FF00", "#0000FF"];

    /// <summary>Seconds per colour.</summary>
    public double StepTime { get; set; } = 1;

    /// <summary>Portion of each step spent crossfading, 0..1.</summary>
    public double FadeRatio { get; set; } = 0.5;

    /// <summary>Steps each fixture is offset from the previous one.</summary>
    public double FixtureOffset { get; set; }

    public override string DisplayName => "Color Cycle";
    public override string SwatchColor => Colors.FirstOrDefault() ?? "#FFFFFF";

    public override EffectSample Sample(in EffectContext ctx)
    {
        if (Colors.Count == 0)
            return new(Rgb.Black, 0);
        var position = ctx.Time / Math.Max(0.01, StepTime) + FixtureOffset * ctx.Index;
        var step = (int)Math.Floor(position);
        var within = position - step;
        var current = Rgb.FromHex(Colors[Mod(step, Colors.Count)]);
        var fadeStart = 1 - Rgb.Clamp01(FadeRatio);
        if (within <= fadeStart || FadeRatio <= 0)
            return new(current, Intensity);
        var next = Rgb.FromHex(Colors[Mod(step + 1, Colors.Count)]);
        return new(Rgb.Lerp(current, next, (within - fadeStart) / (1 - fadeStart)), Intensity);
    }

    protected override object MemberwiseCloneDeep()
    {
        var copy = (ColorCycleEffect)MemberwiseClone();
        copy.Colors = [.. Colors];
        return copy;
    }

    internal static int Mod(int a, int n) => ((a % n) + n) % n;
}

public sealed class ChaseEffect : Effect
{
    public List<string> Colors { get; set; } = ["#FFFFFF"];

    /// <summary>Seconds the head spends on each fixture.</summary>
    public double StepTime { get; set; } = 0.25;

    /// <summary>Length of the fading tail, in fixtures.</summary>
    public double Tail { get; set; } = 1;

    public ChaseDirection Direction { get; set; } = ChaseDirection.Forward;

    /// <summary>Intensity of fixtures the chase is not on.</summary>
    public double Background { get; set; }

    public override string DisplayName => "Chase";
    public override string SwatchColor => Colors.FirstOrDefault() ?? "#FFFFFF";

    public override EffectSample Sample(in EffectContext ctx)
    {
        var count = Math.Max(1, ctx.Count);
        var position = ctx.Time / Math.Max(0.01, StepTime);
        var lap = (int)Math.Floor(position / count);
        var head = Direction switch
        {
            ChaseDirection.Backward => count - 1 - Frac(position / count) * count,
            ChaseDirection.Bounce when count > 1 => Bounce(position, count),
            _ => Frac(position / count) * count,
        };
        // Distance behind the head, wrapping so the tail runs off one end and back in the other.
        var behind = Direction == ChaseDirection.Backward ? ctx.Index - head : head - ctx.Index;
        if (Direction != ChaseDirection.Bounce)
            behind = ((behind % count) + count) % count;
        else
            behind = Math.Abs(behind);
        var level = behind < 1 ? 1 : Tail <= 0 ? 0 : Math.Max(0, 1 - (behind - 1) / Tail);
        level = Math.Max(level, Background);
        var colors = Colors.Count == 0 ? ["#FFFFFF"] : Colors;
        var color = Rgb.FromHex(colors[ColorCycleEffect.Mod(lap, colors.Count)]);
        return new(color, Intensity * level);
    }

    private static double Bounce(double position, int count)
    {
        var period = 2 * (count - 1);
        var p = position % period;
        return p <= count - 1 ? p : period - p;
    }

    protected override object MemberwiseCloneDeep()
    {
        var copy = (ChaseEffect)MemberwiseClone();
        copy.Colors = [.. Colors];
        return copy;
    }
}

public sealed class StrobeEffect : Effect
{
    public string Color { get; set; } = "#FFFFFF";

    /// <summary>Flashes per second.</summary>
    public double Rate { get; set; } = 8;

    /// <summary>Fraction of each period the light is on.</summary>
    public double Duty { get; set; } = 0.3;

    public override string DisplayName => "Strobe";
    public override string SwatchColor => Color;

    public override EffectSample Sample(in EffectContext ctx)
    {
        var on = Frac(ctx.Time * Math.Max(0.01, Rate)) < Duty;
        return new(Rgb.FromHex(Color), on ? Intensity : 0);
    }
}

public sealed class PulseEffect : Effect
{
    public string Color { get; set; } = "#FF2A6D";

    /// <summary>Pulses per second.</summary>
    public double Rate { get; set; } = 1;

    public double Minimum { get; set; }
    public Waveform Wave { get; set; } = Waveform.Sine;

    /// <summary>Phase offset between neighbouring fixtures, in cycles.</summary>
    public double Phase { get; set; }

    public override string DisplayName => "Pulse";
    public override string SwatchColor => Color;

    public override EffectSample Sample(in EffectContext ctx)
    {
        var p = Frac(ctx.Time * Rate + Phase * ctx.Index);
        var shape = Wave switch
        {
            Waveform.Triangle => 1 - Math.Abs(2 * p - 1),
            Waveform.Saw => 1 - p,
            Waveform.Square => p < 0.5 ? 1 : 0,
            _ => 0.5 - 0.5 * Math.Cos(2 * Math.PI * p),
        };
        return new(Rgb.FromHex(Color), Intensity * (Minimum + (1 - Minimum) * shape));
    }
}

public sealed class SparkleEffect : Effect
{
    public string Color { get; set; } = "#FFFFFF";

    /// <summary>Twinkles per fixture per second.</summary>
    public double Rate { get; set; } = 2;

    /// <summary>Seconds a twinkle takes to fade out.</summary>
    public double Decay { get; set; } = 0.3;

    public int Seed { get; set; } = 7;

    public override string DisplayName => "Sparkle";
    public override string SwatchColor => Color;

    public override EffectSample Sample(in EffectContext ctx)
    {
        // Each fixture's timeline is split into 1/Rate slots with one twinkle at a hashed instant per slot,
        // so the result depends only on time — scrubbing and export see exactly what playback does.
        var slotLength = 1 / Math.Max(0.05, Rate);
        var slot = (int)Math.Floor(ctx.Time / slotLength);
        var level = 0.0;
        for (var s = slot - 2; s <= slot; s++)
        {
            var flashAt = (s + Hash(s, ctx.Index, Seed)) * slotLength;
            var age = ctx.Time - flashAt;
            if (age >= 0)
                level = Math.Max(level, Math.Max(0, 1 - age / Math.Max(0.01, Decay)));
        }
        return new(Rgb.FromHex(Color), Intensity * level);
    }

    private static double Hash(int slot, int index, int seed)
    {
        unchecked
        {
            var h = (uint)(slot * 73856093) ^ (uint)(index * 19349663) ^ (uint)(seed * 83492791);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (h & 0xFFFFFF) / (double)0x1000000;
        }
    }
}
