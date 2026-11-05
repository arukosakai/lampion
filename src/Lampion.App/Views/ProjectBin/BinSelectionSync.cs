using Lampion.App.Editor;

namespace Lampion.App.Views.ProjectBin;

/// <summary>
/// Keeps the bin's selection and the editor's in step both ways: picking fixtures, groups or media in the bin
/// selects them in the editor, and fixtures picked elsewhere (3D view, timeline) light up in the bin.
/// </summary>
internal sealed class BinSelectionSync
{
    private readonly EditorState _state;
    private readonly ProjectBinTree _tree;
    private readonly ProjectInfoHeader _info;
    private bool _pushing;

    /// <param name="state">The editor selection to mirror.</param>
    /// <param name="tree">The bin's tree.</param>
    /// <param name="info">Shown the current bin selection after every change.</param>
    public BinSelectionSync(EditorState state, ProjectBinTree tree, ProjectInfoHeader info)
    {
        _state = state;
        _tree = tree;
        _info = info;
        tree.UserSelectionChanged += OnTreeSelection;
        state.SelectionChanged += SyncFromState;
    }

    /// <summary>Selects the editor's fixtures in the bin and refreshes the info header. Ignored while the bin is the one selecting.</summary>
    public void SyncFromState()
    {
        if (_pushing)
            return;
        _tree.ShowSelectedFixtures(_state.SelectedFixtureIds);
        _info.Show(_tree.SelectedItems());
    }

    private void OnTreeSelection()
    {
        if (_pushing)
            return;
        var items = _tree.SelectedItems();
        _info.Show(items);
        var fixtures = new List<string>();
        foreach (var item in items)
        {
            if (item.Kind == BinItemKind.Fixture)
                fixtures.Add(item.Id);
        }
        foreach (var item in items)
        {
            if (item.Kind == BinItemKind.Group)
                fixtures.AddRange(_state.Project.Groups.Find(g => g.Id == item.Id)?.FixtureIds ?? []);
        }
        if (fixtures.Count > 0)
        {
            Push(() => _state.SelectFixtures(fixtures.Distinct()));
            return;
        }
        var media = items.FindIndex(i => i.Kind == BinItemKind.Media);
        if (media >= 0)
            Push(() => _state.SelectClip(items[media].Id));
    }

    private void Push(Action select)
    {
        _pushing = true;
        try
        {
            select();
        }
        finally
        {
            _pushing = false;
        }
    }
}
