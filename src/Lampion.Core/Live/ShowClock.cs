using System.Diagnostics;

namespace Lampion.Core.Live;

/// <summary>
/// Where a live show is, in show seconds, at any <see cref="Stopwatch"/> timestamp. Free-runs between updates and,
/// when the show has music, is steered by the audio player's position reports: small errors are removed by running
/// the lights up to <see cref="MaxSlew"/> faster or slower for about <see cref="CorrectionSeconds"/> (invisible, no
/// jumps); only errors past <see cref="JumpThreshold"/> (a seek, a stall) snap. The engine reads it lock-free
/// every frame; writers (UI, the media player's thread) are serialized.
/// </summary>
public sealed class ShowClock
{
    /// <summary>Errors larger than this many seconds snap instead of being slewed away.</summary>
    public const double JumpThreshold = 0.25;

    /// <summary>Seconds over which a small error is removed.</summary>
    public const double CorrectionSeconds = 1;

    /// <summary>Most the clock runs fast or slow while correcting, as a fraction (0.05 = ±5 %).</summary>
    public const double MaxSlew = 0.05;

    private readonly Lock _writers = new();
    private Anchor _anchor = new(0, 0, 1, false);
    private long _lastError;

    /// <summary>True when the audio player drives the clock: it holds at the start until the music actually plays, and a loop waits for the music to restart.</summary>
    public bool FollowsMedia { get; set; }

    /// <summary>False while holding (before the music starts); the show stays on its <see cref="Hold"/> time.</summary>
    public bool IsRunning => Volatile.Read(ref _anchor).Running;

    /// <summary>Show seconds minus the clock, at the last <see cref="Sync"/>; how far the lights were behind the music before correcting.</summary>
    public double LastError => BitConverter.Int64BitsToDouble(Interlocked.Read(ref _lastError));

    /// <summary>Show time at <paramref name="timestamp"/>. Lock-free and allocation-free; any thread.</summary>
    public double TimeAt(long timestamp)
    {
        var a = Volatile.Read(ref _anchor);
        return a.Running ? a.Time + (timestamp - a.Timestamp) / (double)Stopwatch.Frequency * a.Rate : a.Time;
    }

    /// <summary>Runs from <paramref name="time"/> show seconds at <paramref name="timestamp"/>, at normal speed.</summary>
    public void Start(long timestamp, double time)
    {
        lock (_writers)
            Volatile.Write(ref _anchor, new Anchor(timestamp, time, 1, true));
    }

    /// <summary>Stops at <paramref name="time"/> show seconds until the next <see cref="Start"/> or <see cref="Sync"/>.</summary>
    public void Hold(double time)
    {
        lock (_writers)
            Volatile.Write(ref _anchor, new Anchor(0, time, 1, false));
    }

    /// <summary>
    /// The music was at <paramref name="showTime"/> show seconds at <paramref name="timestamp"/>. A holding clock starts
    /// from there; a running one slews toward it, or snaps when off by more than <see cref="JumpThreshold"/>.
    /// </summary>
    /// <returns>The error that was found, in seconds (positive: the lights were behind).</returns>
    public double Sync(long timestamp, double showTime)
    {
        lock (_writers)
        {
            var a = _anchor;
            if (!a.Running)
            {
                Volatile.Write(ref _anchor, new Anchor(timestamp, showTime, 1, true));
                return Error(0);
            }
            var now = a.Time + (timestamp - a.Timestamp) / (double)Stopwatch.Frequency * a.Rate;
            var error = showTime - now;
            var next = Math.Abs(error) > JumpThreshold
                ? new Anchor(timestamp, showTime, 1, true)
                // Re-anchor where the lights are now, so the correction bends the speed without a step in time.
                : new Anchor(timestamp, now, 1 + Math.Clamp(error / CorrectionSeconds, -MaxSlew, MaxSlew), true);
            Volatile.Write(ref _anchor, next);
            return Error(error);
        }
    }

    private double Error(double error)
    {
        Interlocked.Exchange(ref _lastError, BitConverter.DoubleToInt64Bits(error));
        return error;
    }

    // Replaced whole, never edited, so the engine can't read a half-updated anchor.
    private sealed record Anchor(long Timestamp, double Time, double Rate, bool Running);
}
