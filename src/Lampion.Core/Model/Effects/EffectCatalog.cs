namespace Lampion.Core.Model.Effects;

/// <summary>The stock effects, in library order, and a factory for them by kind.</summary>
public static class EffectCatalog
{
    /// <summary>Every stock effect, in the order the library lists them.</summary>
    public static IReadOnlyList<EffectCatalogEntry> Entries { get; } =
    [
        new("solid", "Solid Color", "One static colour"),
        new("fade", "Color Fade", "Blend from one colour to another across the clip"),
        new("rainbow", "Rainbow", "Hue rotation, optionally spread across fixtures"),
        new("cycle", "Color Cycle", "Step through a palette, crossfading"),
        new("chase", "Chase", "A light running across the fixtures"),
        new("strobe", "Strobe", "Hard on/off flashes"),
        new("pulse", "Pulse", "Breathing intensity wave"),
        new("sparkle", "Sparkle", "Random twinkles"),
        new("gradient", "Gradient", "A palette laid across the fixtures, optionally scrolling"),
        new("wave", "Color Wave", "Two colours rolling across the fixtures in a smooth wave"),
        new("fire", "Fire", "Flickering embers and flames"),
        new("lightning", "Lightning", "Irregular bursts of bright flashes"),
        new("heartbeat", "Heartbeat", "A lub-dub double pulse"),
        new("hit", "Hit", "A sharp flash that decays, once or on repeat"),
    ];

    /// <summary>A new effect with default parameters; unknown kinds give a <see cref="SolidEffect"/>.</summary>
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

    /// <summary>The catalog kind of <paramref name="effect"/>, matched by display name.</summary>
    /// <exception cref="InvalidOperationException">No catalog entry has the effect's display name.</exception>
    public static string KindOf(Effect effect)
    {
        var name = effect.DisplayName;
        for (var i = 0; i < Entries.Count; i++)
            if (Entries[i].Name == name)
                return Entries[i].Kind;
        throw new InvalidOperationException($"\"{name}\" is not in the effect catalog.");
    }
}
