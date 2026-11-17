namespace Lampion.Core.Model.Effects;

/// <summary>
/// Keyframed values for an effect's <see cref="Effect.AnimatableParameters"/>, by slot (the parameter's index),
/// handed to <see cref="Effect.Sample"/> through the context so effects never get mutated while sampling.
/// </summary>
internal partial struct EffectParameterValues : IEquatable<EffectParameterValues>
{
    /// <summary>The most animatable parameters one effect type may declare.</summary>
    public const int Capacity = 8;

    private Buffer _values;
    private int _set;

    /// <summary>Overrides slot <paramref name="slot"/>; slots outside 0..<see cref="Capacity"/> are ignored.</summary>
    public void Set(int slot, double value)
    {
        if ((uint)slot >= Capacity)
            return;
        _values[slot] = value;
        _set |= 1 << slot;
    }

    /// <summary>The override for <paramref name="slot"/>, or <paramref name="value"/> when it has none.</summary>
    public readonly double Or(int slot, double value) => (uint)slot < Capacity && (_set & (1 << slot)) != 0 ? _values[slot] : value;

    /// <summary>Same slots overridden with the same values.</summary>
    public readonly bool Equals(EffectParameterValues other)
    {
        if (_set != other._set)
            return false;
        for (var i = 0; i < Capacity; i++)
            if ((_set & (1 << i)) != 0 && !_values[i].Equals(other._values[i]))
                return false;
        return true;
    }

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is EffectParameterValues other && Equals(other);

    /// <inheritdoc />
    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_set);
        for (var i = 0; i < Capacity; i++)
            if ((_set & (1 << i)) != 0)
                hash.Add(_values[i]);
        return hash.ToHashCode();
    }
}
