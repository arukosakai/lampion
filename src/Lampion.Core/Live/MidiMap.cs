using Lampion.Core.Midi;
using Lampion.Core.Model.Live;

namespace Lampion.Core.Live;

/// <summary>
/// MIDI bindings as flat tables (16 channels × 128 numbers, per message kind), so routing a message is two
/// array reads. Entries hold fader position + 1, or −(pad position + 1), or 0 for unbound. Immutable.
/// </summary>
internal sealed class MidiMap
{
    private const int Slots = 16 * 128;

    private readonly int[] _controls = new int[Slots];
    private readonly int[] _notes = new int[Slots];

    /// <summary>Binds <paramref name="binding"/> to fader <paramref name="fader"/>; the first binding of a control wins.</summary>
    public void BindFader(MidiBinding? binding, int fader) => Bind(binding, fader + 1);

    /// <summary>Binds <paramref name="binding"/> to pad <paramref name="pad"/>; the first binding of a control wins.</summary>
    public void BindPad(MidiBinding? binding, int pad) => Bind(binding, -(pad + 1));

    /// <summary>What <paramref name="message"/> is bound to, encoded as described on the type; 0 when nothing.</summary>
    public int Lookup(in MidiMessage message)
    {
        var slot = ((message.Channel - 1) << 7) | message.Data1;
        if (message.IsControlChange)
            return _controls[slot];
        return message.IsNoteOn || message.IsNoteOff ? _notes[slot] : 0;
    }

    private void Bind(MidiBinding? binding, int value)
    {
        if (binding is null || binding.Channel is < 1 or > 16 || binding.Number is < 0 or > 127)
            return;
        var table = binding.Kind == MidiBindingKind.Note ? _notes : _controls;
        ref var slot = ref table[((binding.Channel - 1) << 7) | binding.Number];
        if (slot == 0)
            slot = value;
    }
}
