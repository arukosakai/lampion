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
[JsonDerivedType(typeof(GradientEffect), "gradient")]
[JsonDerivedType(typeof(ColorWaveEffect), "wave")]
[JsonDerivedType(typeof(FireEffect), "fire")]
[JsonDerivedType(typeof(LightningEffect), "lightning")]
[JsonDerivedType(typeof(HeartbeatEffect), "heartbeat")]
[JsonDerivedType(typeof(HitEffect), "hit")]
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

    /// <summary>One cycle of a waveform at phase 0..1, peaking at 1.</summary>
    protected static double Shape(Waveform wave, double phase) =>
        wave switch
        {
            Waveform.Triangle => 1 - Math.Abs(2 * phase - 1),
            Waveform.Saw => 1 - phase,
            Waveform.Square => phase < 0.5 ? 1 : 0,
            _ => 0.5 - 0.5 * Math.Cos(2 * Math.PI * phase),
        };

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
            "gradient" => new GradientEffect(),
            "wave" => new ColorWaveEffect(),
            "fire" => new FireEffect(),
            "lightning" => new LightningEffect(),
            "heartbeat" => new HeartbeatEffect(),
            "hit" => new HitEffect(),
            _ => new SolidEffect(),
        };

    /// <summary>The catalog kind for an effect instance, e.g. "rainbow".</summary>
    [JsonIgnore]
    public string Kind => Catalog.First(c => c.Name == DisplayName).Kind;

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
        ("gradient", "Gradient", "A palette laid across the fixtures, optionally scrolling"),
        ("wave", "Color Wave", "Two colours rolling across the fixtures in a smooth wave"),
        ("fire", "Fire", "Flickering embers and flames"),
        ("lightning", "Lightning", "Irregular bursts of bright flashes"),
        ("heartbeat", "Heartbeat", "A lub-dub double pulse"),
        ("hit", "Hit", "A sharp flash that decays, once or on repeat"),
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
        var shape = Shape(Wave, Frac(ctx.Time * Rate + Phase * ctx.Index));
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

    /// <summary>A repeatable pseudo-random number in 0..1, so random-looking effects scrub and export identically.</summary>
    internal static double Hash(int slot, int index, int seed)
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

public sealed class GradientEffect : Effect
{
    public List<string> Colors { get; set; } = ["#FF2A6D", "#05D9E8"];

    /// <summary>Times the palette repeats across the fixtures; it wraps, so the last colour blends back into the first.</summary>
    public double Repeat { get; set; } = 1;

    /// <summary>Palette lengths the gradient scrolls per second; 0 holds it still.</summary>
    public double Speed { get; set; }

    public override string DisplayName => "Gradient";
    public override string SwatchColor => Colors.FirstOrDefault() ?? "#FFFFFF";

    public override EffectSample Sample(in EffectContext ctx)
    {
        if (Colors.Count == 0)
            return new(Rgb.Black, 0);
        var across = ctx.Count <= 1 ? 0 : ctx.Index / (double)ctx.Count;
        var position = Frac(across * Repeat + ctx.Time * Speed) * Colors.Count;
        var step = (int)Math.Floor(position);
        var from = Rgb.FromHex(Colors[step % Colors.Count]);
        var to = Rgb.FromHex(Colors[(step + 1) % Colors.Count]);
        return new(Rgb.Lerp(from, to, position - step), Intensity);
    }

    protected override object MemberwiseCloneDeep()
    {
        var copy = (GradientEffect)MemberwiseClone();
        copy.Colors = [.. Colors];
        return copy;
    }
}

public sealed class ColorWaveEffect : Effect
{
    /// <summary>Colour at the wave's crest.</summary>
    public string Color { get; set; } = "#00E5FF";

    /// <summary>Colour in the wave's trough.</summary>
    public string Base { get; set; } = "#3A00FF";

    /// <summary>Crests passing each fixture per second.</summary>
    public double Rate { get; set; } = 0.5;

    /// <summary>Fixtures from one crest to the next.</summary>
    public double Wavelength { get; set; } = 4;

    public Waveform Wave { get; set; } = Waveform.Sine;

    public override string DisplayName => "Color Wave";
    public override string SwatchColor => Color;

    public override EffectSample Sample(in EffectContext ctx)
    {
        var shape = Shape(Wave, Frac(ctx.Time * Rate - ctx.Index / Math.Max(0.1, Wavelength)));
        return new(Rgb.Lerp(Rgb.FromHex(Base), Rgb.FromHex(Color), shape), Intensity);
    }
}

public sealed class FireEffect : Effect
{
    public string Ember { get; set; } = "#B31200";
    public string Flame { get; set; } = "#FFB238";

    /// <summary>Flickers per second.</summary>
    public double Rate { get; set; } = 8;

    /// <summary>Brightness the embers never drop below.</summary>
    public double Minimum { get; set; } = 0.35;

    public int Seed { get; set; } = 3;

    public override string DisplayName => "Fire";
    public override string SwatchColor => Flame;

    public override EffectSample Sample(in EffectContext ctx)
    {
        var x = ctx.Time * Math.Max(0.05, Rate);
        var heat = 0.65 * Noise(x, ctx.Index, Seed) + 0.35 * Noise(x * 2.3, ctx.Index, Seed + 1);
        return new(Rgb.Lerp(Rgb.FromHex(Ember), Rgb.FromHex(Flame), heat), Intensity * (Minimum + (1 - Minimum) * heat));
    }

    /// <summary>Smoothly interpolated value noise, 0..1, built on the same repeatable hash as Sparkle.</summary>
    private static double Noise(double x, int index, int seed)
    {
        var i = (int)Math.Floor(x);
        var f = x - i;
        var s = f * f * (3 - 2 * f);
        var a = SparkleEffect.Hash(i, index, seed);
        var b = SparkleEffect.Hash(i + 1, index, seed);
        return a + (b - a) * s;
    }
}

[JsonConverter(typeof(JsonStringEnumConverter<LightningMode>))]
public enum LightningMode
{
    Together,
    Scattered,
}

public sealed class LightningEffect : Effect
{
    public string Color { get; set; } = "#E8F0FF";

    /// <summary>Strikes per second, on average.</summary>
    public double Rate { get; set; } = 0.5;

    /// <summary>Seconds each flash in a strike takes to fade.</summary>
    public double Decay { get; set; } = 0.12;

    /// <summary>Whether every fixture strikes at once or each on its own.</summary>
    public LightningMode Mode { get; set; } = LightningMode.Together;

    public int Seed { get; set; } = 11;

    public override string DisplayName => "Lightning";
    public override string SwatchColor => Color;

    public override EffectSample Sample(in EffectContext ctx)
    {
        // One strike per 1/Rate slot at a hashed instant; each strike is 1–3 quick flashes, the later ones dimmer.
        var slotLength = 1 / Math.Max(0.02, Rate);
        var key = Mode == LightningMode.Together ? 0 : ctx.Index;
        var slot = (int)Math.Floor(ctx.Time / slotLength);
        var level = 0.0;
        for (var s = slot - 1; s <= slot; s++)
        {
            var at = (s + 0.7 * SparkleEffect.Hash(s, key, Seed)) * slotLength;
            var flashes = 1 + (int)(SparkleEffect.Hash(s, key, Seed + 1) * 3);
            for (var k = 0; k < flashes; k++)
            {
                var age = ctx.Time - at;
                var brightness = k == 0 ? 1 : 0.4 + 0.6 * SparkleEffect.Hash(s, key, Seed + 2 + k);
                if (age >= 0)
                    level = Math.Max(level, brightness * Math.Max(0, 1 - age / Math.Max(0.01, Decay)));
                at += 0.06 + 0.1 * SparkleEffect.Hash(s, key, Seed + 5 + k);
            }
        }
        return new(Rgb.FromHex(Color), Intensity * level);
    }
}

public sealed class HeartbeatEffect : Effect
{
    public string Color { get; set; } = "#FF1744";

    /// <summary>Beats per second (1.2 ≈ 72 bpm).</summary>
    public double Rate { get; set; } = 1.2;

    public double Minimum { get; set; } = 0.05;

    /// <summary>Phase offset between neighbouring fixtures, in cycles.</summary>
    public double Phase { get; set; }

    public override string DisplayName => "Heartbeat";
    public override string SwatchColor => Color;

    public override EffectSample Sample(in EffectContext ctx)
    {
        var p = Frac(ctx.Time * Rate + Phase * ctx.Index);
        var shape = Math.Max(Beat(p, 0.08, 0.05), 0.6 * Beat(p, 0.3, 0.06));
        return new(Rgb.FromHex(Color), Intensity * (Minimum + (1 - Minimum) * shape));
    }

    private static double Beat(double phase, double center, double width)
    {
        var d = Math.Abs(phase - center);
        d = Math.Min(d, 1 - d);
        return Math.Exp(-(d / width) * (d / width));
    }
}

public sealed class HitEffect : Effect
{
    public string Color { get; set; } = "#FFFFFF";

    /// <summary>Seconds a hit takes to fade out.</summary>
    public double Decay { get; set; } = 0.4;

    /// <summary>Seconds between hits; 0 fires once at the start of the clip.</summary>
    public double Interval { get; set; }

    /// <summary>Seconds each fixture's hit lands after the previous one's, for a rolling hit.</summary>
    public double Offset { get; set; }

    public override string DisplayName => "Hit";
    public override string SwatchColor => Color;

    public override EffectSample Sample(in EffectContext ctx)
    {
        var age = ctx.Time - Offset * ctx.Index;
        if (age < 0)
            return new(Rgb.FromHex(Color), 0);
        if (Interval > 0.01)
            age %= Interval;
        var fall = Math.Max(0, 1 - age / Math.Max(0.01, Decay));
        return new(Rgb.FromHex(Color), Intensity * fall * fall);
    }
}
