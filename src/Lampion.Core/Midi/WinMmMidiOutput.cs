using System.Runtime.Versioning;

namespace Lampion.Core.Midi;

/// <summary>A WinMM MIDI output.</summary>
[SupportedOSPlatform("windows")]
internal sealed class WinMmMidiOutput : IMidiOutput
{
    private IntPtr _handle;

    /// <summary>Opens output <paramref name="index"/>.</summary>
    /// <exception cref="IOException">The device is missing or in use.</exception>
    public WinMmMidiOutput(int index)
    {
        var names = WinMm.OutputNames();
        if ((uint)index >= (uint)names.Count)
            throw new IOException($"MIDI output {index + 1} isn't connected.");
        Name = names[index];
        WinMm.Check(WinMm.midiOutOpen(out _handle, (uint)index, IntPtr.Zero, IntPtr.Zero, 0), "open", Name);
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public void Send(MidiMessage message) => WinMm.Check(WinMm.midiOutShortMsg(_handle, (uint)message.Packed), "send to", Name);

    /// <summary>Silences and closes the device.</summary>
    public void Dispose()
    {
        if (_handle == IntPtr.Zero)
            return;
        WinMm.midiOutReset(_handle);
        WinMm.midiOutClose(_handle);
        _handle = IntPtr.Zero;
    }
}
