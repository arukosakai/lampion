using Lampion.Core.Midi;

namespace Lampion.Core.Model.Live;

/// <summary>A MIDI control or note, from any connected input.</summary>
/// <param name="Kind">Control change or note.</param>
/// <param name="Channel">MIDI channel, 1–16.</param>
/// <param name="Number">Controller or note number, 0–127.</param>
public sealed record MidiBinding(MidiBindingKind Kind, int Channel, int Number)
{
    /// <summary>True when <paramref name="message"/> is a press of this control: a note on for notes, any value for controls.</summary>
    public bool Matches(in MidiMessage message) =>
        message.Channel == Channel && message.Data1 == Number && (Kind == MidiBindingKind.Note ? message.IsNoteOn : message.IsControlChange);

    /// <summary>Short label like "CC 7 · ch 1" or "Note 36 · ch 10".</summary>
    public override string ToString() => $"{(Kind == MidiBindingKind.Note ? "Note" : "CC")} {Number} · ch {Channel}";
}
