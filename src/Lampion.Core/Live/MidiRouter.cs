using Lampion.Core.Midi;

namespace Lampion.Core.Live;

/// <summary>
/// Turns incoming MIDI into fader moves and pad presses, straight from the driver's callback thread: a
/// controller reaches the engine's next frame without passing through the UI. Presses bound to neither are queued
/// for the UI (set-list buttons), and the latest message is kept so the UI can show activity and learn bindings.
/// <see cref="Handle"/> is allocation-free and lock-free.
/// </summary>
public sealed class MidiRouter
{
    /// <summary>Unbound presses kept for <see cref="TryTakePress"/>; older ones are dropped.</summary>
    public const int PressCapacity = 64;

    // Each entry is (sequence number << 24) | packed message, so the reader can tell a fresh entry from a stale or overwritten one.
    private readonly long[] _presses = new long[PressCapacity];
    private readonly bool[] _controlHigh = new bool[16 * 128];
    private long _pressesWritten;
    private long _pressesRead;
    private LivePatch? _patch;
    private long _last;
    private long _count;
    private volatile string? _error;

    /// <summary>A router writing to <paramref name="controls"/>.</summary>
    public MidiRouter(LiveControls controls) => Controls = controls;

    /// <summary>Where messages land.</summary>
    public LiveControls Controls { get; }

    /// <summary>The bindings in use; null ignores everything. Any thread.</summary>
    public LivePatch? Patch
    {
        get => Volatile.Read(ref _patch);
        set => Volatile.Write(ref _patch, value);
    }

    /// <summary>Routes one message. Safe to call from any thread, including several inputs at once. Never throws: it runs on
    /// the driver's thread, where an exception would end the process.</summary>
    public void Handle(MidiMessage message)
    {
        try
        {
            Route(message);
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
    }

    /// <summary>Why the last message couldn't be routed, or null; for the status line.</summary>
    public string? Error => _error;

    private void Route(MidiMessage message)
    {
        Interlocked.Exchange(ref _last, (Interlocked.Increment(ref _count) << 24) | (uint)message.Packed);
        if (Volatile.Read(ref _patch) is not { } patch)
            return;
        var target = patch.Midi.Lookup(in message);
        if (target == 0)
        {
            if (IsPress(message))
                QueuePress(message);
            return;
        }
        if (target > 0)
        {
            Controls.SetFader(target - 1, message.Data2 / 127.0);
            return;
        }
        var pad = -target - 1;
        var mode = patch.PadModeAt(pad);
        var pressed = message.IsControlChange ? message.Data2 >= 64 : message.IsNoteOn;
        if (pressed)
            Controls.PressPad(pad, mode);
        else
            Controls.ReleasePad(pad, mode);
    }

    /// <summary>
    /// Takes the oldest queued press (a note on, or a control rising past half way) that no fader or pad is bound to.
    /// Presses beyond the last <see cref="PressCapacity"/> are lost. One reader thread only.
    /// </summary>
    public bool TryTakePress(out MidiMessage message)
    {
        while (true)
        {
            message = default;
            var written = Interlocked.Read(ref _pressesWritten);
            if (_pressesRead >= written)
                return false;
            if (written - _pressesRead > PressCapacity)
                _pressesRead = written - PressCapacity;
            var next = _pressesRead + 1;
            var entry = Interlocked.Read(ref _presses[next & (PressCapacity - 1)]);
            var sequence = entry >> 24;
            // Claimed by a writer that hasn't stored it yet: try again next time.
            if (sequence < next)
                return false;
            _pressesRead = next;
            if (sequence > next)
                continue;
            message = MidiMessage.FromPacked((int)(entry & 0xFFFFFF));
            return true;
        }
    }

    // A control counts once when it rises past half way, so a knob turned through the middle isn't a burst of presses.
    private bool IsPress(in MidiMessage message)
    {
        if (!message.IsControlChange)
            return message.IsNoteOn;
        ref var high = ref _controlHigh[((message.Channel - 1) << 7) | (message.Data1 & 0x7F)];
        var wasHigh = high;
        high = message.Data2 >= 64;
        return high && !wasHigh;
    }

    private void QueuePress(MidiMessage message)
    {
        var sequence = Interlocked.Increment(ref _pressesWritten);
        Interlocked.Exchange(ref _presses[sequence & (PressCapacity - 1)], (sequence << 24) | (uint)message.Packed);
    }

    /// <summary>
    /// The newest message, if one arrived since <paramref name="seen"/>; pass the same variable every call. For
    /// MIDI learn and the activity light; messages in between are skipped.
    /// </summary>
    public bool TryGetLatest(ref long seen, out MidiMessage message)
    {
        var last = Interlocked.Read(ref _last);
        var count = last >> 24;
        message = MidiMessage.FromPacked((int)(last & 0xFFFFFF));
        if (count == seen)
            return false;
        seen = count;
        return count != 0;
    }
}
