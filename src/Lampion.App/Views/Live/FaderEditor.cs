using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Lampion.App.Editor;
using Lampion.App.Live;
using Lampion.Core.Model.Live;
using Lampion.Core.Model.Rig;

namespace Lampion.App.Views.Live;

/// <summary>Edits one console fader in place; closing records the edit as one undo step.</summary>
internal static class FaderEditor
{
    private static readonly (LiveFaderTarget Target, string Name)[] Targets =
    [
        (LiveFaderTarget.Intensity, "Fixture intensity"),
        (LiveFaderTarget.Hue, "Fixture colour (hue)"),
        (LiveFaderTarget.Saturation, "Fixture saturation"),
        (LiveFaderTarget.Role, "Fixture channel (pan, strobe, …)"),
        (LiveFaderTarget.PadIntensity, "Pad intensity"),
        (LiveFaderTarget.PadSpeed, "Pad speed (¼× – 4×)"),
        (LiveFaderTarget.PadHueShift, "Pad hue shift"),
    ];

    /// <summary>Shows the editor over <paramref name="owner"/> until closed.</summary>
    public static Task Show(Window owner, EditorState state, LiveSession session, LiveFader fader)
    {
        var window = Dialogs.Modal("Fader", 460);
        var project = state.Project;
        var pads = project.Live.Pads;

        var fixtureRows = TargetPicker.Build(project, fader.FixtureIds, fader.GroupIds);
        var role = Ui.Field("Channel", Ui.Combo(Enum.GetValues<ChannelRole>(), fader.Role, r => fader.Role = r), 110);
        var pad = Ui.Field("Pad", Ui.Combo(pads, pads.Find(p => p.Id == fader.PadId)!, p => fader.PadId = p.Id, p => p.Name), 110);
        void ShowTarget()
        {
            var forPad = fader.Target is LiveFaderTarget.PadIntensity or LiveFaderTarget.PadSpeed or LiveFaderTarget.PadHueShift;
            fixtureRows.IsVisible = !forPad;
            pad.IsVisible = forPad;
            role.IsVisible = fader.Target == LiveFaderTarget.Role;
        }
        var target = Ui.Combo(Targets.Select(t => t.Target), fader.Target, t => { fader.Target = t; ShowTarget(); }, t => Array.Find(Targets, x => x.Target == t).Name);
        ShowTarget();

        var done = new Button { Content = "Done", MinWidth = 90, IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        done.Click += (_, _) => window.Close();
        window.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 8,
            Children =
            {
                Ui.Field("Name", Ui.Text(fader.Name, v => fader.Name = v), 110),
                Ui.Field("Controls", target, 110),
                role,
                pad,
                fixtureRows,
                MidiLearnRow.Build(session, () => fader.Midi, b => fader.Midi = b),
                Ui.Label("Fixture faders only reach fixtures the playing show doesn't drive.", null),
                done,
            },
        };
        window.Closed += (_, _) =>
        {
            session.Learn(null);
            state.Commit();
        };
        return window.ShowDialog(owner);
    }
}
