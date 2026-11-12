using System.Diagnostics;
using System.Runtime;
using Lampion.Core.Engine;

namespace Lampion.Core.Live;

/// <summary>
/// The live clock: a dedicated high-priority thread that renders the <see cref="LiveEngine"/> on a fixed
/// schedule and publishes every frame to <see cref="Frames"/>, whatever the UI is doing. Nothing on the UI
/// thread (layout, 3D, a modal dialog, a GC of UI garbage) can delay a frame. Public members are for one
/// controlling thread.
/// </summary>
public sealed class LiveLoop : IDisposable
{
    /// <summary>Frames per second by default: over twice DMX's ~44 Hz refresh, so whatever the sender picks up is under 10 ms old.</summary>
    public const double DefaultRate = 100;

    private readonly Lock _statsGate = new();
    private readonly DmxFrame _frame = new();
    private Thread? _thread;
    private bool _stop;
    private GCLatencyMode _previousLatency;
    private long _statsStart;
    private long _frames;
    private long _renderTicks;
    private long _worstRender;
    private long _worstLate;
    private long _overruns;
    private volatile string? _error;

    /// <summary>A stopped loop for <paramref name="engine"/> at <paramref name="rate"/> frames per second.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rate"/> is not between 1 and 1000.</exception>
    public LiveLoop(LiveEngine engine, double rate = DefaultRate)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rate, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rate, 1000);
        Engine = engine;
        Rate = rate;
    }

    /// <summary>The engine it drives.</summary>
    public LiveEngine Engine { get; }

    /// <summary>Target frames per second.</summary>
    public double Rate { get; }

    /// <summary>Every rendered frame, newest wins; give it to the DMX sender and the UI.</summary>
    public DmxFrameMailbox Frames { get; } = new();

    /// <summary>True between <see cref="Start"/> and <see cref="Stop"/>.</summary>
    public bool IsRunning => _thread is not null;

    /// <summary>The last exception a render threw, in words; the loop keeps going and keeps the last good frame out. Null while healthy.</summary>
    public string? Error => _error;

    /// <summary>Starts the thread. Switches the GC to sustained low latency while running, so a full blocking collection can't stall a frame.</summary>
    public void Start()
    {
        if (_thread is not null)
            return;
        Volatile.Write(ref _stop, false);
        _previousLatency = GCSettings.LatencyMode;
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
        TakeStats();
        _thread = new Thread(Run) { IsBackground = true, Priority = ThreadPriority.Highest, Name = "Lampion live engine" };
        _thread.Start();
    }

    /// <summary>Stops the thread after the frame in progress.</summary>
    public void Stop()
    {
        if (_thread is null)
            return;
        Volatile.Write(ref _stop, true);
        _thread.Join();
        _thread = null;
        GCSettings.LatencyMode = _previousLatency;
    }

    /// <summary>Timing since the last call (or <see cref="Start"/>), and starts a new window. Any thread.</summary>
    public LiveLoopStats TakeStats()
    {
        lock (_statsGate)
        {
            var now = Stopwatch.GetTimestamp();
            var ms = 1000.0 / Stopwatch.Frequency;
            var stats = new LiveLoopStats(
                _frames,
                (now - _statsStart) / (double)Stopwatch.Frequency,
                _frames > 0 ? _renderTicks * ms / _frames : 0,
                _worstRender * ms,
                _worstLate * ms,
                _overruns
            );
            _statsStart = now;
            _frames = _renderTicks = _worstRender = _worstLate = _overruns = 0;
            return stats;
        }
    }

    /// <summary>Stops the loop.</summary>
    public void Dispose() => Stop();

    private void Run()
    {
        using var wait = new PreciseWait();
        var period = (long)(Stopwatch.Frequency / Rate);
        var due = Stopwatch.GetTimestamp();
        while (!Volatile.Read(ref _stop))
        {
            var start = Stopwatch.GetTimestamp();
            try
            {
                Engine.Render(start, _frame);
                Frames.Publish(_frame);
                _error = null;
            }
            catch (Exception ex)
            {
                // Lights that freeze or black out mid-show are worse than one wrong frame: report and carry on.
                _error = ex.Message;
            }
            var end = Stopwatch.GetTimestamp();
            Record(end - start, start - due, period);

            due += period;
            // After a stall (debugger, sleep), start a fresh schedule instead of rendering a burst to catch up.
            if (end - due > period)
                due = end + period;
            wait.Until(due, ref _stop);
        }
    }

    private void Record(long render, long late, long period)
    {
        lock (_statsGate)
        {
            _frames++;
            _renderTicks += render;
            _worstRender = Math.Max(_worstRender, render);
            _worstLate = Math.Max(_worstLate, late);
            if (late > period)
                _overruns++;
        }
    }
}
