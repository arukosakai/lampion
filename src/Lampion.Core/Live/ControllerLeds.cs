using Lampion.Core.Midi;
using Lampion.Core.Model.Live;

namespace Lampion.Core.Live;

/// <summary>
/// Lights a controller to match the console: pads while they're on, show buttons for the show playing (blinking while it
/// loads), Stop while one plays and GO while one is up next. Only changes are sent. Most controllers get a note on with
/// the pad's LED velocity and 0 for off; an APC mini mk2 gets its pads in their effect's colour, dim while off.
/// Call from one thread, never the engine's, so a slow MIDI driver can't hold up a DMX frame.
/// </summary>
public sealed class ControllerLeds
{
    private const int Slots = 16 * 128;
    private const int Nothing = -1;

    // Per binding slot (channel, note): the message last sent, and the one wanted now; Nothing for none.
    private readonly int[] _sent = new int[Slots];
    private readonly int[] _wanted = new int[Slots];
    private readonly bool[] _claimed = new bool[Slots];

    /// <summary>A driver that assumes nothing about the controller's lights yet.</summary>
    public ControllerLeds() => Forget();

    /// <summary>Forgets what was sent, so the next <see cref="Update"/> sends everything; for a freshly opened device.</summary>
    public void Forget() => Array.Fill(_sent, Nothing);

    /// <summary>Sends whatever changed since the last call. Allocation-free.</summary>
    /// <param name="output">Where to send; null does nothing.</param>
    /// <param name="live">The console; its pads are by position in <paramref name="controls"/>.</param>
    /// <param name="controls">Pad states.</param>
    /// <param name="playing">The show playing, or null.</param>
    /// <param name="loading">The show being loaded, or null.</param>
    /// <param name="next">The show GO would cut to, or null.</param>
    /// <returns>The send error in words, or null.</returns>
    public string? Update(IMidiOutput? output, LiveSet live, LiveControls controls, LiveShow? playing, LiveShow? loading, LiveShow? next)
    {
        if (output is null)
            return null;
        var apc = ApcMiniMk2.Matches(output.Name);
        Array.Fill(_wanted, Nothing);
        Array.Clear(_claimed);
        if (apc)
        {
            // The whole surface, so pads nothing is bound to any more go dark.
            for (var note = ApcMiniMk2.FirstPad; note <= ApcMiniMk2.LastPad; note++)
                _wanted[note] = ApcMiniMk2.Off(note).Packed;
            for (var i = 0; i < ApcMiniMk2.GridSize; i++)
            {
                _wanted[ApcMiniMk2.FirstTrack + i] = ApcMiniMk2.Off(ApcMiniMk2.FirstTrack + i).Packed;
                _wanted[ApcMiniMk2.FirstScene + i] = ApcMiniMk2.Off(ApcMiniMk2.FirstScene + i).Packed;
            }
        }
        for (var i = 0; i < live.Pads.Count && i < LiveControls.MaxPads; i++)
        {
            var pad = live.Pads[i];
            var on = controls.IsPadOn(i);
            Want(pad.Midi, apc, on, blink: false, apc ? ApcMiniMk2.NearestColour(pad.Effect.SwatchColor) : pad.LedVelocity);
        }
        foreach (var show in live.Shows)
        {
            var isLoading = ReferenceEquals(show, loading);
            Want(show.Midi, apc, isLoading || ReferenceEquals(show, playing), isLoading, ApcMiniMk2.Green);
        }
        Want(live.StopMidi, apc, playing is not null, blink: false, ApcMiniMk2.White);
        Want(live.GoMidi, apc, next is not null, blink: false, ApcMiniMk2.Green);

        try
        {
            for (var slot = 0; slot < Slots; slot++)
            {
                var wanted = _wanted[slot];
                var sent = _sent[slot];
                if (wanted == sent)
                    continue;
                // A binding that went away leaves its light off rather than stuck on.
                var message = wanted != Nothing ? MidiMessage.FromPacked(wanted) : MidiMessage.NoteOn((slot >> 7) + 1, slot & 0x7F, 0);
                output.Send(message);
                _sent[slot] = wanted;
            }
            return null;
        }
        catch (IOException ex)
        {
            return ex.Message;
        }
    }

    /// <summary>Switches off every light this driver turned on, then forgets them; for closing the device.</summary>
    public void Blank(IMidiOutput? output)
    {
        if (output is null)
            return;
        try
        {
            for (var slot = 0; slot < Slots; slot++)
                if (_sent[slot] != Nothing)
                    output.Send(MidiMessage.NoteOn((slot >> 7) + 1, slot & 0x7F, 0));
        }
        catch (IOException)
        {
            // The device is going away anyway.
        }
        Forget();
    }

    private void Want(MidiBinding? binding, bool apc, bool on, bool blink, int colour)
    {
        if (binding is not { Kind: MidiBindingKind.Note, Channel: >= 1 and <= 16, Number: >= 0 and <= 127 })
            return;
        var slot = ((binding.Channel - 1) << 7) | binding.Number;
        var note = binding.Number;
        MidiMessage message;
        if (apc && binding.Channel == 1 && ApcMiniMk2.IsPad(note))
            message = ApcMiniMk2.PadLight(note, blink ? PadGlow.Blink : on ? PadGlow.Full : PadGlow.Dim, colour);
        else if (apc && binding.Channel == 1 && ApcMiniMk2.IsButton(note))
            message = ApcMiniMk2.ButtonLight(note, on, blink);
        else
            message = MidiMessage.NoteOn(binding.Channel, note, on ? Math.Clamp(colour, 1, 127) : 0);
        // The first binding of a light wins, as it does for presses.
        if (_claimed[slot])
            return;
        _claimed[slot] = true;
        _wanted[slot] = message.Packed;
    }
}
