using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lampion.App.Live;
using Lampion.App.Output;
using Lampion.Core.Live;
using Lampion.Core.Midi;

namespace Lampion.App.Views.Live;

/// <summary>
/// The bottom of the live window: MIDI device pickers and activity, DMX output on/off, and the engine's timing over
/// the last second, so a stall on show night is visible rather than felt.
/// </summary>
internal sealed class LiveStatusLine : StackPanel
{
    private readonly LiveSession _session;
    private readonly DmxOutput _output;
    private readonly TextBlock _engine = new() { FontSize = 11 };
    private readonly TextBlock _midi = new() { FontSize = 11, Foreground = Brushes.Gray, MinWidth = 150 };
    private readonly TextBlock _problem = new() { FontSize = 11, Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap };
    private readonly CheckBox _send = new() { Content = "Send DMX" };
    private readonly ContentControl _devices = new();
    private LiveLoopStats _shownStats;
    private string? _shownMidi;
    private string? _shownProblem;

    /// <summary>A status line for <paramref name="session"/> and <paramref name="output"/>.</summary>
    public LiveStatusLine(LiveSession session, DmxOutput output)
    {
        _session = session;
        _output = output;
        Spacing = 4;
        Margin = new Thickness(0, 8, 0, 0);
        _send.IsCheckedChanged += (_, _) =>
        {
            if ((_send.IsChecked == true) != _output.IsSending)
                _output.Toggle();
        };
        ToolTip.SetTip(_send, "Send to the device in Output → Output setup. Frames go straight from the live engine to the device.");
        FillDevices();
        Children.Add(Ui.Row(_send, _devices, Ui.Button("Rescan", FillDevices, "Look for MIDI devices again, e.g. after plugging one in"), _midi));
        Children.Add(_engine);
        Children.Add(_problem);
        _midi.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
    }

    /// <summary>Updates the readouts. Call every UI tick; text is rebuilt only when it changed.</summary>
    public void Refresh()
    {
        _send.IsChecked = _output.IsSending;
        var stats = _session.Stats;
        if (stats != _shownStats)
        {
            _shownStats = stats;
            var target = _session.Loop.Rate;
            _engine.Text = $"Engine {stats.Rate:0.0} Hz (target {target:0}) · render avg {stats.AverageRenderMs:0.000} ms, worst {stats.WorstRenderMs:0.000} ms · worst lateness {stats.WorstLateMs:0.00} ms · overruns {stats.Overruns}";
            _engine.Foreground = stats.Overruns > 0 || stats.Rate < target * 0.95 ? Brushes.IndianRed : Brushes.MediumSeaGreen;
        }
        var midi = _session.LastMidi is { } last ? "MIDI ● " + last : _session.IsLearning ? "Waiting for MIDI…" : "MIDI idle";
        if (!ReferenceEquals(midi, _shownMidi) && midi != _shownMidi)
            _midi.Text = _shownMidi = midi;
        var problem = _session.Problem ?? (_output.IsSending ? _output.Problem : null);
        if (problem != _shownProblem)
        {
            _shownProblem = problem;
            _problem.Text = problem;
            _problem.IsVisible = problem is not null;
        }
    }

    private void FillDevices()
    {
        if (!MidiDevices.Supported)
        {
            _devices.Content = Ui.Label("MIDI controllers need Windows in this prototype; the on-screen console works everywhere.", null);
            return;
        }
        var inputs = new List<string> { "No MIDI input" };
        inputs.AddRange(MidiDevices.InputNames());
        var outputs = new List<string> { "No pad lights" };
        outputs.AddRange(MidiDevices.OutputNames());
        var input = Ui.Combo(Enumerable.Range(0, inputs.Count), _session.MidiIn is null ? 0 : inputs.IndexOf(_session.MidiIn.Name), i => Open(i - 1, null), i => inputs[i]);
        var output = Ui.Combo(Enumerable.Range(0, outputs.Count), _session.MidiOut is null ? 0 : outputs.IndexOf(_session.MidiOut.Name), o => Open(null, o - 1), o => outputs[o]);
        input.MinWidth = output.MinWidth = 170;
        ToolTip.SetTip(input, "The controller whose sliders and pads drive the console");
        ToolTip.SetTip(output, "Where pad lights are sent (usually the same controller)");
        _devices.Content = Ui.Row(input, output);

        void Open(int? newInput, int? newOutput)
        {
            var currentIn = _session.MidiIn is { } i ? inputs.IndexOf(i.Name) - 1 : -1;
            var currentOut = _session.MidiOut is { } o ? outputs.IndexOf(o.Name) - 1 : -1;
            _session.OpenMidi(newInput ?? currentIn, newOutput ?? currentOut);
        }
    }
}
