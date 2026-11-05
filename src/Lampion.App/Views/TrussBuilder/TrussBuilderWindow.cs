using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Lampion.App.Editor;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Stage;

namespace Lampion.App.Views.TrussBuilder;

/// <summary>
/// Stage → Truss structure: pick an arch or a cube, size it in metres, choose the lights hung on it, and see
/// a to-scale sketch update as you go. Build puts it all up as one undo step.
/// </summary>
internal sealed class TrussBuilderWindow : Window
{
    private const double LabelWidth = 110;

    private readonly EditorState _state;
    private readonly TrussBuildSettings _settings = new();
    private readonly TrussSketch _sketch = new() { Height = 220 };
    private readonly Grid _depthRow;
    private readonly Grid _spansRow;

    /// <param name="state">The project to build into.</param>
    /// <param name="near">Where the structure starts out, snapped to 0.5 m.</param>
    public TrussBuilderWindow(EditorState state, Vec3 near)
    {
        _state = state;
        Title = "Truss Structure";
        Width = 640;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _settings.CentreX = StageMath.Snap(near.X, 0.5);
        _settings.CentreZ = StageMath.Snap(near.Z, 0.5);
        var profiles = state.Project.Profiles;
        _settings.Profile = profiles.FirstOrDefault(p => p.Id == BuiltInProfiles.RgbwParId) ?? profiles.FirstOrDefault();

        var name = Ui.Text(_settings.Name, v => _settings.Name = string.IsNullOrWhiteSpace(v) ? DefaultName(_settings.Shape) : v.Trim());
        var shape = Ui.Combo(Enum.GetValues<TrussShape>(), _settings.Shape, v =>
        {
            if (name.Text == DefaultName(_settings.Shape))
                name.Text = _settings.Name = DefaultName(v);
            _settings.Shape = v;
            Changed();
        }, v => v == TrussShape.Arch ? "Arch (goalpost)" : "Cube (ground support)");

        _depthRow = Row("Depth (m)", Metres(_settings.Depth, 0.9, 50, v => _settings.Depth = v));
        _spansRow = Row("Lit spans", Ui.Combo(Enum.GetValues<LightSpans>(), _settings.Spans, v =>
        {
            _settings.Spans = v;
            Changed();
        }, v => v switch { LightSpans.Front => "Front only", LightSpans.FrontAndBack => "Front and back", _ => "All four sides" }));

        var noLights = new FixtureProfile { Model = "No lights" };
        var lightProfile = Ui.Combo([noLights, .. profiles], _settings.Profile ?? noLights, v =>
        {
            _settings.Profile = ReferenceEquals(v, noLights) ? null : v;
            Changed();
        }, p => p.Model);

        var ok = new Button { Content = "Build", MinWidth = 90, IsDefault = true };
        var cancel = new Button { Content = "Cancel", MinWidth = 90, IsCancel = true };
        ok.Click += (_, _) =>
        {
            Result = TrussRigger.Rig(_state, _settings);
            Close();
        };
        cancel.Click += (_, _) => Close();

        var form = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                Ui.Section("Structure"),
                Row("Shape", shape),
                Row("Name", name),
                Row("Width (m)", Metres(_settings.Width, 0.9, 100, v => _settings.Width = v)),
                _depthRow,
                Row("Height (m)", Metres(_settings.Height, 0.6, 30, v => _settings.Height = v)),
                Ui.Section("Placement"),
                Row("Centre X (m)", Metres(_settings.CentreX, -200, 200, v => _settings.CentreX = v)),
                Row("Centre Z (m)", Metres(_settings.CentreZ, -200, 200, v => _settings.CentreZ = v)),
                Row("Turn (°)", Ui.Number(_settings.Yaw, v => { _settings.Yaw = v; Changed(); }, -360, 360, 5, "0.#")),
                Ui.Section("Lights"),
                Row("Fixture", lightProfile),
                Row("Per span", Ui.Number(_settings.LightsPerSpan, v => { _settings.LightsPerSpan = (int)v; Changed(); }, 0, 64, 1, "0")),
                _spansRow,
            },
        };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0), Children = { ok, cancel } };
        var sketchFrame = new Border { Background = Ui.Brush("#1A1A1C"), CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 0, 0, 12), Child = _sketch };
        Content = new StackPanel { Margin = new Thickness(16), Children = { sketchFrame, form, buttons } };
        Changed();
    }

    /// <summary>The status-bar summary after Build, or null when cancelled.</summary>
    public string? Result { get; private set; }

    private static string DefaultName(TrussShape shape) => shape == TrussShape.Arch ? "Arch" : "Truss cube";

    private static Grid Row(string label, Control editor) => Ui.Field(label, editor, LabelWidth);

    private NumericUpDown Metres(double value, double min, double max, Action<double> set) =>
        Ui.Number(value, v =>
        {
            set(v);
            Changed();
        }, min, max, 0.25, "0.00");

    private void Changed()
    {
        var cube = _settings.Shape == TrussShape.Cube;
        _depthRow.IsVisible = cube;
        _spansRow.IsVisible = cube && _settings.HasLights;
        _sketch.Show(_settings);
    }
}
