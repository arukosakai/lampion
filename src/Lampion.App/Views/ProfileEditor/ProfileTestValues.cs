using System.Runtime.InteropServices;

namespace Lampion.App.Views.ProfileEditor;

/// <summary>
/// The profile editor's test DMX values, one byte per channel of the profile being edited, in profile order.
/// They drive the preview, the tester and optionally the rig, and are never saved.
/// </summary>
internal sealed class ProfileTestValues
{
    private readonly List<byte> _values = [];

    /// <summary>Number of values; kept equal to the working profile's channel count by the session.</summary>
    public int Count => _values.Count;

    /// <summary>The value of channel <paramref name="index"/> (0-based), DMX 0–255.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside 0..<see cref="Count"/>-1.</exception>
    public byte this[int index]
    {
        get => _values[index];
        set => _values[index] = value;
    }

    /// <summary>
    /// The values in place, for reading or writing without copying. Only valid until the next
    /// <see cref="Add"/>, <see cref="RemoveAt"/> or <see cref="Clear"/>.
    /// </summary>
    public Span<byte> Span => CollectionsMarshal.AsSpan(_values);

    /// <summary>Appends a value for a newly added channel.</summary>
    public void Add(byte value) => _values.Add(value);

    /// <summary>Drops the value of a removed channel.</summary>
    public void RemoveAt(int index) => _values.RemoveAt(index);

    /// <summary>Removes every value.</summary>
    public void Clear() => _values.Clear();

    /// <summary>Exchanges two values, following a channel move.</summary>
    public void Swap(int a, int b) => (_values[a], _values[b]) = (_values[b], _values[a]);

    /// <summary>A copy the caller owns, for handing to code that keeps it.</summary>
    public byte[] ToArray() => _values.ToArray();
}
