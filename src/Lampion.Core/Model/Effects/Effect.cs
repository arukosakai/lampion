using System.Text.Json.Serialization;

namespace Lampion.Core.Model.Effects;

/// <summary>
/// A look a light clip plays. Implementations are pure: <see cref="Sample"/> depends only on the parameters
/// and the context, so scrubbing, playback and QLC+ export see exactly the same light. Sampling is
/// allocation-free and safe to call from several threads as long as nobody edits the parameters meanwhile.
/// Saved with a <c>type</c> discriminator; the derived classes' property names are part of the file format.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SolidEffect), "solid")]
[JsonDerivedType(typeof(ColorFadeEffect), "fade")]
[JsonDerivedType(typeof(RainbowEffect), "rainbow")]
[JsonDerivedType(typeof(ColorCycleEffect), "cycle")]
[JsonDerivedType(typeof(ChaseEffect), "chase")]
[JsonDerivedType(typeof(StrobeEffect), "strobe")]
[JsonDerivedType(typeof(PulseEffect), "pulse")]
[JsonDerivedType(typeof(SparkleEffect), "sparkle")]
[JsonDerivedType(typeof(EvenSparkleEffect), "even-sparkle")]
[JsonDerivedType(typeof(GradientEffect), "gradient")]
[JsonDerivedType(typeof(ColorWaveEffect), "wave")]
[JsonDerivedType(typeof(FireEffect), "fire")]
[JsonDerivedType(typeof(LightningEffect), "lightning")]
[JsonDerivedType(typeof(HeartbeatEffect), "heartbeat")]
[JsonDerivedType(typeof(HitEffect), "hit")]
public abstract class Effect
{
    private protected Effect() { }

    /// <summary>Overall brightness multiplier, normally 0..1.</summary>
    public double Intensity { get; set; } = 1;

    /// <summary>Catalog name, e.g. "Color Fade". The overrides are written to project files.</summary>
    [JsonIgnore]
    public abstract string DisplayName { get; }

    /// <summary>A representative colour (<c>#RRGGBB</c>) for drawing the clip on the timeline. The overrides are written to project files.</summary>
    [JsonIgnore]
    public abstract string SwatchColor { get; }

    /// <summary>The catalog kind for this effect, e.g. "rainbow".</summary>
    /// <exception cref="InvalidOperationException">The effect's <see cref="DisplayName"/> is not in the catalog.</exception>
    [JsonIgnore]
    public string Kind => EffectCatalog.KindOf(this);

    /// <summary>The numeric parameters automation can keyframe, in slot order; shared, never mutated. Colours, seeds and choices aren't animatable.</summary>
    [JsonIgnore]
    public IReadOnlyList<AnimatableParameter> AnimatableParameters => DeclaredParameters;

    /// <summary>The animatable parameter saved as <paramref name="key"/>, or null if this effect has none by that name.</summary>
    public AnimatableParameter? FindParameter(string? key)
    {
        var slot = SlotOf(key);
        return slot < 0 ? null : AnimatableParameters[slot];
    }

    /// <summary>The slot of the animatable parameter saved as <paramref name="key"/>, or -1. Allocation-free.</summary>
    internal int SlotOf(string? key)
    {
        var parameters = AnimatableParameters;
        for (var i = 0; i < parameters.Count; i++)
            if (string.Equals(parameters[i].Key, key, StringComparison.Ordinal))
                return i;
        return -1;
    }

    // Not virtual on the public property: JSON would write an override even though the base is ignored.
    private protected virtual IReadOnlyList<AnimatableParameter> DeclaredParameters => [];

    /// <summary>The colour and intensity for one fixture at one instant. Pure and allocation-free.</summary>
    public abstract EffectSample Sample(in EffectContext ctx);

    /// <summary>A deep copy; lists (palettes) are copied, so editing one never touches the other.</summary>
    public Effect Clone() => CloneCore();

    /// <summary>Copies the effect; overrides copy their collections too.</summary>
    protected virtual Effect CloneCore() => (Effect)MemberwiseClone();
}
