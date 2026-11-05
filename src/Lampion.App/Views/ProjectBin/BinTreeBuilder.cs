using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.App.Editor;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Views.ProjectBin;

/// <summary>
/// Turns the project into the bin's rows: Groups (with their members), Fixtures by address, Fixture profiles
/// and Media, each filtered by the search text. Remembers which folders the user opened across rebuilds.
/// </summary>
internal sealed class BinTreeBuilder
{
    // Static so twirl-downs survive reopening a project, as AE's Project panel does.
    private static readonly Dictionary<string, bool> Expanded = new() { ["groups"] = true, ["fixtures"] = true };

    private readonly EditorState _state;
    private readonly BinContextMenus _menus;
    private readonly BinCommands _commands;

    /// <param name="state">Supplies the project to show.</param>
    /// <param name="menus">Gives each row its right-click menu.</param>
    /// <param name="commands">Runs what double-clicking a row does.</param>
    public BinTreeBuilder(EditorState state, BinContextMenus menus, BinCommands commands)
    {
        _state = state;
        _menus = menus;
        _commands = commands;
    }

    /// <summary>Adds the folders and their rows to <paramref name="items"/>, which should be empty.</summary>
    /// <param name="items">The tree's top-level items.</param>
    /// <param name="filter">Trimmed search text; empty shows everything. With a filter, empty folders are hidden and the rest open.</param>
    public void Fill(ItemCollection items, string filter)
    {
        var project = _state.Project;
        bool Match(string name) => filter.Length == 0 || name.Contains(filter, StringComparison.OrdinalIgnoreCase);

        var groups = Folder("groups", "Groups", project.Groups.Count);
        foreach (var group in project.Groups)
        {
            var members = group.FixtureIds.Select(project.FixtureById).OfType<Fixture>().ToList();
            if (!Match(group.Name) && !members.Any(m => Match(m.Name)))
                continue;
            var node = Node(new BinItem(BinItemKind.Group, group.Id), Chip(group.Color), group.Name, $"{members.Count}", $"group:{group.Id}");
            foreach (var fixture in members.Where(m => filter.Length == 0 || Match(m.Name) || Match(group.Name)))
                node.Items.Add(FixtureNode(fixture));
            groups.Items.Add(node);
        }
        AddFolder(items, groups, filter);

        var fixtures = Folder("fixtures", "Fixtures", project.Fixtures.Count);
        foreach (var fixture in project.Fixtures.OrderBy(f => f.Universe).ThenBy(f => f.Address).Where(f => Match(f.Name)))
            fixtures.Items.Add(FixtureNode(fixture));
        AddFolder(items, fixtures, filter);

        var profiles = Folder("profiles", "Fixture profiles", project.Profiles.Count);
        foreach (var profile in project.Profiles.Where(p => Match(p.Model)))
        {
            var used = project.Fixtures.Count(f => f.ProfileId == profile.Id);
            var info = used > 0 ? $"{profile.Channels.Count} ch · {used}×" : $"{profile.Channels.Count} ch";
            profiles.Items.Add(Node(new BinItem(BinItemKind.Profile, profile.Id), Glyph("⚙"), profile.Model, info));
        }
        AddFolder(items, profiles, filter);

        var media = project.Tracks.Where(t => t.Kind != TrackKind.Light).SelectMany(t => t.Clips.OfType<MediaClip>()).ToList();
        var mediaFolder = Folder("media", "Media", media.Count);
        foreach (var clip in media.Where(m => Match(m.Name)))
        {
            var isVideo = project.Tracks.Any(t => t.Kind == TrackKind.Video && t.Clips.Contains(clip));
            var info = File.Exists(clip.Path) ? $"{clip.Duration:0.0} s" : "missing";
            mediaFolder.Items.Add(Node(new BinItem(BinItemKind.Media, clip.Id), Glyph(isVideo ? "▶" : "♪"), clip.Name, info));
        }
        AddFolder(items, mediaFolder, filter);
    }

    private static void AddFolder(ItemCollection items, TreeViewItem folder, string filter)
    {
        if (filter.Length > 0 && folder.Items.Count == 0)
            return;
        if (filter.Length > 0)
            folder.IsExpanded = true;
        items.Add(folder);
    }

    private static TreeViewItem Folder(string key, string name, int count)
    {
        var folder = new TreeViewItem
        {
            Header = Header(Glyph("📁"), name, $"{count}", bold: true),
            Tag = new BinItem(BinItemKind.Folder, key),
        };
        RememberExpansion(folder, key);
        return folder;
    }

    private TreeViewItem FixtureNode(Fixture fixture)
    {
        var profile = _state.Project.ProfileOf(fixture);
        return Node(new BinItem(BinItemKind.Fixture, fixture.Id), Glyph("💡"), fixture.Name, $"{fixture.Universe}.{fixture.Address:000}", tooltip: profile?.DisplayName);
    }

    private TreeViewItem Node(BinItem item, Control icon, string name, string info, string? expandKey = null, string? tooltip = null)
    {
        var header = Header(icon, name, info);
        if (tooltip is not null)
            ToolTip.SetTip(header, tooltip);
        var node = new TreeViewItem { Header = header, Tag = item, ContextMenu = _menus.For(item) };
        if (expandKey is not null)
            RememberExpansion(node, expandKey);
        node.DoubleTapped += (_, e) =>
        {
            e.Handled = true;
            _commands.Open(item);
        };
        return node;
    }

    private static void RememberExpansion(TreeViewItem node, string key)
    {
        node.IsExpanded = Expanded.GetValueOrDefault(key);
        node.PropertyChanged += (_, e) =>
        {
            if (e.Property == TreeViewItem.IsExpandedProperty)
                Expanded[key] = node.IsExpanded;
        };
    }

    private static Control Header(Control icon, string name, string info, bool bold = false)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("22,*,Auto"), MinHeight = 20 };
        icon.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(icon);
        var label = new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal };
        Grid.SetColumn(label, 1);
        grid.Children.Add(label);
        var meta = new TextBlock { Text = info, Foreground = Brushes.Gray, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0) };
        Grid.SetColumn(meta, 2);
        grid.Children.Add(meta);
        return grid;
    }

    private static TextBlock Glyph(string glyph) => new() { Text = glyph, FontSize = 12 };

    private static Control Chip(string color) => new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(3), Background = Ui.Brush(color) };
}
