using Lampion.Core.Midi;

namespace Lampion.Core.Tests;

/// <summary>A MIDI output that keeps what was sent.</summary>
internal sealed class RecordingMidiOutput(string name) : IMidiOutput
{
    public string Name { get; } = name;

    public List<MidiMessage> Sent { get; } = [];

    public void Send(MidiMessage message) => Sent.Add(message);

    public void Dispose()
    {
    }
}
