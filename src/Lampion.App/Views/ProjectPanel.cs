using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.App.Controls;
using Lampion.App.Editor;
using Lampion.Core.Model;

namespace Lampion.App.Views;

/// <summary>
/// The Project panel as a Premiere-style bin: one searchable tree of groups, fixtures, profiles and media,
/// an info header for the selection, and New / Delete at the bottom. Fixtures and groups drag onto the
/// timeline to make (or extend) light tracks.
/// </summary>
public sealed class ProjectPanel : DockPanel
{
    private enum ItemKind
    {
        Folder,
        Group,
        Fixture,
        Profile,
        Media,
    }

    private sealed record BinItem(ItemKind Kind, string Id);

    private static readonly Dictionary<string, bool> Expanded = new() { ["groups"] = true, ["fixtures"] = true };

    private readonly EditorState _state;
    private readonly TimelineControl _timeline;
    private readonly Func<Task> _importAudio;
    private readonly Func<Task> _importVideo;
    private readonly TreeView _tree = new() { SelectionMode = SelectionMode.Multiple };
    private readonly TextBox _search = new() { PlaceholderText = "🔍  Search", MinHeight = 24, Margin = new Thickness(6, 4) };
    private readonly ContentControl _info = new() { Margin = new Thickness(8, 6), MinHeight = 54 };
    private readonly TextBlock _count = new() { Foreground = Brushes.Gray, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
    private string _signature = "";
    private bool _syncing;

    public ProjectPanel(EditorState state, TimelineControl timeline, Func<Task> importAudio, Func<Task> importVideo)
    {
        _state = state;
        _timeline = timeline;
        _importAudio = importAudio;
        _importVideo = importVideo;

        var top = new StackPanel { Children = { _info, _search } };
        SetDock(top, Dock.Top);
        Children.Add(top);
        var toolbar = Toolbar();
        SetDock(toolbar, Dock.Bottom);
        Children.Add(toolbar);
        Children.Add(new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x14)), BorderThickness = new Thickness(0, 1), Child = _tree });

        _search.TextChanged += (_, _) => Refresh(force: true);
        _tree.SelectionChanged += (_, _) => OnTreeSelection();
        _tree.AddHandler(PointerPressedEvent, OnTreePressed, RoutingStrategies.Bubble, handledEventsToo: true);
        _tree.KeyDown += (_, e) =>
        {
            if (e.Key == Key.F2 && Primary() is { } item)
                _ = Rename(item);
        };
        state.Changed += () => Refresh(force: false);
        state.ProjectReplaced += () => Refresh(force: true);
        state.SelectionChanged += SyncFromState;
        Refresh(force: true);
    }

    // ---- Toolbar ---------------------------------------------------------------------------------------

    private Control Toolbar()
    {
        var menu = new MenuFlyout();
        void Add(string header, Action action)
        {
            var item = new MenuItem { Header = header };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }
        Add("Fixtures…", () => _ = NewFixtures());
        Add("Group from selection", MakeGroup);
        Add("Fixture profile…", () => _ = NewProfile());
        menu.Items.Add(new Separator());
        Add("Import audio…", () => _ = _importAudio());
        Add("Import video…", () => _ = _importVideo());

        var newButton = new Button { Content = "＋ New", Flyout = menu, Padding = new Thickness(8, 2) };
        ToolTip.SetTip(newButton, "New fixtures, groups, profiles, or import media");
        var delete = new GlyphButton("🗑", "Delete the selected items (Del)", DeleteSelected);
        var bar = new DockPanel { Margin = new Thickness(6, 4), LastChildFill = false };
        SetDock(newButton, Dock.Left);
        SetDock(delete, Dock.Right);
        SetDock(_count, Dock.Left);
        _count.Margin = new Thickness(10, 0, 0, 0);
        bar.Children.Add(newButton);
        bar.Children.Add(_count);
        bar.Children.Add(delete);
        return bar;
    }

    private Window? Owner => TopLevel.GetTopLevel(this) as Window;

    public async Task NewFixtures()
    {
        if (Owner is not { } owner)
            return;
        if (await NewFixturesDialog.Show(owner, _state) is { Count: > 0 } added)
            _state.SelectFixtures(added);
    }

    private async Task NewProfile()
    {
        if (Owner is not { } owner)
            return;
        var created = new FixtureProfile { Model = "Custom fixture", Channels = [new FixtureChannel { Name = "Dimmer", Role = ChannelRole.Dimmer }] };
        await ProfileEditorDialog.Show(owner, _state, created, isNew: true);
    }

    private void MakeGroup()
    {
        var ids = _state.Project.Fixtures.Where(f => _state.SelectedFixtureIds.Contains(f.Id)).Select(f => f.Id).ToList();
        if (ids.Count == 0)
            return;
        string[] palette = ["#E6A23A", "#3AA0E6", "#E63A8E", "#5BD16A", "#B05BE6", "#E6E03A"];
        _state.Project.Groups.Add(new FixtureGroup { Name = $"Group {_state.Project.Groups.Count + 1}", Color = palette[_state.Project.Groups.Count % palette.Length], FixtureIds = ids });
        _state.Commit();
    }

    private void DeleteSelected()
    {
        var items = SelectedItems();
        if (items.Count == 0)
            return;
        var project = _state.Project;
        var fixtures = items.Where(i => i.Kind == ItemKind.Fixture).Select(i => i.Id).ToHashSet();
        var groups = items.Where(i => i.Kind == ItemKind.Group).Select(i => i.Id).ToHashSet();
        var media = items.Where(i => i.Kind == ItemKind.Media).Select(i => i.Id).ToHashSet();
        var profiles = items.Where(i => i.Kind == ItemKind.Profile).Select(i => i.Id).Where(id => project.Fixtures.All(f => f.ProfileId != id || fixtures.Contains(f.Id))).ToHashSet();
        project.Fixtures.RemoveAll(f => fixtures.Contains(f.Id));
        project.Groups.RemoveAll(g => groups.Contains(g.Id));
        project.Profiles.RemoveAll(p => profiles.Contains(p.Id));
        foreach (var group in project.Groups)
            group.FixtureIds.RemoveAll(fixtures.Contains);
        foreach (var track in project.Tracks)
        {
            track.FixtureIds.RemoveAll(fixtures.Contains);
            track.GroupIds.RemoveAll(groups.Contains);
            track.Clips.RemoveAll(c => media.Contains(c.Id));
        }
        _state.Commit();
        _state.Deselect();
    }

    // ---- Tree ------------------------------------------------------------------------------------------

    private void Refresh(bool force)
    {
        var project = _state.Project;
        var media = project.Tracks.Where(t => t.Kind != TrackKind.Light).SelectMany(t => t.Clips.OfType<MediaClip>()).ToList();
        var signature = string.Join(
            "|",
            _search.Text,
            string.Join(",", project.Groups.Select(g => g.Id + g.Name + g.Color + g.FixtureIds.Count)),
            string.Join(",", project.Fixtures.Select(f => f.Id + f.Name + f.Universe + "." + f.Address + f.ProfileId)),
            string.Join(",", project.Profiles.Select(p => p.Id + p.Model + p.Channels.Count)),
            string.Join(",", media.Select(m => m.Id + m.Name))
        );
        if (!force && signature == _signature)
            return;
        _signature = signature;

        var filter = (_search.Text ?? "").Trim();
        bool Match(string name) => filter.Length == 0 || name.Contains(filter, StringComparison.OrdinalIgnoreCase);

        _syncing = true;
        _tree.Items.Clear();

        var groups = Folder("groups", "Groups", project.Groups.Count);
        foreach (var group in project.Groups)
        {
            var members = group.FixtureIds.Select(project.FixtureById).OfType<Fixture>().ToList();
            if (!Match(group.Name) && !members.Any(m => Match(m.Name)))
                continue;
            var node = Node(new BinItem(ItemKind.Group, group.Id), Chip(group.Color), group.Name, $"{members.Count}", $"group:{group.Id}");
            foreach (var fixture in members.Where(m => filter.Length == 0 || Match(m.Name) || Match(group.Name)))
                node.Items.Add(FixtureNode(fixture));
            groups.Items.Add(node);
        }
        AddFolder(groups, filter);

        var fixtures = Folder("fixtures", "Fixtures", project.Fixtures.Count);
        foreach (var fixture in project.Fixtures.OrderBy(f => f.Universe).ThenBy(f => f.Address).Where(f => Match(f.Name)))
            fixtures.Items.Add(FixtureNode(fixture));
        AddFolder(fixtures, filter);

        var profiles = Folder("profiles", "Fixture profiles", project.Profiles.Count);
        foreach (var profile in project.Profiles.Where(p => Match(p.Model)))
        {
            var used = project.Fixtures.Count(f => f.ProfileId == profile.Id);
            profiles.Items.Add(Node(new BinItem(ItemKind.Profile, profile.Id), Glyph("⚙"), profile.Model, used > 0 ? $"{profile.Channels.Count} ch · {used}×" : $"{profile.Channels.Count} ch"));
        }
        AddFolder(profiles, filter);

        var mediaFolder = Folder("media", "Media", media.Count);
        foreach (var clip in media.Where(m => Match(m.Name)))
        {
            var isVideo = project.Tracks.Any(t => t.Kind == TrackKind.Video && t.Clips.Contains(clip));
            mediaFolder.Items.Add(Node(new BinItem(ItemKind.Media, clip.Id), Glyph(isVideo ? "▶" : "♪"), clip.Name, File.Exists(clip.Path) ? $"{clip.Duration:0.0} s" : "missing"));
        }
        AddFolder(mediaFolder, filter);

        _syncing = false;
        _count.Text = $"{project.Fixtures.Count} fixtures · {project.Groups.Count} groups";
        SyncFromState();
    }

    private void AddFolder(TreeViewItem folder, string filter)
    {
        if (filter.Length > 0 && folder.Items.Count == 0)
            return;
        if (filter.Length > 0)
            folder.IsExpanded = true;
        _tree.Items.Add(folder);
    }

    private static TreeViewItem Folder(string key, string name, int count)
    {
        var folder = new TreeViewItem
        {
            Header = Header(Glyph("📁"), name, $"{count}", bold: true),
            IsExpanded = Expanded.GetValueOrDefault(key),
            Tag = new BinItem(ItemKind.Folder, key),
        };
        folder.PropertyChanged += (_, e) =>
        {
            if (e.Property == TreeViewItem.IsExpandedProperty)
                Expanded[key] = folder.IsExpanded;
        };
        return folder;
    }

    private TreeViewItem FixtureNode(Fixture fixture)
    {
        var profile = _state.Project.ProfileOf(fixture);
        return Node(new BinItem(ItemKind.Fixture, fixture.Id), Glyph("💡"), fixture.Name, $"{fixture.Universe}.{fixture.Address:000}", tooltip: profile?.DisplayName);
    }

    private TreeViewItem Node(BinItem item, Control icon, string name, string info, string? expandKey = null, string? tooltip = null)
    {
        var header = Header(icon, name, info);
        if (tooltip is not null)
            ToolTip.SetTip(header, tooltip);
        var node = new TreeViewItem { Header = header, Tag = item, ContextMenu = ContextMenuFor(item) };
        if (expandKey is not null)
        {
            node.IsExpanded = Expanded.GetValueOrDefault(expandKey);
            node.PropertyChanged += (_, e) =>
            {
                if (e.Property == TreeViewItem.IsExpandedProperty)
                    Expanded[expandKey] = node.IsExpanded;
            };
        }
        node.DoubleTapped += (_, e) =>
        {
            e.Handled = true;
            Open(item);
        };
        return node;
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

    // ---- Selection -------------------------------------------------------------------------------------

    private List<BinItem> SelectedItems() => _tree.SelectedItems?.OfType<TreeViewItem>().Select(i => i.Tag).OfType<BinItem>().ToList() ?? [];

    private BinItem? Primary() => SelectedItems().FirstOrDefault();

    private void OnTreeSelection()
    {
        if (_syncing)
            return;
        var items = SelectedItems();
        ShowInfo(items);
        var fixtures = items.Where(i => i.Kind == ItemKind.Fixture).Select(i => i.Id).ToList();
        foreach (var group in items.Where(i => i.Kind == ItemKind.Group))
            fixtures.AddRange(_state.Project.Groups.Find(g => g.Id == group.Id)?.FixtureIds ?? []);
        if (fixtures.Count > 0)
        {
            _syncing = true;
            _state.SelectFixtures(fixtures.Distinct());
            _syncing = false;
        }
        else if (items.FirstOrDefault(i => i.Kind == ItemKind.Media) is { } media)
        {
            _syncing = true;
            _state.SelectClip(media.Id);
            _syncing = false;
        }
    }

    /// <summary>Mirrors fixtures picked elsewhere (3D view, timeline) into the bin.</summary>
    private void SyncFromState()
    {
        if (_syncing)
            return;
        _syncing = true;
        var wanted = _state.SelectedFixtureIds;
        foreach (var folder in _tree.Items.OfType<TreeViewItem>())
        {
            if (folder.Tag is not BinItem { Id: "fixtures" })
                continue;
            foreach (var node in folder.Items.OfType<TreeViewItem>())
                node.IsSelected = node.Tag is BinItem { Kind: ItemKind.Fixture } item && wanted.Contains(item.Id);
        }
        _syncing = false;
        ShowInfo(SelectedItems());
    }

    /// <summary>Premiere's preview area: what the selected item is, at a glance.</summary>
    private void ShowInfo(List<BinItem> items)
    {
        var project = _state.Project;
        (string Title, string Line1, string Line2, string Icon) info = items.Count switch
        {
            0 => ProjectSummary(),
            > 1 => ($"{items.Count} items selected", "Drag them onto the timeline to light them", "", "▦"),
            _ => items[0] switch
            {
                { Kind: ItemKind.Fixture } f when project.FixtureById(f.Id) is { } fixture => (
                    fixture.Name,
                    project.ProfileOf(fixture)?.DisplayName ?? "no profile",
                    $"DMX {fixture.Universe}.{fixture.Address:000}–{fixture.Address + (project.ProfileOf(fixture)?.Channels.Count ?? 1) - 1:000} · {fixture.Position.X:0.##}, {fixture.Position.Y:0.##}, {fixture.Position.Z:0.##} m",
                    "💡"
                ),
                { Kind: ItemKind.Group } g when project.Groups.Find(x => x.Id == g.Id) is { } group => (group.Name, $"{group.FixtureIds.Count} fixtures", "Drag onto the timeline for a light track", "▣"),
                { Kind: ItemKind.Profile } p when project.Profiles.Find(x => x.Id == p.Id) is { } profile => (
                    profile.Model,
                    $"{profile.Manufacturer} · {profile.Channels.Count} channels · {profile.Beam}",
                    "Double-click to edit the channel layout",
                    "⚙"
                ),
                { Kind: ItemKind.Media } m when _state.FindClip(m.Id)?.Clip is MediaClip clip => (clip.Name, Path.GetFileName(clip.Path), $"{clip.Duration:0.00} s at {clip.Start:0.00} s", "♪"),
                _ => ProjectSummary(),
            },
        };
        var problems = items.Count == 0 ? project.PatchProblems() : [];
        var text = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = info.Title, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                new TextBlock { Text = info.Line1, Foreground = Brushes.Gray, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis },
                new TextBlock { Text = problems.Count > 0 ? $"⚠ {problems.Count} patch problem{(problems.Count == 1 ? "" : "s")}" : info.Line2, Foreground = problems.Count > 0 ? Brushes.Orange : Brushes.Gray, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis },
            },
        };
        if (problems.Count > 0)
            ToolTip.SetTip(text, string.Join("\n", problems));
        var thumb = new Border
        {
            Width = 54,
            Height = 54,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x16)),
            Child = new TextBlock { Text = info.Icon, FontSize = 22, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            Margin = new Thickness(0, 0, 10, 0),
        };
        var row = new DockPanel();
        SetDock(thumb, Dock.Left);
        row.Children.Add(thumb);
        row.Children.Add(text);
        _info.Content = row;
    }

    private (string, string, string, string) ProjectSummary()
    {
        var project = _state.Project;
        var universes = project.Fixtures.Select(f => f.Universe).Distinct().Count();
        return (project.Name, $"{project.Fixtures.Count} fixtures in {universes} universe{(universes == 1 ? "" : "s")}", $"{project.Tracks.Count} tracks · {project.AllClips.Count()} clips", "🏮");
    }

    // ---- Actions ---------------------------------------------------------------------------------------

    private void Open(BinItem item)
    {
        switch (item.Kind)
        {
            case ItemKind.Profile when Owner is { } owner && _state.Project.Profiles.Find(p => p.Id == item.Id) is { } profile:
                _ = ProfileEditorDialog.Show(owner, _state, profile, isNew: false);
                break;
            case ItemKind.Media when _state.FindClip(item.Id)?.Clip is { } clip:
                _state.Seek(clip.Start);
                break;
        }
    }

    private ContextMenu? ContextMenuFor(BinItem item)
    {
        var menu = new ContextMenu();
        void Add(string header, Action action)
        {
            var entry = new MenuItem { Header = header };
            entry.Click += (_, _) => action();
            menu.Items.Add(entry);
        }
        switch (item.Kind)
        {
            case ItemKind.Group:
                Add("New light track for this group", () => TrackFor([], [item.Id]));
                Add("Rename…", () => _ = Rename(item));
                Add("Change colour…", () => _ = Recolour(item.Id));
                break;
            case ItemKind.Fixture:
                Add("New light track for the selection", () => TrackFor(SelectedItems().Where(i => i.Kind == ItemKind.Fixture).Select(i => i.Id).DefaultIfEmpty(item.Id).ToList(), []));
                Add("Group the selection", MakeGroup);
                Add("Rename…", () => _ = Rename(item));
                break;
            case ItemKind.Profile:
                Add("Edit profile…", () => Open(item));
                Add("Patch fixtures of this type…", () => _ = NewFixtures());
                break;
            case ItemKind.Media:
                Add("Rename…", () => _ = Rename(item));
                break;
        }
        menu.Items.Add(new Separator());
        Add("Delete", DeleteSelected);
        return menu;
    }

    private void TrackFor(List<string> fixtureIds, List<string> groupIds)
    {
        var track = TimelineOps.AddLightTrack(_state, fixtureIds, groupIds);
        if (groupIds.Count == 1 && _state.Project.Groups.Find(g => g.Id == groupIds[0]) is { } group)
        {
            track.Name = group.Name;
            _state.Commit();
        }
    }

    private async Task Rename(BinItem item)
    {
        if (Owner is not { } owner)
            return;
        var project = _state.Project;
        var (current, apply) = item.Kind switch
        {
            ItemKind.Group when project.Groups.Find(g => g.Id == item.Id) is { } g => (g.Name, (Action<string>)(v => g.Name = v)),
            ItemKind.Fixture when project.FixtureById(item.Id) is { } f => (f.Name, v => f.Name = v),
            ItemKind.Media when _state.FindClip(item.Id)?.Clip is { } c => (c.Name, v => c.Name = v),
            _ => ("", (Action<string>?)null),
        };
        if (apply is null)
            return;
        var name = await Dialogs.Prompt(owner, "Rename", "New name", current);
        if (string.IsNullOrWhiteSpace(name))
            return;
        apply(name.Trim());
        _state.Commit();
    }

    private async Task Recolour(string groupId)
    {
        if (Owner is not { } owner || _state.Project.Groups.Find(g => g.Id == groupId) is not { } group)
            return;
        var hex = await Dialogs.Prompt(owner, "Group colour", "Hex colour, e.g. #E6A23A", group.Color);
        if (hex is not null && Core.Color.Rgb.TryParseHex(hex, out var rgb))
        {
            group.Color = rgb.ToHex();
            _state.Commit();
        }
    }

    // ---- Dragging to the timeline ----------------------------------------------------------------------

    private void OnTreePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(_tree).Properties.IsLeftButtonPressed || e.ClickCount > 1)
            return;
        var start = e.GetPosition(_tree);
        var dragging = false;
        void Moved(object? s, PointerEventArgs args)
        {
            var p = args.GetPosition(_tree);
            if (!dragging && Math.Abs(p.X - start.X) + Math.Abs(p.Y - start.Y) < 8)
                return;
            if (!dragging && DragPayload().Fixtures.Count + DragPayload().Groups.Count == 0)
                return;
            dragging = true;
            Cursor = new Cursor(StandardCursorType.DragCopy);
            _timeline.PreviewDrop(args.GetPosition(_timeline), DropKind.Targets);
        }
        void Released(object? s, PointerReleasedEventArgs args)
        {
            _tree.RemoveHandler(PointerMovedEvent, Moved);
            _tree.RemoveHandler(PointerReleasedEvent, Released);
            Cursor = Cursor.Default;
            args.Pointer.Capture(null);
            if (!dragging)
                return;
            var (fixtures, groups, name) = DragPayload();
            _timeline.DropTargets(fixtures, groups, name, args.GetPosition(_timeline));
        }
        _tree.AddHandler(PointerMovedEvent, Moved, RoutingStrategies.Bubble, handledEventsToo: true);
        _tree.AddHandler(PointerReleasedEvent, Released, RoutingStrategies.Bubble, handledEventsToo: true);
        e.Pointer.Capture(_tree);
    }

    private (List<string> Fixtures, List<string> Groups, string Name) DragPayload()
    {
        var items = SelectedItems();
        var groups = items.Where(i => i.Kind == ItemKind.Group).Select(i => i.Id).ToList();
        var fixtures = items.Where(i => i.Kind == ItemKind.Fixture).Select(i => i.Id).ToList();
        var name = groups.Count == 1 && fixtures.Count == 0 && _state.Project.Groups.Find(g => g.Id == groups[0]) is { } group
            ? group.Name
            : fixtures.Count == 1 && groups.Count == 0 && _state.Project.FixtureById(fixtures[0]) is { } fixture
                ? fixture.Name
                : $"Lights {_state.Project.Tracks.Count(t => t.Kind == TrackKind.Light) + 1}";
        return (fixtures, groups, name);
    }
}
