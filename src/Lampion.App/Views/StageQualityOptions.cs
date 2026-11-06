using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.App.Controls;
using Lampion.App.Editor;
using Lampion.App.Rendering;

namespace Lampion.App.Views;

/// <summary>
/// How hard the GPU works on the 3D view. A preset sets every knob; touching one knob makes it "Custom".
/// Saved per machine, since the right answer depends on the graphics card, not the show. Loads the saved
/// settings into the view when it is built.
/// </summary>
internal sealed partial class StageQualityOptions : StackPanel
{
    private const string Custom = "Custom";

    private readonly StageView _view;
    private readonly ComboBox _preset;
    private readonly Knob<double> _resolution;
    private readonly Knob<int> _samples;
    private readonly Knob<int> _hazeSteps;
    private readonly Knob<int> _lights;
    private bool _syncing;

    /// <summary>The quality section for <paramref name="view"/>'s settings menu.</summary>
    public StageQualityOptions(StageView view)
    {
        _view = view;
        var settings = UserData.LoadSettings();
        view.Fidelity = settings.Fidelity;
        view.ShowFps = settings.ShowFps;

        _resolution = new Knob<double>([(0.5, "50%"), (0.75, "75%"), (1, "100%")], view.Fidelity.RenderScale, v => view.Fidelity = view.Fidelity with { RenderScale = v });
        _samples = new Knob<int>([(0, "Off"), (2, "2×"), (4, "4×")], view.Fidelity.Samples, v => view.Fidelity = view.Fidelity with { Samples = v });
        _hazeSteps = new Knob<int>([(8, "Low"), (16, "Medium"), (Shaders.MaxHazeSteps, "High")], view.Fidelity.HazeSteps, v => view.Fidelity = view.Fidelity with { HazeSteps = v });
        _lights = new Knob<int>([(8, "8"), (24, "24"), (Shaders.MaxLights, $"{Shaders.MaxLights}")], view.Fidelity.MaxLights, v => view.Fidelity = view.Fidelity with { MaxLights = v });
        ToolTip.SetTip(_resolution.Combo, "Renders fewer pixels and scales the picture up: the biggest saving, at the cost of sharpness");
        ToolTip.SetTip(_samples.Combo, "Smooths jagged edges on trusses and beams");
        ToolTip.SetTip(_hazeSteps.Combo, "Detail of the haze inside beams; lower is grainier, not dimmer");
        ToolTip.SetTip(_lights.Combo, "How many of the brightest fixtures light the floor and set");

        var presetNames = new List<string>();
        foreach (var (name, _) in StageFidelity.Presets)
            presetNames.Add(name);
        presetNames.Add(Custom);
        _preset = new ComboBox { ItemsSource = presetNames, HorizontalAlignment = HorizontalAlignment.Stretch };
        _preset.SelectionChanged += (_, _) => PresetChosen();

        var fps = new CheckBox { Content = "Show frame rate", IsChecked = view.ShowFps };
        fps.IsCheckedChanged += (_, _) =>
        {
            view.ShowFps = fps.IsChecked == true;
            Save();
        };

        _resolution.Changed += KnobChanged;
        _samples.Changed += KnobChanged;
        _hazeSteps.Changed += KnobChanged;
        _lights.Changed += KnobChanged;
        Quietly(ShowPreset);

        Spacing = 4;
        Margin = new Thickness(0, 10, 0, 0);
        Children.Add(new TextBlock { Text = "STAGE QUALITY", Foreground = Brushes.Gray, FontSize = 10.5, FontWeight = FontWeight.SemiBold });
        Children.Add(Ui.Field("Preset", _preset, 110));
        Children.Add(Ui.Field("Resolution", _resolution.Combo, 110));
        Children.Add(Ui.Field("Anti-aliasing", _samples.Combo, 110));
        Children.Add(Ui.Field("Haze detail", _hazeSteps.Combo, 110));
        Children.Add(Ui.Field("Lights / surface", _lights.Combo, 110));
        Children.Add(fps);
    }

    private void PresetChosen()
    {
        if (_syncing || _preset.SelectedItem is not string name || Find(name) is not { } chosen)
            return;
        _view.Fidelity = chosen;
        Quietly(() =>
        {
            _resolution.Select(chosen.RenderScale);
            _samples.Select(chosen.Samples);
            _hazeSteps.Select(chosen.HazeSteps);
            _lights.Select(chosen.MaxLights);
        });
        Save();
    }

    private void KnobChanged()
    {
        if (_syncing)
            return;
        Quietly(ShowPreset);
        Save();
    }

    private void ShowPreset() => _preset.SelectedItem = _view.Fidelity.PresetName ?? Custom;

    private void Quietly(Action change)
    {
        _syncing = true;
        change();
        _syncing = false;
    }

    private static StageFidelity? Find(string name)
    {
        foreach (var (presetName, preset) in StageFidelity.Presets)
            if (presetName == name)
                return preset;
        return null;
    }

    private void Save() => UserData.SaveSettings(UserData.LoadSettings() with { Fidelity = _view.Fidelity, ShowFps = _view.ShowFps });
}
