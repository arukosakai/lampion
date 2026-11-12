namespace Lampion.Core.Midi;

/// <summary>An open MIDI output, used to light a controller's pads. Not thread-safe; call from one thread.</summary>
public interface IMidiOutput : IDisposable
{
    /// <summary>The device name, as the system reports it.</summary>
    string Name { get; }

    /// <summary>Sends a short message. Allocation-free.</summary>
    /// <exception cref="IOException">The device went away.</exception>
    void Send(MidiMessage message);
}
