using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Lampion.App.Views.ProfileEditor;

/// <summary>
/// The Channels part of the profile editor: a heading with the channel count, one <see cref="ChannelRowView"/>
/// per channel, the ＋ Add channel menu and a note on which roles the timeline fills in.
/// </summary>
internal sealed class ChannelListEditor : StackPanel
{
    private readonly ProfileEditSession _session;
    private readonly StackPanel _rows = new() { Spacing = 4 };
    private readonly List<ChannelRowView> _rowViews = [];
    private readonly TextBlock _count = new() { Foreground = ProfileEditorUi.Muted, VerticalAlignment = VerticalAlignment.Center };

    /// <param name="session">The editor session whose working profile is listed.</param>
    public ChannelListEditor(ProfileEditSession session)
    {
        _session = session;
        Spacing = 6;

        var title = ProfileEditorUi.Heading("Channels");
        title.Margin = new Thickness(0);
        var header = new DockPanel { Margin = new Thickness(0, 14, 0, 4) };
        DockPanel.SetDock(_count, Dock.Right);
        header.Children.Add(_count);
        header.Children.Add(title);

        var addMenu = new MenuFlyout();
        foreach (var role in RoleChips.All)
        {
            var item = new MenuItem { Header = RoleChips.Chip(role) };
            item.Click += (_, _) => session.AddChannel(role);
            addMenu.Items.Add(item);
        }
        var add = new Button { Content = "＋ Add channel ▾", Flyout = addMenu, Margin = new Thickness(0, 4, 0, 0) };

        Children.Add(header);
        Children.Add(_rows);
        Children.Add(add);
        Children.Add(
            new TextBlock
            {
                Text = "Dimmer, Red, Green, Blue and White are filled from colour on the timeline. Other roles get a lane in Effect Controls → Channels.",
                Foreground = ProfileEditorUi.Muted,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0),
            }
        );

        session.ChannelsChanged += Rebuild;
        session.ValuesChanged += ShowValues;
    }

    /// <summary>Rebuilds every row from the working profile.</summary>
    public void Rebuild()
    {
        var channels = _session.Working.Channels;
        _rows.Children.Clear();
        _rowViews.Clear();
        _rows.Children.Add(HeaderRow());
        for (var i = 0; i < channels.Count; i++)
        {
            var row = new ChannelRowView(_session, i);
            _rowViews.Add(row);
            _rows.Children.Add(row);
        }
        if (channels.Count == 0)
            _rows.Children.Add(new TextBlock { Text = "No channels yet. Add the first one below.", Foreground = ProfileEditorUi.Muted, Margin = new Thickness(4, 8) });
        _count.Text = channels.Count == 1 ? "1 channel" : $"{channels.Count} channels";
    }

    private void ShowValues()
    {
        var test = _session.Test;
        for (var i = 0; i < _rowViews.Count && i < test.Count; i++)
            _rowViews[i].ShowValue(test[i]);
    }

    private static Grid HeaderRow()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(ChannelRowView.Columns), ColumnSpacing = 8, Margin = new Thickness(8, 0, 6, 0) };
        string[] titles = ["#", "Name", "Role", "Default", "Preview value"];
        for (var i = 0; i < titles.Length; i++)
            ProfileEditorUi.Place(grid, new TextBlock { Text = titles[i], Foreground = ProfileEditorUi.Muted, FontSize = 11, HorizontalAlignment = i == 0 ? HorizontalAlignment.Center : HorizontalAlignment.Left }, i);
        return grid;
    }
}
