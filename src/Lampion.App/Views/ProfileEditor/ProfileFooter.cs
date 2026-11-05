using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Lampion.App.Views.ProfileEditor;

/// <summary>
/// The profile editor's bottom bar: Cancel and Save/Create, and a note on how many patched fixtures use the
/// profile, turning into a warning when the edit changes their footprint.
/// </summary>
internal sealed class ProfileFooter : Border
{
    private readonly ProfileEditSession _session;
    private readonly TextBlock _note = new() { VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };

    /// <param name="session">Supplies the patched fixtures and the footprint before and after.</param>
    /// <param name="isNew">Labels the confirm button Create instead of Save.</param>
    /// <param name="save">Runs when the confirm button (or Enter) is pressed.</param>
    /// <param name="cancel">Runs when Cancel (or Escape) is pressed.</param>
    public ProfileFooter(ProfileEditSession session, bool isNew, Action save, Action cancel)
    {
        _session = session;
        var ok = new Button { Content = isNew ? "Create" : "Save", MinWidth = 96, IsDefault = true };
        ok.Classes.Add("accent");
        ok.Click += (_, _) => save();
        var cancelButton = new Button { Content = "Cancel", MinWidth = 96, IsCancel = true };
        cancelButton.Click += (_, _) => cancel();

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { cancelButton, ok } };
        var row = new DockPanel { Margin = new Thickness(18, 10), LastChildFill = true };
        DockPanel.SetDock(buttons, Dock.Right);
        row.Children.Add(buttons);
        row.Children.Add(_note);

        Background = ProfileEditorUi.PanelBackground;
        BorderBrush = Brushes.Black;
        BorderThickness = new Thickness(0, 1, 0, 0);
        Child = row;

        session.ChannelsChanged += Refresh;
    }

    private void Refresh()
    {
        var users = _session.Patched.Count;
        var before = _session.Original.Channels.Count;
        var after = _session.Working.Channels.Count;
        _note.Foreground = users > 0 && after != before ? ProfileEditorUi.Warning : ProfileEditorUi.Muted;
        if (users == 0)
        {
            _note.Text = "";
            return;
        }
        var used = $"Used by {users} patched fixture{(users == 1 ? "" : "s")}.";
        _note.Text = after == before ? used : $"{used} The footprint changes from {before} to {after} channels, so check the patch for overlaps.";
    }
}
