using Avalonia.Controls;
using Lampion.App.Editor;

namespace Lampion.App.Views.Inspector;

/// <summary>
/// What every inspector component shares: the editor state, a way to edit the model without the panel
/// rebuilding under the user's pointer, a way to ask for a rebuild, and the list of live-refreshed parts.
/// UI thread only.
/// </summary>
internal sealed class InspectorContext
{
    private readonly Control _host;
    private readonly Action _rebuild;
    private readonly List<IInspectorRefreshable> _live = [];

    /// <summary>Creates the context for one inspector panel.</summary>
    /// <param name="state">The shared editor state.</param>
    /// <param name="host">The panel, used to find the owning window for dialogs.</param>
    /// <param name="rebuild">Rebuilds the whole inspector for the current selection.</param>
    public InspectorContext(EditorState state, Control host, Action rebuild)
    {
        State = state;
        _host = host;
        _rebuild = rebuild;
    }

    /// <summary>The shared editor state; model edits go through it so undo and dirty-tracking work.</summary>
    public EditorState State { get; }

    /// <summary>True while an <see cref="Edit"/> is running, so the panel can ignore the change it caused itself.</summary>
    public bool IsEditing { get; private set; }

    /// <summary>The window the inspector sits in, for dialogs; null while it isn't attached to one.</summary>
    public Window? Owner => TopLevel.GetTopLevel(_host) as Window;

    /// <summary>Applies an edit made in the inspector: <see cref="EditorState.Commit"/> records an undo step, otherwise it's a live <see cref="EditorState.Touch"/>.</summary>
    /// <param name="change">Mutates the model.</param>
    /// <param name="commit">False while dragging; true when the edit is finished.</param>
    public void Edit(Action change, bool commit = true)
    {
        IsEditing = true;
        try
        {
            change();
            Notify(commit);
        }
        finally
        {
            IsEditing = false;
        }
    }

    /// <summary>Like <see cref="Edit(Action, bool)"/>, for a setter that takes the new value, so scrubbing needs no closure per pointer move.</summary>
    /// <param name="set">Mutates the model; usually a delegate created once when the row was built.</param>
    /// <param name="value">The new value.</param>
    /// <param name="commit">False while dragging; true when the edit is finished.</param>
    public void Edit<T>(Action<T> set, T value, bool commit = true)
    {
        IsEditing = true;
        try
        {
            set(value);
            Notify(commit);
        }
        finally
        {
            IsEditing = false;
        }
    }

    /// <summary>Rebuilds the inspector, e.g. after an edit that adds or removes rows. Every control built so far is discarded.</summary>
    public void Rebuild() => _rebuild();

    /// <summary>Registers a part to refresh on every playhead change until the next rebuild.</summary>
    public void Track(IInspectorRefreshable item) => _live.Add(item);

    /// <summary>Forgets every registered part; called when the control tree is rebuilt.</summary>
    public void ClearTracked() => _live.Clear();

    /// <summary>Refreshes every registered part. Allocation-free; runs on every playhead tick.</summary>
    public void RefreshAll()
    {
        for (var i = 0; i < _live.Count; i++)
            _live[i].Refresh();
    }

    private void Notify(bool commit)
    {
        if (commit)
            State.Commit();
        else
            State.Touch();
    }
}
