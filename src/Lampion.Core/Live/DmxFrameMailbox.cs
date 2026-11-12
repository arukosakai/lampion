using Lampion.Core.Engine;

namespace Lampion.Core.Live;

/// <summary>
/// Hands the newest frame from the engine thread to readers (DMX sender, UI) that run on their own clocks.
/// Readers always get the latest frame and never a half-written one; the lock is only held for a 512-byte-per-
/// universe copy, so neither side waits for more than microseconds.
/// </summary>
public sealed class DmxFrameMailbox : IDmxFrameSource
{
    private readonly Lock _gate = new();
    private readonly DmxFrame _frame = new();
    private long _sequence;

    /// <inheritdoc />
    public long Sequence => Interlocked.Read(ref _sequence);

    /// <summary>Makes a copy of <paramref name="frame"/> the newest. Allocation-free once the universes exist.</summary>
    public void Publish(DmxFrame frame)
    {
        lock (_gate)
        {
            frame.CopyTo(_frame);
            Interlocked.Increment(ref _sequence);
        }
    }

    /// <inheritdoc />
    public long CopyLatestTo(DmxFrame destination)
    {
        lock (_gate)
        {
            _frame.CopyTo(destination);
            return _sequence;
        }
    }
}
