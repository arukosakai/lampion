using Avalonia.Controls;
using Lampion.App.Live;
using Lampion.Core.Model.Live;

namespace Lampion.App.Views.Live;

/// <summary>The "MIDI" row of the fader and pad editors: the binding, Learn and Clear.</summary>
internal static class MidiLearnRow
{
    /// <summary>A row showing <paramref name="get"/>'s binding; Learn waits for the next control or note, then calls <paramref name="set"/>.</summary>
    public static Control Build(LiveSession session, Func<MidiBinding?> get, Action<MidiBinding?> set)
    {
        var label = Ui.Label(Describe(get()), null);
        label.MinWidth = 130;
        label.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        Button? learn = null;
        learn = Ui.Button("Learn", () =>
        {
            label.Text = "Move a control or hit a pad…";
            session.Learn(binding =>
            {
                set(binding);
                label.Text = Describe(binding);
            });
        }, "Binds the next slider, knob, pad or key that arrives on the MIDI input");
        var clear = Ui.Button("Clear", () =>
        {
            session.Learn(null);
            set(null);
            label.Text = Describe(null);
        }, "Remove the MIDI binding");
        return Ui.Field("MIDI", Ui.Row(label, learn, clear), 110);
    }

    private static string Describe(MidiBinding? binding) => binding?.ToString() ?? "Not mapped";
}
