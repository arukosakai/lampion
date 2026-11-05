using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.App.Editor;
using Lampion.Core.Model.Rig;

namespace Lampion.App.Views.NewFixtures;

/// <summary>
/// The New Fixtures window, styled after Premiere's New Sequence dialog: profiles on the left (with New and
/// Edit), what the selected one is on the right, and the patch settings below it.
/// </summary>
internal sealed class NewFixturesWindow : Window
{
    private readonly EditorState _state;
    private readonly ListBox _profiles = new() { SelectionMode = SelectionMode.Single };
    private readonly ProfileDetailsView _details = new();
    private readonly PatchSettingsForm _settings;

    /// <param name="state">The project to patch into.</param>
    public NewFixturesWindow(EditorState state)
    {
        _state = state;
        Title = "New Fixtures";
        Width = 760;
        Height = 560;
        MinWidth = 640;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _settings = new PatchSettingsForm(state.Project);
        _profiles.SelectionChanged += (_, _) => ShowSelected();
        FillProfiles();

        var ok = new Button { Content = "OK", MinWidth = 90, IsDefault = true };
        var cancel = new Button { Content = "Cancel", MinWidth = 90, IsCancel = true };
        cancel.Click += (_, _) => Close();
        ok.Click += (_, _) => Confirm();

        var right = new DockPanel { Margin = new Thickness(16, 0, 0, 0) };
        DockPanel.SetDock(_details, Dock.Top);
        right.Children.Add(_details);
        right.Children.Add(_settings);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0), Children = { ok, cancel } };
        var left = ProfileColumn();
        var root = new DockPanel { Margin = new Thickness(16) };
        DockPanel.SetDock(buttons, Dock.Bottom);
        DockPanel.SetDock(left, Dock.Left);
        root.Children.Add(buttons);
        root.Children.Add(left);
        root.Children.Add(right);
        Content = root;
    }

    /// <summary>Ids of the fixtures patched on OK, in patch order; null when the window was cancelled.</summary>
    public List<string>? Added { get; private set; }

    private FixtureProfile? Selected
    {
        get
        {
            var profiles = _state.Project.Profiles;
            var index = _profiles.SelectedIndex;
            return index >= 0 && index < profiles.Count ? profiles[index] : null;
        }
    }

    private Control ProfileColumn()
    {
        var newProfile = Ui.Button("New profile…", async () =>
        {
            var created = ProfileEditorDialog.NewCustomProfile();
            if (await ProfileEditorDialog.Show(this, _state, created, isNew: true))
                FillProfiles(created.Id);
        });
        var editProfile = Ui.Button("Edit…", async () =>
        {
            if (Selected is { } profile && await ProfileEditorDialog.Show(this, _state, profile, isNew: false))
                FillProfiles(profile.Id);
        });

        var column = new DockPanel { Width = 250 };
        var buttons = Ui.Row(newProfile, editProfile);
        buttons.Margin = new Thickness(0, 6, 0, 0);
        DockPanel.SetDock(buttons, Dock.Bottom);
        var label = Ui.Section("Fixture profiles");
        DockPanel.SetDock(label, Dock.Top);
        column.Children.Add(label);
        column.Children.Add(buttons);
        column.Children.Add(new Border { BorderBrush = Brushes.Black, BorderThickness = new Thickness(1), Child = _profiles });
        return column;
    }

    private void FillProfiles(string? select = null)
    {
        var profiles = _state.Project.Profiles;
        _profiles.Items.Clear();
        foreach (var p in profiles)
        {
            var title = new TextBlock { Text = p.Model, FontWeight = FontWeight.SemiBold };
            var summary = new TextBlock { Text = $"{p.Manufacturer} · {p.Channels.Count} ch · {p.Beam}", Foreground = Brushes.Gray, FontSize = 11 };
            _profiles.Items.Add(new ListBoxItem { Content = Ui.Column(0, title, summary) });
        }
        var index = select is null ? 0 : profiles.FindIndex(p => p.Id == select);
        _profiles.SelectedIndex = Math.Max(0, index);
    }

    private void ShowSelected()
    {
        var profile = Selected;
        _settings.UseProfile(profile);
        _details.Show(profile);
    }

    private void Confirm()
    {
        if (Selected is not { } profile)
            return;
        Added = _settings.Patch(_state, profile);
        Close();
    }
}
