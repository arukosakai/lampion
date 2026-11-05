namespace Lampion.Core.Engine;

/// <summary>
/// Each fixture's channel values at one instant, before they are placed in universes, indexed like
/// <see cref="Model.Project.Fixtures"/>. Reuses one buffer across renders; spans handed out are valid until
/// the next render into this frame. Not thread-safe.
/// </summary>
public sealed class FixtureFrame
{
    private byte[] _data = [];
    private int[] _offsets = [];
    private int[] _lengths = [];
    private int _used;

    /// <summary>Number of fixtures in the frame.</summary>
    public int FixtureCount { get; private set; }

    /// <summary>Whether the fixture was rendered, i.e. it has a profile.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fixtureIndex"/> is not below <see cref="FixtureCount"/>.</exception>
    public bool HasChannels(int fixtureIndex) => _offsets[Check(fixtureIndex)] >= 0;

    /// <summary>The fixture's footprint in profile order; empty when it has no profile.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fixtureIndex"/> is not below <see cref="FixtureCount"/>.</exception>
    public ReadOnlySpan<byte> this[int fixtureIndex] =>
        _offsets[Check(fixtureIndex)] is var offset and >= 0 ? _data.AsSpan(offset, _lengths[fixtureIndex]) : ReadOnlySpan<byte>.Empty;

    /// <summary>Starts a new render of <paramref name="fixtureCount"/> fixtures, all without channels.</summary>
    internal void Reset(int fixtureCount)
    {
        if (_offsets.Length < fixtureCount)
        {
            var length = Math.Max(fixtureCount, _offsets.Length * 2);
            _offsets = new int[length];
            _lengths = new int[length];
        }
        _offsets.AsSpan(0, fixtureCount).Fill(-1);
        _used = 0;
        FixtureCount = fixtureCount;
    }

    /// <summary>Room for the fixture's <paramref name="length"/> channels; valid until the next call.</summary>
    internal Span<byte> Reserve(int fixtureIndex, int length)
    {
        if (_used + length > _data.Length)
            Array.Resize(ref _data, Math.Max(_used + length, _data.Length * 2));
        _offsets[fixtureIndex] = _used;
        _lengths[fixtureIndex] = length;
        var span = _data.AsSpan(_used, length);
        _used += length;
        return span;
    }

    private int Check(int fixtureIndex) =>
        (uint)fixtureIndex < (uint)FixtureCount ? fixtureIndex : throw new ArgumentOutOfRangeException(nameof(fixtureIndex));
}
