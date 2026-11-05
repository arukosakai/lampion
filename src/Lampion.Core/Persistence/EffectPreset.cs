using System.Text.Json;
using Lampion.Core.Model;

namespace Lampion.Core.Persistence;

/// <summary>
/// A saved look: an effect with its parameters plus the clip's fades and automation lanes, so one
/// configured clip can be dropped into any show. Keyframe times are clip-relative, so the preset keeps
/// the length it was saved at and new clips are made that long.
/// </summary>
public sealed class EffectPreset
{
    public const string Extension = ".lampfx";

    public int FormatVersion { get; set; } = 1;
    public string Name { get; set; } = "Preset";
    public string Description { get; set; } = "";
    public Effect Effect { get; set; } = new SolidEffect();
    public double Duration { get; set; } = 4;
    public double FadeIn { get; set; }
    public double FadeOut { get; set; }
    public Easing FadeEasing { get; set; } = Easing.Linear;
    public List<AutomationLane> Automation { get; set; } = [];

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowOutOfOrderMetadataProperties = true,
    };

    public static EffectPreset From(LightClip clip, string name, string description = "") =>
        new()
        {
            Name = name,
            Description = description,
            Effect = clip.Effect.Clone(),
            Duration = clip.Duration,
            FadeIn = clip.FadeIn,
            FadeOut = clip.FadeOut,
            FadeEasing = clip.FadeEasing,
            Automation = clip.Automation.Select(a => a.Clone()).ToList(),
        };

    /// <summary>A fresh clip with this look; nothing is shared with the preset, so editing one never touches the other.</summary>
    public LightClip CreateClip(double start) =>
        new()
        {
            Name = Name,
            Start = Math.Max(0, start),
            Duration = Math.Max(0.05, Duration),
            Effect = Effect.Clone(),
            FadeIn = FadeIn,
            FadeOut = FadeOut,
            FadeEasing = FadeEasing,
            Automation = Automation.Select(a => a.Clone()).ToList(),
        };

    /// <summary>Gives an existing clip this look, keeping its place and length.</summary>
    public void ApplyTo(LightClip clip)
    {
        clip.Effect = Effect.Clone();
        clip.FadeIn = Math.Min(FadeIn, clip.Duration);
        clip.FadeOut = Math.Min(FadeOut, clip.Duration);
        clip.FadeEasing = FadeEasing;
        clip.Automation = Automation.Select(a => a.Clone()).ToList();
        clip.EffectOffset = 0;
        clip.EffectSpan = null;
    }

    public static string Serialize(EffectPreset preset) => JsonSerializer.Serialize(preset, Options);

    /// <exception cref="InvalidDataException">The JSON is empty or not a preset.</exception>
    public static EffectPreset Deserialize(string json)
    {
        var preset = JsonSerializer.Deserialize<EffectPreset>(json, Options) ?? throw new InvalidDataException("The preset file is empty.");
        if (preset.Effect is null)
            throw new InvalidDataException("The preset has no effect.");
        preset.Automation ??= [];
        foreach (var lane in preset.Automation)
            lane.SortKeys();
        return preset;
    }

    public void Save(string path)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, Serialize(this));
        File.Move(temp, path, overwrite: true);
    }

    public static EffectPreset Load(string path) => Deserialize(File.ReadAllText(path));
}

/// <summary>Looks that ship with Lampion, built from the stock effects.</summary>
public static class BuiltInPresets
{
    public static IReadOnlyList<EffectPreset> All() =>
    [
        new()
        {
            Name = "Police",
            Description = "Red and blue swapping between neighbouring fixtures",
            Duration = 8,
            Effect = new ColorCycleEffect { Colors = ["#FF0010", "#0030FF"], StepTime = 0.25, FadeRatio = 0, FixtureOffset = 1 },
        },
        new()
        {
            Name = "Candlelight",
            Description = "Low, warm, gently flickering",
            Duration = 16,
            FadeIn = 2,
            FadeOut = 2,
            Effect = new FireEffect { Ember = "#7A2400", Flame = "#FF9A3C", Rate = 3, Minimum = 0.55, Intensity = 0.6 },
        },
        new()
        {
            Name = "Ocean",
            Description = "Slow teal and deep-blue swell",
            Duration = 16,
            FadeIn = 3,
            FadeOut = 3,
            FadeEasing = Easing.EaseInOut,
            Effect = new ColorWaveEffect { Color = "#1DE9B6", Base = "#0A1A8C", Rate = 0.15, Wavelength = 6 },
        },
        new()
        {
            Name = "Sunset",
            Description = "Amber to magenta gradient drifting across the rig",
            Duration = 16,
            Effect = new GradientEffect { Colors = ["#FF8C1A", "#FF2A6D", "#7B1FA2"], Speed = 0.05 },
        },
        new()
        {
            Name = "Storm",
            Description = "Scattered lightning strikes, each fixture on its own",
            Duration = 16,
            Effect = new LightningEffect { Color = "#DDE6FF", Rate = 0.8, Mode = LightningMode.Scattered },
        },
        new()
        {
            Name = "Rainbow sweep",
            Description = "Solid red with the hue shift keyframed a full turn round the wheel",
            Duration = 8,
            Effect = new SolidEffect { Color = "#FF0000" },
            Automation =
            [
                new AutomationLane
                {
                    Target = AutomationTarget.HueShift,
                    Animated = true,
                    Keys = [new Keyframe { Time = 0, Value = 0 }, new Keyframe { Time = 8, Value = 1 }],
                },
            ],
        },
    ];
}
