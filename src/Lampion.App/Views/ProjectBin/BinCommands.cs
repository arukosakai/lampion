using Avalonia.Controls;
using Lampion.App.Editor;
using Lampion.Core.Color;
using Lampion.Core.Model.Rig;

namespace Lampion.App.Views.ProjectBin;

/// <summary>
/// What the Project bin can do to the project: patch, group, delete, rename, recolour, open and make tracks.
/// Every edit goes through <see cref="EditorState.Commit"/>, so it is undoable.
/// </summary>
internal sealed class BinCommands
{
    private readonly EditorState _state;
    private readonly Control _host;
    private readonly ProjectBinTree _tree;

    /// <param name="state">The project being edited.</param>
    /// <param name="host">Any control in the panel; its window owns the dialogs.</param>
    /// <param name="tree">Supplies the selection the commands act on.</param>
    public BinCommands(EditorState state, Control host, ProjectBinTree tree)
    {
        _state = state;
        _host = host;
        _tree = tree;
    }

    private Window? Owner => TopLevel.GetTopLevel(_host) as Window;

    /// <summary>Opens the New Fixtures dialog and selects what it patched. Does nothing while the panel isn't in a window.</summary>
    public async Task NewFixtures()
    {
        if (Owner is not { } owner)
            return;
        if (await NewFixturesDialog.Show(owner, _state) is { Count: > 0 } added)
            _state.SelectFixtures(added);
    }

    /// <summary>Opens the profile editor on a fresh one-channel profile, added to the project if saved.</summary>
    public async Task NewProfile()
    {
        if (Owner is not { } owner)
            return;
        await ProfileEditorDialog.Show(owner, _state, ProfileEditorDialog.NewCustomProfile(), isNew: true);
    }

    /// <summary>Groups the selected fixtures (in patch order) under the next palette colour. No-op without fixtures.</summary>
    public void MakeGroup()
    {
        var project = _state.Project;
        var ids = project.Fixtures.Where(f => _state.SelectedFixtureIds.Contains(f.Id)).Select(f => f.Id).ToList();
        if (ids.Count == 0)
            return;
        var group = new FixtureGroup { Name = $"Group {project.Groups.Count + 1}", Color = GroupPalette.ForIndex(project.Groups.Count) };
        group.FixtureIds.AddRange(ids);
        project.Groups.Add(group);
        _state.Commit();
    }

    /// <summary>
    /// Deletes the selected fixtures, groups and media, and the selected profiles nothing else still uses,
    /// along with every reference tracks and groups held to them.
    /// </summary>
    public void DeleteSelected()
    {
        var items = _tree.SelectedItems();
        if (items.Count == 0)
            return;
        var project = _state.Project;
        var fixtures = IdsOf(items, BinItemKind.Fixture);
        var groups = IdsOf(items, BinItemKind.Group);
        var media = IdsOf(items, BinItemKind.Media);
        var profiles = IdsOf(items, BinItemKind.Profile);
        profiles.RemoveWhere(id => project.Fixtures.Any(f => f.ProfileId == id && !fixtures.Contains(f.Id)));
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

    /// <summary>Double-click: profiles open in the profile editor, media moves the playhead to the clip.</summary>
    public void Open(BinItem item)
    {
        switch (item.Kind)
        {
            case BinItemKind.Profile when Owner is { } owner && _state.Project.Profiles.Find(p => p.Id == item.Id) is { } profile:
                _ = ProfileEditorDialog.Show(owner, _state, profile, isNew: false);
                break;
            case BinItemKind.Media when _state.FindClip(item.Id)?.Clip is { } clip:
                _state.Seek(clip.Start);
                break;
        }
    }

    /// <summary>A new light track for the selected fixtures, or for <paramref name="clicked"/> when none are selected.</summary>
    public void TrackForSelectedFixtures(BinItem clicked)
    {
        var ids = _tree.SelectedItems().Where(i => i.Kind == BinItemKind.Fixture).Select(i => i.Id).DefaultIfEmpty(clicked.Id).ToList();
        TrackFor(ids, []);
    }

    /// <summary>A new light track for one group, named after it.</summary>
    public void TrackForGroup(string groupId) => TrackFor([], [groupId]);

    private void TrackFor(List<string> fixtureIds, List<string> groupIds)
    {
        var track = TimelineOps.AddLightTrack(_state, fixtureIds, groupIds);
        if (groupIds.Count == 1 && _state.Project.Groups.Find(g => g.Id == groupIds[0]) is { } group)
        {
            track.Name = group.Name;
            _state.Commit();
        }
    }

    /// <summary>Prompts for a new name for a group, fixture or media clip; blank answers are ignored.</summary>
    public async Task Rename(BinItem item)
    {
        if (Owner is not { } owner)
            return;
        var project = _state.Project;
        var (current, apply) = item.Kind switch
        {
            BinItemKind.Group when project.Groups.Find(g => g.Id == item.Id) is { } g => (g.Name, (Action<string>)(v => g.Name = v)),
            BinItemKind.Fixture when project.FixtureById(item.Id) is { } f => (f.Name, v => f.Name = v),
            BinItemKind.Media when _state.FindClip(item.Id)?.Clip is { } c => (c.Name, v => c.Name = v),
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

    /// <summary>Prompts for a group's hex colour; anything that doesn't parse leaves it unchanged.</summary>
    public async Task Recolour(string groupId)
    {
        if (Owner is not { } owner || _state.Project.Groups.Find(g => g.Id == groupId) is not { } group)
            return;
        var hex = await Dialogs.Prompt(owner, "Group colour", "Hex colour, e.g. #E6A23A", group.Color);
        if (hex is not null && Rgb.TryParseHex(hex, out var rgb))
        {
            group.Color = rgb.ToHex();
            _state.Commit();
        }
    }

    private static HashSet<string> IdsOf(List<BinItem> items, BinItemKind kind)
    {
        var ids = new HashSet<string>();
        foreach (var item in items)
            if (item.Kind == kind)
                ids.Add(item.Id);
        return ids;
    }
}
