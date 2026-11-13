using Lampion.Core.Midi;
using Lampion.Core.Model.Live;

namespace Lampion.Core.Live;

/// <summary>
/// The console laid onto an APC mini mk2: faders 1–9 on its faders, the first 64 pads on the grid row by row from the
/// top left, shows 1–7 on scene buttons 1–7, Stop on the bottom scene button ("Stop All Clips") and GO on the
/// rightmost track button. Faders, pads and shows past those keep whatever binding they had.
/// </summary>
public static class ApcLayout
{
    /// <summary>Note of the button that stops the show.</summary>
    public const int StopNote = ApcMiniMk2.FirstScene + ApcMiniMk2.GridSize - 1;

    /// <summary>Note of the GO button.</summary>
    public const int GoNote = ApcMiniMk2.FirstTrack + ApcMiniMk2.GridSize - 1;

    /// <summary>Shows that get a scene button.</summary>
    public const int Shows = ApcMiniMk2.GridSize - 1;

    /// <summary>Rebinds <paramref name="live"/>'s faders, pads, shows, GO and Stop as described on the type.</summary>
    public static void Apply(LiveSet live)
    {
        for (var i = 0; i < live.Faders.Count && i < ApcMiniMk2.Faders; i++)
            live.Faders[i].Midi = new MidiBinding(MidiBindingKind.Control, 1, ApcMiniMk2.FirstFader + i);
        const int grid = ApcMiniMk2.GridSize;
        for (var i = 0; i < live.Pads.Count && i < grid * grid; i++)
            live.Pads[i].Midi = Note(ApcMiniMk2.PadNote(i / grid, i % grid));
        for (var i = 0; i < live.Shows.Count && i < Shows; i++)
            live.Shows[i].Midi = Note(ApcMiniMk2.FirstScene + i);
        live.StopMidi = Note(StopNote);
        live.GoMidi = Note(GoNote);
    }

    private static MidiBinding Note(int number) => new(MidiBindingKind.Note, 1, number);
}
