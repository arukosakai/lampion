using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.App.Editor;
using Lampion.Core.Model;

namespace Lampion.App.Views;

/// <summary>
/// Patching new fixtures, styled after Premiere's New Sequence dialog: presets on the left, what the
/// selected one is on the right, and the few choices that matter below.
/// </summary>
public static class NewFixturesDialog
{
    public static async Task<List<string>?> Show(Window owner, EditorState state)
    {
        var project = state.Project;
        var window = new Window
        {
            Title = "New Fixtures",
            Width = 760,
            Height = 560,
            MinWidth = 640,
            MinHeight = 480,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        List<string>? added = null;

        var profiles = new ListBox { SelectionMode = SelectionMode.Single };
        var details = new StackPanel { Spacing = 4 };
        var quantity = Ui.Number(4, _ => { }, 1, 256, 1, "0");
        var universe = Ui.Number(1, _ => { }, 1, 64, 1, "0");
        var startAddress = Ui.Number(1, _ => { }, 1, 512, 1, "0");
        var auto = new CheckBox { Content = "Next free", IsChecked = true };
        var prefix = new TextBox();
        var placementOptions = new List<(string Label, StageObject? Truss)> { ("In a row at 5.5 m", null) };
        placementOptions.AddRange(project.Stage.Objects.Where(o => o.Kind == StageObjectKind.Truss).Select(t => ($"Hung on {t.Name}", (StageObject?)t)));
        var placement = Ui.Combo(placementOptions, placementOptions[^1], _ => { }, p => p.Label);
        var makeGroup = new CheckBox { Content = "Put them in a new group", IsChecked = true };
        var range = new TextBlock { Foreground = Brushes.Gray, FontSize = 11 };

        FixtureProfile? Selected() => profiles.SelectedIndex >= 0 && profiles.SelectedIndex < project.Profiles.Count ? project.Profiles[profiles.SelectedIndex] : null;

        void FillProfiles(string? select = null)
        {
            profiles.Items.Clear();
            foreach (var p in project.Profiles)
                profiles.Items.Add(new ListBoxItem { Content = Ui.Column(0, new TextBlock { Text = p.Model, FontWeight = FontWeight.SemiBold }, new TextBlock { Text = $"{p.Manufacturer} · {p.Channels.Count} ch · {p.Beam}", Foreground = Brushes.Gray, FontSize = 11 }) });
            var index = select is null ? 0 : project.Profiles.FindIndex(p => p.Id == select);
            profiles.SelectedIndex = Math.Max(0, index);
        }

        void UpdateRange()
        {
            var profile = Selected();
            if (profile is null)
            {
                range.Text = "";
                return;
            }
            var count = (int)(quantity.Value ?? 1);
            var u = (int)(universe.Value ?? 1);
            if (auto.IsChecked == true)
            {
                var free = project.NextFreeAddress(u, profile.Channels.Count);
                startAddress.Value = Math.Max(1, free);
            }
            var start = (int)(startAddress.Value ?? 1);
            var end = start + count * profile.Channels.Count - 1;
            range.Text = end > 512 ? $"⚠ {count} × {profile.Channels.Count} ch from {u}.{start:000} runs past channel 512 — later ones move to the next universe" : $"Patches {u}.{start:000} – {u}.{end:000}";
        }

        void ShowDetails()
        {
            details.Children.Clear();
            var profile = Selected();
            if (profile is null)
                return;
            if (string.IsNullOrWhiteSpace(prefix.Text) || project.Profiles.Any(p => p.Model == prefix.Text))
                prefix.Text = profile.Model;
            details.Children.Add(new TextBlock { Text = $"{profile.Manufacturer} {profile.Model}", FontSize = 15, FontWeight = FontWeight.SemiBold });
            details.Children.Add(new TextBlock { Text = $"{profile.Mode} · {profile.Beam} · {profile.BeamAngle:0.#}° beam{(profile.HasPanTilt ? $" · pan {profile.PanRange:0}° / tilt {profile.TiltRange:0}°" : "")}", Foreground = Brushes.Gray, FontSize = 11 });
            var table = new Grid { ColumnDefinitions = new ColumnDefinitions("34,*,*,50"), Margin = new Thickness(0, 8, 0, 0) };
            void Cell(string text, int row, int column, IBrush? brush = null)
            {
                var block = new TextBlock { Text = text, Foreground = brush ?? Brushes.Gainsboro, FontSize = 12, Margin = new Thickness(0, 1) };
                Grid.SetRow(block, row);
                Grid.SetColumn(block, column);
                table.Children.Add(block);
            }
            table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Cell("Ch", 0, 0, Brushes.Gray);
            Cell("Name", 0, 1, Brushes.Gray);
            Cell("Role", 0, 2, Brushes.Gray);
            Cell("Default", 0, 3, Brushes.Gray);
            for (var i = 0; i < profile.Channels.Count; i++)
            {
                table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var c = profile.Channels[i];
                Cell($"{i + 1}", i + 1, 0);
                Cell(c.Name, i + 1, 1);
                Cell(c.Role.ToString(), i + 1, 2, c.Role is ChannelRole.Red ? Brushes.IndianRed : c.Role is ChannelRole.Green ? Brushes.LightGreen : c.Role is ChannelRole.Blue ? Brushes.CornflowerBlue : null);
                Cell($"{c.Default}", i + 1, 3);
            }
            details.Children.Add(new ScrollViewer { Content = table, MaxHeight = 220 });
            UpdateRange();
        }

        profiles.SelectionChanged += (_, _) => ShowDetails();
        quantity.ValueChanged += (_, _) => UpdateRange();
        universe.ValueChanged += (_, _) => UpdateRange();
        startAddress.ValueChanged += (_, _) =>
        {
            if (startAddress.IsKeyboardFocusWithin)
                auto.IsChecked = false;
            UpdateRange();
        };
        auto.IsCheckedChanged += (_, _) => UpdateRange();
        FillProfiles();

        var newProfile = Ui.Button("New profile…", async () =>
        {
            var created = new FixtureProfile { Model = "Custom fixture", Channels = [new FixtureChannel { Name = "Dimmer", Role = ChannelRole.Dimmer }] };
            if (await ProfileEditorDialog.Show(window, state, created, isNew: true))
                FillProfiles(created.Id);
        });
        var editProfile = Ui.Button("Edit…", async () =>
        {
            if (Selected() is { } profile && await ProfileEditorDialog.Show(window, state, profile, isNew: false))
                FillProfiles(profile.Id);
        });

        var ok = new Button { Content = "OK", MinWidth = 90, IsDefault = true };
        var cancel = new Button { Content = "Cancel", MinWidth = 90, IsCancel = true };
        cancel.Click += (_, _) => window.Close();
        ok.Click += (_, _) =>
        {
            if (Selected() is not { } profile)
                return;
            var truss = placementOptions[Math.Max(0, placement.SelectedIndex)].Truss;
            added = Patch(state, profile, (int)(quantity.Value ?? 1), (int)(universe.Value ?? 1), auto.IsChecked == true ? null : (int)(startAddress.Value ?? 1), prefix.Text ?? profile.Model, truss, makeGroup.IsChecked == true);
            window.Close();
        };

        var left = new DockPanel { Width = 250 };
        var leftButtons = Ui.Row(newProfile, editProfile);
        leftButtons.Margin = new Thickness(0, 6, 0, 0);
        DockPanel.SetDock(leftButtons, Dock.Bottom);
        var presetsLabel = Ui.Section("Fixture profiles");
        DockPanel.SetDock(presetsLabel, Dock.Top);
        left.Children.Add(presetsLabel);
        left.Children.Add(leftButtons);
        left.Children.Add(new Border { BorderBrush = Brushes.Black, BorderThickness = new Thickness(1), Child = profiles });

        var settings = Ui.Column(
            4,
            Ui.Section("Patch"),
            Ui.Field("Name", prefix, 110),
            Ui.Field("Quantity", quantity, 110),
            Ui.Field("Universe", universe, 110),
            Ui.Field("Start address", Ui.Row(startAddress, auto), 110),
            range,
            Ui.Section("Placement"),
            Ui.Field("Where", placement, 110),
            makeGroup
        );
        var right = new DockPanel { Margin = new Thickness(16, 0, 0, 0) };
        DockPanel.SetDock(details, Dock.Top);
        right.Children.Add(details);
        right.Children.Add(settings);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0), Children = { ok, cancel } };
        var root = new DockPanel { Margin = new Thickness(16) };
        DockPanel.SetDock(buttons, Dock.Bottom);
        DockPanel.SetDock(left, Dock.Left);
        root.Children.Add(buttons);
        root.Children.Add(left);
        root.Children.Add(right);
        window.Content = root;
        await window.ShowDialog(owner);
        return added;
    }

    private static List<string> Patch(EditorState state, FixtureProfile profile, int count, int universe, int? startAddress, string name, StageObject? truss, bool group)
    {
        var project = state.Project;
        var fixtures = new List<Fixture>();
        var address = startAddress ?? project.NextFreeAddress(universe, profile.Channels.Count);
        for (var i = 0; i < count; i++)
        {
            // Continue in the next universe when this one is full, like a console's auto-patch.
            while (address < 0 || address + profile.Channels.Count - 1 > 512)
            {
                if (++universe > 64)
                    break;
                address = project.NextFreeAddress(universe, profile.Channels.Count);
            }
            if (universe > 64)
                break;
            var fixture = new Fixture
            {
                Name = count == 1 ? name : $"{name} {i + 1}",
                ProfileId = profile.Id,
                Universe = universe,
                Address = address,
                Position = new Vec3(-(count - 1) / 2.0 + i, 5.5, 0),
                Pitch = profile.HasPanTilt ? -90 : -60,
            };
            project.Fixtures.Add(fixture);
            fixtures.Add(fixture);
            address = project.NextFreeAddress(universe, profile.Channels.Count);
        }
        if (truss is not null)
            StageMath.DistributeAlong(truss, fixtures);
        if (group && fixtures.Count > 1)
        {
            string[] palette = ["#E6A23A", "#3AA0E6", "#E63A8E", "#5BD16A", "#B05BE6", "#E6E03A"];
            project.Groups.Add(new FixtureGroup { Name = name, Color = palette[project.Groups.Count % palette.Length], FixtureIds = fixtures.Select(f => f.Id).ToList() });
        }
        state.Commit();
        return fixtures.Select(f => f.Id).ToList();
    }
}
