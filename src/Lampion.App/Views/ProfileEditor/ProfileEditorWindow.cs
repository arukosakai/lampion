using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Lampion.App.Editor;
using Lampion.Core.Model.Rig;

namespace Lampion.App.Views.ProfileEditor;

/// <summary>
/// The profile editor window: identity, optics and channels on the left; preview, DMX footprint and tester on
/// the right. It edits a copy and only puts it in the project on Save.
/// </summary>
internal sealed class ProfileEditorWindow : Window
{
    private readonly EditorState _state;
    private readonly ProfileEditSession _session;

    /// <param name="state">The project the profile is saved into, and where live test values are held.</param>
    /// <param name="profile">The profile to edit; left untouched unless the user saves.</param>
    /// <param name="isNew">True for a profile not yet in the project (titles and the Create button).</param>
    public ProfileEditorWindow(EditorState state, FixtureProfile profile, bool isNew)
    {
        _state = state;
        _session = new ProfileEditSession(state, profile);
        Title = isNew ? "New Fixture Profile" : $"Fixture Profile — {profile.Manufacturer} {profile.Model}";
        Width = 1240;
        Height = 840;
        MinWidth = 980;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var editor = new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(18, 14, 18, 14),
            Children = { new ProfileIdentitySection(_session), new ChannelListEditor(_session) },
        };
        var scroller = new ScrollViewer { Content = editor };
        // Opening focuses the last field, and the scroller follows focus; start at the top instead.
        Opened += (_, _) => Dispatcher.UIThread.Post(() => scroller.Offset = default, DispatcherPriority.Background);

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,500") };
        body.Children.Add(scroller);
        ProfileEditorUi.Place(body, SidePanel(), 1);

        var footer = new ProfileFooter(_session, isNew, Save, Close);
        var root = new DockPanel();
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        root.Children.Add(body);
        Content = root;

        // However the window goes (Save, Cancel, ✕), the patched fixtures go back to the show.
        Closed += (_, _) => state.Test = null;
        _session.NotifyChannelsChanged();
    }

    /// <summary>True once the user saved; the edited profile is then in the project.</summary>
    public bool Saved { get; private set; }

    private Control SidePanel()
    {
        var preview = new ProfilePreviewSection(_session);
        var tester = new ProfileTesterSection(_state, _session);
        preview.Aimed += tester.SetPanTilt;
        var bottom = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 10, 0, 0),
            Children = { ProfileEditorUi.Heading("DMX footprint"), new FootprintRibbon(_session), tester },
        };
        var panel = new DockPanel { Margin = new Thickness(16, 14, 16, 14) };
        DockPanel.SetDock(preview.Header, Dock.Top);
        DockPanel.SetDock(bottom, Dock.Bottom);
        panel.Children.Add(preview.Header);
        panel.Children.Add(bottom);
        panel.Children.Add(preview.View);
        return new Border { Background = ProfileEditorUi.PanelBackground, BorderBrush = Brushes.Black, BorderThickness = new Thickness(1, 0, 0, 0), Child = panel };
    }

    private void Save()
    {
        var profiles = _state.Project.Profiles;
        var working = _session.Working;
        var index = profiles.FindIndex(p => p.Id == working.Id);
        if (index >= 0)
            profiles[index] = working;
        else
            profiles.Add(working);
        _state.Commit();
        Saved = true;
        Close();
    }
}
