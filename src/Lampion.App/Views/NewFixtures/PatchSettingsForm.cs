using Avalonia.Controls;
using Avalonia.Media;
using Lampion.App.Editor;
using Lampion.Core.Model;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Stage;

namespace Lampion.App.Views.NewFixtures;

/// <summary>
/// The choices that matter when patching: name, quantity, universe, start address (or next free), where they
/// hang and whether they become a group, with a live readout of the addresses they will take.
/// </summary>
internal sealed class PatchSettingsForm : StackPanel
{
    private readonly Project _project;
    private readonly NumericUpDown _quantity = Ui.Number(4, _ => { }, 1, 256, 1, "0");
    private readonly NumericUpDown _universe = Ui.Number(1, _ => { }, 1, 64, 1, "0");
    private readonly NumericUpDown _startAddress = Ui.Number(1, _ => { }, 1, 512, 1, "0");
    private readonly CheckBox _auto = new() { Content = "Next free", IsChecked = true };
    private readonly TextBox _name = new();
    private readonly List<(string Label, StageObject? Truss)> _placements;
    private readonly ComboBox _placement;
    private readonly CheckBox _makeGroup = new() { Content = "Put them in a new group", IsChecked = true };
    private readonly TextBlock _range = new() { Foreground = Brushes.Gray, FontSize = 11 };
    private FixtureProfile? _profile;

    /// <param name="project">The project being patched: its free addresses and trusses.</param>
    public PatchSettingsForm(Project project)
    {
        _project = project;
        Spacing = 4;
        _placements = [("In a row at 5.5 m", null)];
        _placements.AddRange(project.Stage.Objects.Where(o => o.Kind == StageObjectKind.Truss).Select(t => ($"Hung on {t.Name}", (StageObject?)t)));
        _placement = Ui.Combo(_placements, _placements[^1], _ => { }, p => p.Label);

        _quantity.ValueChanged += (_, _) => UpdateRange();
        _universe.ValueChanged += (_, _) => UpdateRange();
        _startAddress.ValueChanged += (_, _) =>
        {
            if (_startAddress.IsKeyboardFocusWithin)
                _auto.IsChecked = false;
            UpdateRange();
        };
        _auto.IsCheckedChanged += (_, _) => UpdateRange();

        Children.Add(Ui.Section("Patch"));
        Children.Add(Ui.Field("Name", _name, 110));
        Children.Add(Ui.Field("Quantity", _quantity, 110));
        Children.Add(Ui.Field("Universe", _universe, 110));
        Children.Add(Ui.Field("Start address", Ui.Row(_startAddress, _auto), 110));
        Children.Add(_range);
        Children.Add(Ui.Section("Placement"));
        Children.Add(Ui.Field("Where", _placement, 110));
        Children.Add(_makeGroup);
    }

    /// <summary>
    /// Switches to <paramref name="profile"/>. The name follows the profile's model unless the user typed one
    /// of their own; the address range is recalculated.
    /// </summary>
    public void UseProfile(FixtureProfile? profile)
    {
        _profile = profile;
        if (profile is null)
            return;
        if (string.IsNullOrWhiteSpace(_name.Text) || _project.Profiles.Any(p => p.Model == _name.Text))
            _name.Text = profile.Model;
        UpdateRange();
    }

    /// <summary>Patches the fixtures as set up (see <see cref="FixturePatcher.Patch"/>) and returns their ids.</summary>
    public List<string> Patch(EditorState state, FixtureProfile profile)
    {
        var truss = _placements[Math.Max(0, _placement.SelectedIndex)].Truss;
        var start = _auto.IsChecked == true ? (int?)null : (int)(_startAddress.Value ?? 1);
        return FixturePatcher.Patch(state, profile, (int)(_quantity.Value ?? 1), (int)(_universe.Value ?? 1), start, _name.Text ?? profile.Model, truss, _makeGroup.IsChecked == true);
    }

    private void UpdateRange()
    {
        if (_profile is not { } profile)
        {
            _range.Text = "";
            return;
        }
        var count = (int)(_quantity.Value ?? 1);
        var universe = (int)(_universe.Value ?? 1);
        var footprint = profile.Channels.Count;
        if (_auto.IsChecked == true)
            _startAddress.Value = Math.Max(1, PatchPlanner.NextFreeAddress(_project, universe, footprint));
        var start = (int)(_startAddress.Value ?? 1);
        var end = start + count * footprint - 1;
        _range.Text = end > 512
            ? $"⚠ {count} × {footprint} ch from {universe}.{start:000} runs past channel 512 — later ones move to the next universe"
            : $"Patches {universe}.{start:000} – {universe}.{end:000}";
    }
}
