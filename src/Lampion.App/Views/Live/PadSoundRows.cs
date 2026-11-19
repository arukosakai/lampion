using Avalonia.Controls;
using Avalonia.Layout;
using Lampion.App.Controls;
using Lampion.App.Editor;
using Lampion.App.Views.Inspector;
using Lampion.App.Views.Shell;
using Lampion.Core.Model.Live;
using Lampion.Core.Persistence;

namespace Lampion.App.Views.Live;

/// <summary>The pad editor's sound rows: the file (Choose / Clear), its volume and whether it stops with the pad.</summary>
internal static class PadSoundRows
{
    private static readonly ScrubRange VolumeRange = new(0, 2, 0.005, 100, "0", "%");

    /// <summary>The rows for <paramref name="pad"/>; the file picker opens over <paramref name="owner"/>.</summary>
    public static Control Build(Window owner, EditorState state, InspectorFields fields, LivePad pad)
    {
        var file = Ui.Label(Describe(pad), null);
        file.MinWidth = 130;
        file.VerticalAlignment = VerticalAlignment.Center;
        async Task Choose()
        {
            if (await Dialogs.PickOpen(owner, "Choose a pad sound", ("Audio", MediaImport.AudioPatterns)) is { } path)
            {
                pad.SoundPath = ShowPaths.Relative(state.FilePath, path);
                file.Text = Describe(pad);
            }
        }
        var choose = Ui.Button("Choose…", () => _ = Choose(), "An audio file played from the start each time the pad switches on");
        var clear = Ui.Button("Clear", () =>
        {
            pad.SoundPath = "";
            file.Text = Describe(pad);
        }, "Play no sound");

        return Ui.Column(
            8,
            Ui.Field("Sound", Ui.Row(file, choose, clear), 110),
            Ui.Field("Volume", fields.Scrub(pad.SoundVolume, VolumeRange, v => pad.SoundVolume = v), 110),
            Ui.Field("Stop on off", fields.Check(pad.SoundStopsWithPad, v => pad.SoundStopsWithPad = v), 110));
    }

    private static string Describe(LivePad pad) => pad.SoundPath.Length == 0 ? "None" : Path.GetFileName(pad.SoundPath);
}
