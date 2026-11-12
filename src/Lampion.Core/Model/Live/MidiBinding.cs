namespace Lampion.Core.Model.Live;

/// <summary>A MIDI control or note, from any connected input.</summary>
/// <param name="Kind">Control change or note.</param>
/// <param name="Channel">MIDI channel, 1–16.</param>
/// <param name="Number">Controller or note number, 0–127.</param>
public sealed record MidiBinding(MidiBindingKind Kind, int Channel, int Number)
{
    /// <summary>Short label like "CC 7 · ch 1" or "Note 36 · ch 10".</summary>
    public override string ToString() => $"{(Kind == MidiBindingKind.Note ? "Note" : "CC")} {Number} · ch {Channel}";
}
