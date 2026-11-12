using Lampion.Core.Midi;

namespace Lampion.Core.Live;

/// <summary>
/// Turns incoming MIDI into fader moves and pad presses, straight from the driver's callback thread: a
/// controller reaches the engine's next frame without passing through the UI. Also keeps the latest message so
/// the UI can show activity and learn bindings. <see cref="Handle"/> is allocation-free and lock-free.
/// </summary>
public sealed class MidiRouter
{
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
        if (target > 0)
        {
            Controls.SetFader(target - 1, message.Data2 / 127.0);
            return;
        }
        if (target == 0)
            return;
        var pad = -target - 1;
        var mode = patch.PadModeAt(pad);
        var pressed = message.IsControlChange ? message.Data2 >= 64 : message.IsNoteOn;
        if (pressed)
            Controls.PressPad(pad, mode);
        else
            Controls.ReleasePad(pad, mode);
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
