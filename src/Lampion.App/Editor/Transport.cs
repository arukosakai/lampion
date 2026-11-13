using System.Diagnostics;

namespace Lampion.App.Editor;

/// <summary>
/// The playhead and the wall clock that moves it while playing. Raises no events; every method that can
/// move the playhead reports whether it did, so <see cref="EditorState"/> knows when to.
/// </summary>
internal sealed class Transport
{
    // Play() from within this many seconds of the end starts over from 0 instead of stopping at once.
    private const double RewindMargin = 0.01;

    private readonly Stopwatch _clock = new();
    private double _playStart;

    /// <summary>The playhead in seconds; never negative.</summary>
    public double Playhead { get; private set; }

    /// <summary>True while the clock is running.</summary>
    public bool IsPlaying => _clock.IsRunning;

    /// <summary>True to wrap to 0 at the end of the content instead of stopping.</summary>
    public bool Loop { get; set; }

    /// <summary>Moves the playhead to <paramref name="time"/> seconds (clamped at 0), carrying on from there if playing.</summary>
    public void Seek(double time)
    {
        Playhead = Math.Max(0, time);
        if (IsPlaying)
        {
            _playStart = Playhead;
            _clock.Restart();
        }
    }

    /// <summary>Starts the clock, rewinding first when the playhead is at <paramref name="contentEnd"/>.</summary>
    /// <returns>False when already playing.</returns>
    public bool Play(double contentEnd)
    {
        if (IsPlaying)
            return false;
        if (Playhead >= contentEnd - RewindMargin)
            Playhead = 0;
        _playStart = Playhead;
        _clock.Restart();
        return true;
    }

    /// <summary>Stops the clock, leaving the playhead where it is.</summary>
    /// <returns>False when already paused.</returns>
    public bool Pause()
    {
        if (!IsPlaying)
            return false;
        _clock.Stop();
        return true;
    }

    /// <summary>Keeps the playhead where it is while playing, e.g. until media has started. Allocation-free.</summary>
    public void Hold()
    {
        if (!IsPlaying)
            return;
        _playStart = Playhead;
        _clock.Restart();
    }

    /// <summary>
    /// Moves the playhead to where the clock says it is, stopping (or wrapping, with <see cref="Loop"/>) at
    /// <paramref name="contentEnd"/>. Allocation-free.
    /// </summary>
    /// <returns>False when paused, in which case nothing moved.</returns>
    public bool Advance(double contentEnd)
    {
        if (!IsPlaying)
            return false;
        var t = _playStart + _clock.Elapsed.TotalSeconds;
        if (t >= contentEnd)
        {
            if (Loop)
            {
                _playStart = 0;
                _clock.Restart();
                t = 0;
            }
            else
            {
                _clock.Stop();
                t = contentEnd;
            }
        }
        Playhead = t;
        return true;
    }
}
