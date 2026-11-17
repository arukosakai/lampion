namespace Lampion.Core.Model.Effects;

/// <summary>
/// A numeric effect parameter that automation lanes can keyframe (an <see cref="Animation.AutomationTarget.EffectParameter"/>
/// lane names it by <see cref="Key"/>). Descriptors are immutable and shared by every instance of the effect type.
/// </summary>
public sealed class AnimatableParameter
{
    private readonly Func<Effect, double> _get;
    private readonly Action<Effect, double> _set;

    private AnimatableParameter(string key, string label, ParameterTiming timing, double minimum, Func<Effect, double> get, Action<Effect, double> set)
    {
        Key = key;
        Label = label;
        Timing = timing;
        Minimum = minimum;
        _get = get;
        _set = set;
    }

    /// <summary>Stable name saved in project files, e.g. "rate" or "stepTime".</summary>
    public string Key { get; }

    /// <summary>Display name, e.g. "Step time".</summary>
    public string Label { get; }

    /// <summary>How keyframed values are applied.</summary>
    public ParameterTiming Timing { get; }

    /// <summary>Keyframed values below this are raised to it, matching the clamp the effect applies to the property.</summary>
    public double Minimum { get; }

    /// <summary>The effect's own (static) value.</summary>
    /// <exception cref="InvalidCastException"><paramref name="effect"/> is not the type that declares the parameter.</exception>
    public double Get(Effect effect) => _get(effect);

    /// <summary>Writes the effect's own (static) value.</summary>
    /// <exception cref="InvalidCastException"><paramref name="effect"/> is not the type that declares the parameter.</exception>
    public void Set(Effect effect, double value) => _set(effect, value);

    /// <summary>The display name of the parameter saved as <paramref name="key"/> on any stock effect, or the key itself if none has it.</summary>
    public static string LabelFor(string? key)
    {
        if (key is null)
            return "Effect parameter";
        foreach (var entry in EffectCatalog.Entries)
            foreach (var parameter in EffectCatalog.Create(entry.Kind).AnimatableParameters)
                if (parameter.Key == key)
                    return parameter.Label;
        return key;
    }

    internal static AnimatableParameter Plain<T>(string key, string label, Func<T, double> get, Action<T, double> set, double minimum = double.NegativeInfinity)
        where T : Effect => new(key, label, ParameterTiming.Plain, minimum, e => get((T)e), (e, v) => set((T)e, v));

    internal static AnimatableParameter Frequency<T>(string key, string label, Func<T, double> get, Action<T, double> set, double minimum = double.NegativeInfinity)
        where T : Effect => new(key, label, ParameterTiming.Frequency, minimum, e => get((T)e), (e, v) => set((T)e, v));

    // The minimum must be positive: the clock divides by the value.
    internal static AnimatableParameter Period<T>(string key, string label, Func<T, double> get, Action<T, double> set, double minimum)
        where T : Effect => new(key, label, ParameterTiming.Period, minimum, e => get((T)e), (e, v) => set((T)e, v));
}
