namespace Lampion.App.Editor;

/// <summary>
/// Undo and redo stacks of serialized project snapshots, plus the snapshot of the state on screen.
/// Knows nothing about projects or serialization: it only stores and swaps strings.
/// </summary>
internal sealed class UndoHistory
{
    /// <summary>Steps kept before the oldest is dropped.</summary>
    public const int Limit = 200;

    private readonly List<string> _undo = [];
    private readonly List<string> _redo = [];

    /// <summary>Starts a history whose current state is <paramref name="snapshot"/>.</summary>
    public UndoHistory(string snapshot) => Current = snapshot;

    /// <summary>The snapshot of the state currently on screen.</summary>
    public string Current { get; private set; }

    /// <summary>True when <see cref="TryUndo"/> has a step to go back to.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>True when <see cref="TryRedo"/> has a step to go forward to.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Makes <paramref name="snapshot"/> current, pushing the previous one as an undo step and clearing redo.</summary>
    /// <returns>False, and nothing recorded, when the snapshot equals the current one.</returns>
    public bool Record(string snapshot)
    {
        if (snapshot == Current)
            return false;
        _undo.Add(Current);
        if (_undo.Count > Limit)
            _undo.RemoveAt(0);
        _redo.Clear();
        Current = snapshot;
        return true;
    }

    /// <summary>Steps back; <paramref name="snapshot"/> is the state to restore. False when there is nothing to undo.</summary>
    public bool TryUndo(out string snapshot) => Step(_undo, _redo, out snapshot);

    /// <summary>Steps forward; <paramref name="snapshot"/> is the state to restore. False when there is nothing to redo.</summary>
    public bool TryRedo(out string snapshot) => Step(_redo, _undo, out snapshot);

    /// <summary>Forgets every step and starts over from <paramref name="snapshot"/>.</summary>
    public void Reset(string snapshot)
    {
        _undo.Clear();
        _redo.Clear();
        Current = snapshot;
    }

    private bool Step(List<string> from, List<string> to, out string snapshot)
    {
        if (from.Count == 0)
        {
            snapshot = Current;
            return false;
        }
        to.Add(Current);
        Current = from[^1];
        from.RemoveAt(from.Count - 1);
        snapshot = Current;
        return true;
    }
}
