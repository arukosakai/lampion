using Avalonia;
using Avalonia.Controls;
using Lampion.App.Controls;
using Lampion.App.Editor;
using Lampion.App.Views.ProjectBin;
using Lampion.Core.Model.Rig;

namespace Lampion.App.Views;

/// <summary>
/// The Project panel as a Premiere-style bin: one searchable tree of groups, fixtures, profiles and media,
/// an info header for the selection, and New / Delete at the bottom. Fixtures and groups drag onto the
/// timeline to make (or extend) light tracks.
/// </summary>
public sealed class ProjectPanel : DockPanel
{
    private readonly EditorState _state;
    private readonly TextBox _search = new() { PlaceholderText = "🔍  Search", MinHeight = 24, Margin = new Thickness(6, 4) };
    private readonly ProjectBinTree _tree = new();
    private readonly ProjectInfoHeader _info;
    private readonly ProjectToolbar _toolbar;
    private readonly BinFingerprint _fingerprint = new();
    private readonly BinCommands _commands;
    private readonly BinTreeBuilder _builder;
    private readonly BinSelectionSync _selection;

    /// <summary>Builds the panel and keeps it in step with <paramref name="state"/> from then on.</summary>
    /// <param name="state">The project, selection and undo the panel works on.</param>
    /// <param name="timeline">Where fixtures and groups dragged out of the bin are dropped.</param>
    /// <param name="importAudio">Runs ＋ New → Import audio…</param>
    /// <param name="importVideo">Runs ＋ New → Import video…</param>
    public ProjectPanel(EditorState state, TimelineControl timeline, Func<Task> importAudio, Func<Task> importVideo)
    {
        _state = state;
        _info = new ProjectInfoHeader(state);
        _commands = new BinCommands(state, this, _tree);
        _builder = new BinTreeBuilder(state, new BinContextMenus(_commands), _commands);
        _toolbar = new ProjectToolbar(_commands, importAudio, importVideo);
        _selection = new BinSelectionSync(state, _tree, _info);
        BinDragSource.Attach(_tree, this, timeline, state);

        var top = new StackPanel { Children = { _info, _search } };
        SetDock(top, Dock.Top);
        Children.Add(top);
        SetDock(_toolbar, Dock.Bottom);
        Children.Add(_toolbar);
        Children.Add(_tree);

        _search.TextChanged += (_, _) => Refresh(force: true);
        _tree.RenameRequested += item => _ = _commands.Rename(item);
        state.Changed += () => Refresh(force: false);
        state.ProjectReplaced += () => Refresh(force: true);
        Refresh(force: true);
    }

    /// <summary>Opens the New Fixtures dialog and selects what it patched. Does nothing while the panel isn't in a window.</summary>
    public Task NewFixtures() => _commands.NewFixtures();

    // Changed fires on every drag tick; the fingerprint keeps the rebuild and patch check to edits the bin shows.
    private void Refresh(bool force)
    {
        if (!_fingerprint.Update(_state.Project, _search.Text) && !force)
            return;
        var project = _state.Project;
        _info.SetProblems(PatchPlanner.Problems(project));
        _tree.Rebuild(_builder, (_search.Text ?? "").Trim());
        _toolbar.ShowCounts(project.Fixtures.Count, project.Groups.Count);
        _selection.SyncFromState();
    }
}
