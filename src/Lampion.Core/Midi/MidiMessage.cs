namespace Lampion.Core.Midi;

/// <summary>One short (three-byte) MIDI message.</summary>
/// <param name="Status">Status byte: message kind in the high nibble, channel − 1 in the low nibble.</param>
/// <param name="Data1">Note or controller number, 0–127.</param>
/// <param name="Data2">Velocity or controller value, 0–127.</param>
public readonly record struct MidiMessage(byte Status, byte Data1, byte Data2)
{
    /// <summary>The message kind in the high nibble, e.g. 0x90 for note on.</summary>
    public int Command => Status & 0xF0;

    /// <summary>MIDI channel, 1–16.</summary>
    public int Channel => (Status & 0x0F) + 1;

    /// <summary>A control change.</summary>
    public bool IsControlChange => Command == 0xB0;

    /// <summary>A note on with a velocity above 0.</summary>
    public bool IsNoteOn => Command == 0x90 && Data2 > 0;

    /// <summary>A note off, including the note on with velocity 0 most controllers send instead.</summary>
    public bool IsNoteOff => Command == 0x80 || (Command == 0x90 && Data2 == 0);

    /// <summary>The message as WinMM packs it: status in the low byte, then data 1, then data 2.</summary>
    public int Packed => Status | (Data1 << 8) | (Data2 << 16);

    /// <summary>A message from its WinMM packing (see <see cref="Packed"/>).</summary>
    public static MidiMessage FromPacked(int packed) => new((byte)packed, (byte)(packed >> 8), (byte)(packed >> 16));

    /// <summary>A note on; velocity 0 turns the note (or a controller's LED) off.</summary>
    /// <param name="channel">1–16.</param>
    /// <param name="note">0–127.</param>
    /// <param name="velocity">0–127.</param>
    public static MidiMessage NoteOn(int channel, int note, int velocity) =>
        new((byte)(0x90 | ((channel - 1) & 0x0F)), (byte)(note & 0x7F), (byte)(velocity & 0x7F));

    /// <summary>A control change.</summary>
    /// <param name="channel">1–16.</param>
    /// <param name="controller">0–127.</param>
    /// <param name="value">0–127.</param>
    public static MidiMessage ControlChange(int channel, int controller, int value) =>
        new((byte)(0xB0 | ((channel - 1) & 0x0F)), (byte)(controller & 0x7F), (byte)(value & 0x7F));
}
