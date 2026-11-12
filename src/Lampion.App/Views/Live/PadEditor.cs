using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Lampion.App.Editor;
using Lampion.App.Live;
using Lampion.App.Views.Inspector;
using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Live;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Views.Live;

/// <summary>Edits one effect pad in place, with the same parameter rows as Effect Controls; closing records one undo step.</summary>
internal static class PadEditor
{
    /// <summary>Shows the editor over <paramref name="owner"/> until closed.</summary>
    public static Task Show(Window owner, EditorState state, LiveSession session, LivePad pad)
    {
        var window = Dialogs.Modal("Pad", 480);
        var parameters = new StackPanel { Spacing = 2 };
        InspectorFields? fields = null;
        fields = new InspectorFields(new InspectorContext(state, parameters, () => FillParameters(parameters, pad, fields!)));
        FillParameters(parameters, pad, fields);

        var effect = Ui.Combo(EffectCatalog.Entries, EffectCatalog.Entries.First(e => e.Kind == pad.Effect.Kind), e =>
        {
            pad.Effect = EffectCatalog.Create(e.Kind);
            pad.Name = e.Name;
            FillParameters(parameters, pad, fields);
        }, e => e.Name);

        var done = new Button { Content = "Done", MinWidth = 90, IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        done.Click += (_, _) => window.Close();
        window.Content = new ScrollViewer
        {
            MaxHeight = 760,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 8,
                Children =
                {
                    Ui.Field("Name", Ui.Text(pad.Name, v => pad.Name = v), 110),
                    Ui.Field("Effect", effect, 110),
                    parameters,
                    Ui.Field("Press", Ui.Combo(Enum.GetValues<PadMode>(), pad.Mode, m => pad.Mode = m, m => m == PadMode.Toggle ? "Toggle on / off" : "Flash while held"), 110),
                    Ui.Field("Blend", Ui.Combo(Enum.GetValues<BlendMode>(), pad.Blend, b => pad.Blend = b), 110),
                    Ui.Field("Order", Ui.Combo(Enum.GetValues<FixtureOrder>(), pad.Order, o => pad.Order = o), 110),
                    Ui.Field("LED velocity", Ui.Number(pad.LedVelocity, v => pad.LedVelocity = (int)v, 1, 127, 1, "0"), 110),
                    TargetPicker.Build(state.Project, pad.FixtureIds, pad.GroupIds),
                    MidiLearnRow.Build(session, () => pad.Midi, b => pad.Midi = b),
                    done,
                },
            },
        };
        window.Closed += (_, _) =>
        {
            session.Learn(null);
            state.Commit();
        };
        return window.ShowDialog(owner);
    }

    private static void FillParameters(StackPanel panel, LivePad pad, InspectorFields fields)
    {
        panel.Children.Clear();
        foreach (var parameter in EffectParameterTable.For(pad.Effect))
            panel.Children.Add(Ui.Field(parameter.Label, parameter.CreateEditor(pad.Effect, fields), 110));
    }
}
