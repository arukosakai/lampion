namespace Lampion.Core.Midi;

/// <summary>An open MIDI input; messages go to the handler it was opened with until disposed.</summary>
public interface IMidiInput : IDisposable
{
    /// <summary>The device name, as the system reports it.</summary>
    string Name { get; }
}
