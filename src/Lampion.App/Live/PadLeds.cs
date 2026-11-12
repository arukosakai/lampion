using Lampion.Core.Live;
using Lampion.Core.Midi;
using Lampion.Core.Model.Live;

namespace Lampion.App.Live;

/// <summary>
/// Lights a controller's pads to match the console, launchpad style: sends a note on with the pad's LED
/// velocity when a pad turns on and velocity 0 when it turns off, only on changes. Runs on the UI tick, so
/// a slow MIDI driver can never hold up a DMX frame.
/// </summary>
internal sealed class PadLeds
{
    private readonly bool[] _lit = new bool[LiveControls.MaxPads];

    /// <summary>Sends whatever changed since the last call. <paramref name="force"/> resends everything, e.g. after reconnecting.</summary>
    /// <returns>The send error in words, or null.</returns>
    public string? Update(IMidiOutput? output, IReadOnlyList<LivePad> pads, LiveControls controls, bool force)
    {
        if (output is null)
            return null;
        try
        {
            for (var i = 0; i < pads.Count && i < LiveControls.MaxPads; i++)
            {
                var on = controls.IsPadOn(i);
                if (!force && on == _lit[i])
                    continue;
                _lit[i] = on;
                if (pads[i].Midi is { Kind: MidiBindingKind.Note } note)
                    output.Send(MidiMessage.NoteOn(note.Channel, note.Number, on ? Math.Clamp(pads[i].LedVelocity, 1, 127) : 0));
            }
            return null;
        }
        catch (IOException ex)
        {
            return ex.Message;
        }
    }
}
