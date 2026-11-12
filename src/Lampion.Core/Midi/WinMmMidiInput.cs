using System.Runtime.Versioning;

namespace Lampion.Core.Midi;

/// <summary>A WinMM MIDI input. Messages arrive on the driver's callback thread and go straight to the handler.</summary>
[SupportedOSPlatform("windows")]
internal sealed class WinMmMidiInput : IMidiInput
{
    private readonly MidiHandler _handler;
    // Held in a field so the GC can't collect the delegate while the driver still has its function pointer.
    private readonly WinMm.MidiInProc _callback;
    private IntPtr _handle;

    /// <summary>Opens and starts input <paramref name="index"/>.</summary>
    /// <exception cref="IOException">The device is missing or in use.</exception>
    public WinMmMidiInput(int index, MidiHandler handler)
    {
        var names = WinMm.InputNames();
        if ((uint)index >= (uint)names.Count)
            throw new IOException($"MIDI input {index + 1} isn't connected.");
        Name = names[index];
        _handler = handler;
        _callback = OnMessage;
        WinMm.Check(WinMm.midiInOpen(out _handle, (uint)index, _callback, IntPtr.Zero, WinMm.CallbackFunction), "open", Name);
        var started = WinMm.midiInStart(_handle);
        if (started != 0)
        {
            WinMm.midiInClose(_handle);
            _handle = IntPtr.Zero;
            WinMm.Check(started, "start", Name);
        }
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>Stops and closes the device; no callbacks arrive after this returns.</summary>
    public void Dispose()
    {
        if (_handle == IntPtr.Zero)
            return;
        WinMm.midiInStop(_handle);
        WinMm.midiInReset(_handle);
        WinMm.midiInClose(_handle);
        _handle = IntPtr.Zero;
        GC.KeepAlive(_callback);
    }

    private void OnMessage(IntPtr handle, uint message, IntPtr instance, IntPtr param1, IntPtr param2)
    {
        if (message == WinMm.MimData)
            _handler(MidiMessage.FromPacked((int)param1));
    }
}
