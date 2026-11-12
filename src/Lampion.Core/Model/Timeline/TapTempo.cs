namespace Lampion.Core.Model.Timeline;

/// <summary>
/// Tap-tempo: turns a run of taps into beats per minute, averaging the most recent intervals.
/// A pause longer than <see cref="MaxGap"/> starts a new run. Not thread-safe.
/// </summary>
public sealed class TapTempo
{
    /// <summary>Longest pause between taps, in seconds, that still counts as the same run (30 BPM).</summary>
    public const double MaxGap = 2;

    /// <summary>How many recent taps are averaged, so a run can drift to a new tempo.</summary>
    public const int Window = 8;

    private readonly double[] _taps = new double[Window];
    private int _count;

    /// <summary>Taps in the current run, up to <see cref="Window"/>.</summary>
    public int Count => _count;

    /// <summary>Records a tap.</summary>
    /// <param name="seconds">When the tap happened, in seconds on any monotonic clock.</param>
    /// <returns>The tempo in BPM once the run has two taps; null for the first tap of a run.</returns>
    public double? Tap(double seconds)
    {
        if (_count > 0)
        {
            var gap = seconds - _taps[_count - 1];
            if (gap <= 0 || gap > MaxGap)
                _count = 0;
        }
        if (_count == Window)
        {
            Array.Copy(_taps, 1, _taps, 0, Window - 1);
            _count--;
        }
        _taps[_count++] = seconds;
        if (_count < 2)
            return null;
        var averageInterval = (_taps[_count - 1] - _taps[0]) / (_count - 1);
        return 60 / averageInterval;
    }

    /// <summary>Forgets the current run.</summary>
    public void Reset() => _count = 0;
}
