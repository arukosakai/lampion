using Lampion.Core.Model.Rig;

namespace Lampion.Core.Engine;

/// <summary>
/// Full DMX output at one instant: 512 channels per used universe, kept sorted by (1-based) universe number.
/// Clearing keeps the buffers for reuse, so a frame that is re-rendered every tick allocates nothing.
/// Spans handed out point into the frame and are valid until it is cleared or re-rendered. Not thread-safe.
/// </summary>
public sealed class DmxFrame
{
    /// <summary>Channels in one DMX universe.</summary>
    public const int UniverseSize = 512;

    private readonly List<int> _numbers = [];
    private readonly List<byte[]> _universes = [];
    private readonly Stack<byte[]> _spare = new();

    /// <summary>Number of universes in the frame.</summary>
    public int UniverseCount => _numbers.Count;

    /// <summary>The universe's 512 channels (index 0 is channel 1), adding a zeroed one if the frame has none yet.</summary>
    /// <param name="number">1-based universe number; any value is accepted, including 0 and negatives.</param>
    public Span<byte> Universe(int number)
    {
        var at = _numbers.BinarySearch(number);
        if (at >= 0)
            return _universes[at];
        byte[] data;
        if (_spare.TryPop(out var reused))
        {
            Array.Clear(reused);
            data = reused;
        }
        else
            data = new byte[UniverseSize];
        _numbers.Insert(~at, number);
        _universes.Insert(~at, data);
        return data;
    }

    /// <summary>The universe's channels, if the frame has it.</summary>
    public bool TryGetUniverse(int number, out ReadOnlySpan<byte> channels)
    {
        var at = _numbers.BinarySearch(number);
        channels = at >= 0 ? _universes[at] : default;
        return at >= 0;
    }

    /// <summary>The fixture's channels as they ended up in the frame (overlapping fixtures included); empty when its universe is absent, cut short at channel 512.</summary>
    public ReadOnlySpan<byte> Slice(Fixture fixture, int channelCount)
    {
        if (!TryGetUniverse(fixture.Universe, out var data))
            return ReadOnlySpan<byte>.Empty;
        var start = Math.Clamp(fixture.Address - 1, 0, UniverseSize);
        return data.Slice(start, Math.Clamp(channelCount, 0, UniverseSize - start));
    }

    /// <summary>Removes every universe; their buffers are kept for reuse.</summary>
    public void Clear()
    {
        foreach (var data in _universes)
            _spare.Push(data);
        _numbers.Clear();
        _universes.Clear();
    }

    /// <summary>Makes <paramref name="destination"/> an exact copy of this frame, reusing its buffers.</summary>
    public void CopyTo(DmxFrame destination)
    {
        if (ReferenceEquals(destination, this))
            return;
        destination.Clear();
        for (var i = 0; i < _numbers.Count; i++)
            _universes[i].CopyTo(destination.Universe(_numbers[i]));
    }

    /// <summary>
    /// Writes <paramref name="values"/> at the fixture's address, starting at its first channel (addresses
    /// below 1 start at channel 1) and stopping at channel 512. Adds the universe if needed.
    /// </summary>
    public void Write(Fixture fixture, ReadOnlySpan<byte> values)
    {
        var start = Math.Clamp(fixture.Address - 1, 0, UniverseSize);
        var count = Math.Min(values.Length, UniverseSize - start);
        values[..count].CopyTo(Universe(fixture.Universe).Slice(start, count));
    }

    /// <summary>A new frame with <paramref name="values"/> written over each fixture's channels (see <see cref="Write"/>); this frame is untouched. Allocates the copy.</summary>
    public DmxFrame With(IEnumerable<Fixture> fixtures, ReadOnlySpan<byte> values)
    {
        var copy = new DmxFrame();
        CopyTo(copy);
        foreach (var fixture in fixtures)
            copy.Write(fixture, values);
        return copy;
    }

    /// <summary>Enumerates the universes in ascending number order without allocating.</summary>
    public DmxUniverseEnumerator GetEnumerator() => new(this);

    /// <summary>The universe number at sorted position <paramref name="i"/>.</summary>
    internal int NumberAt(int i) => _numbers[i];

    /// <summary>The channels at sorted position <paramref name="i"/>.</summary>
    internal ReadOnlySpan<byte> ChannelsAt(int i) => _universes[i];
}
