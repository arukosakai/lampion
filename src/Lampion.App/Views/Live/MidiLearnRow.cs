using Avalonia.Controls;
using Lampion.App.Live;
using Lampion.Core.Model.Live;

namespace Lampion.App.Views.Live;

/// <summary>The "MIDI" row of the fader and pad editors: the binding, Learn and Clear.</summary>
internal static class MidiLearnRow
{
    /// <summary>
    /// A row captioned <paramref name="label"/> showing <paramref name="get"/>'s binding; Learn waits for the next control or
    /// note, then calls <paramref name="set"/>.
    /// </summary>
    public static Control Build(LiveSession session, Func<MidiBinding?> get, Action<MidiBinding?> set, string label = "MIDI")
    {
        var binding = Ui.Label(Describe(get()), null);
        binding.MinWidth = 130;
        binding.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        Button? learn = null;
        learn = Ui.Button("Learn", () =>
        {
            binding.Text = "Move a control or hit a pad…";
            session.Learn(learned =>
            {
                set(learned);
                binding.Text = Describe(learned);
            });
        }, "Binds the next slider, knob, pad or key that arrives on the MIDI input");
        var clear = Ui.Button("Clear", () =>
        {
            session.Learn(null);
            set(null);
            binding.Text = Describe(null);
        }, "Remove the MIDI binding");
        return Ui.Field(label, Ui.Row(binding, learn, clear), 110);
    }

    private static string Describe(MidiBinding? binding) => binding?.ToString() ?? "Not mapped";
}
