using System.Text.Json;
using System.Text.Json.Serialization;
using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Timeline;

namespace Lampion.Core.Persistence;

/// <summary>
/// A saved look (<c>.lampfx</c>): an effect with its parameters plus the clip's fades and automation lanes,
/// so one configured clip can be dropped into any show. Keyframe times are clip-relative, so the preset
/// keeps the length it was saved at and new clips are made that long.
/// </summary>
public sealed class EffectPreset
{
    /// <summary>File extension, including the dot.</summary>
    public const string Extension = ".lampfx";

    /// <summary>File format version.</summary>
    public int FormatVersion { get; set; } = 1;

    /// <summary>Name shown in the library; also the name of clips made from it.</summary>
    public string Name { get; set; } = "Preset";

    /// <summary>One-line description for the library.</summary>
    public string Description { get; set; } = "";

    /// <summary>The effect and its parameters.</summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Replace)]
    public Effect Effect { get; set; } = new SolidEffect();

    /// <summary>Clip length in seconds the keyframes were laid out for.</summary>
    public double Duration { get; set; } = 4;

    /// <summary>Fade-in in seconds.</summary>
    public double FadeIn { get; set; }

    /// <summary>Fade-out in seconds.</summary>
    public double FadeOut { get; set; }

    /// <summary>Curve of both fades.</summary>
    public Easing FadeEasing { get; set; } = Easing.Linear;

    /// <summary>Automation lanes, with clip-relative key times.</summary>
    public List<AutomationLane> Automation { get; } = [];

    /// <summary>A preset from a clip's look; nothing is shared with the clip.</summary>
    public static EffectPreset From(LightClip clip, string name, string description = "")
    {
        var preset = new EffectPreset
        {
            Name = name,
            Description = description,
            Effect = clip.Effect.Clone(),
            Duration = clip.Duration,
            FadeIn = clip.FadeIn,
            FadeOut = clip.FadeOut,
            FadeEasing = clip.FadeEasing,
        };
        preset.Automation.AddRange(clip.Automation.Select(a => a.Clone()));
        return preset;
    }

    /// <summary>A fresh clip with this look; nothing is shared with the preset, so editing one never touches the other.</summary>
    /// <param name="start">Seconds on the timeline; negative values start at 0.</param>
    public LightClip CreateClip(double start)
    {
        var clip = new LightClip
        {
            Name = Name,
            Start = Math.Max(0, start),
            Duration = Math.Max(0.05, Duration),
            Effect = Effect.Clone(),
            FadeIn = FadeIn,
            FadeOut = FadeOut,
            FadeEasing = FadeEasing,
        };
        clip.Automation.AddRange(Automation.Select(a => a.Clone()));
        return clip;
    }

    /// <summary>Gives an existing clip this look, keeping its place and length (fades are shortened to fit).</summary>
    public void ApplyTo(LightClip clip)
    {
        clip.Effect = Effect.Clone();
        clip.FadeIn = Math.Min(FadeIn, clip.Duration);
        clip.FadeOut = Math.Min(FadeOut, clip.Duration);
        clip.FadeEasing = FadeEasing;
        clip.Automation.Clear();
        clip.Automation.AddRange(Automation.Select(a => a.Clone()));
        clip.EffectOffset = 0;
        clip.EffectSpan = null;
    }

    /// <summary>The preset as indented JSON.</summary>
    public static string Serialize(EffectPreset preset) => JsonSerializer.Serialize(preset, LampionJson.Indented);

    /// <summary>A preset from JSON, with lane keys sorted.</summary>
    /// <exception cref="JsonException">The text is not a preset.</exception>
    /// <exception cref="InvalidDataException">The JSON is empty, damaged or has no effect.</exception>
    public static EffectPreset Deserialize(string json)
    {
        var preset = LampionJson.Read<EffectPreset>(json, "The preset file is empty.");
        if (preset.Effect is null)
            throw new InvalidDataException("The preset has no effect.");
        foreach (var lane in preset.Automation)
            lane.SortKeys();
        return preset;
    }

    /// <summary>Writes the preset to <paramref name="path"/> via a temp file, replacing any existing file.</summary>
    public void Save(string path)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, Serialize(this));
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>Reads a preset file.</summary>
    /// <exception cref="IOException">The file can't be read.</exception>
    /// <exception cref="JsonException">The file is not a preset.</exception>
    /// <exception cref="InvalidDataException">The file is empty, damaged or has no effect.</exception>
    public static EffectPreset Load(string path) => Deserialize(File.ReadAllText(path));
}
