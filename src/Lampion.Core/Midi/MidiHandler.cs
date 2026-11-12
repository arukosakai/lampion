namespace Lampion.Core.Midi;

/// <summary>
/// Receives incoming MIDI. Called on the driver's own thread, often at high priority: return quickly,
/// don't block, don't allocate, and don't call back into the MIDI API.
/// </summary>
public delegate void MidiHandler(MidiMessage message);
