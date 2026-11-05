using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Lampion.App.Controls;
using Lampion.App.Editor;

namespace Lampion.App.Views.ProjectBin;

/// <summary>
/// Dragging selected fixtures and groups from the bin onto the timeline, where they make (or extend) a light
/// track. The drag starts after 8 px of travel, and only when the selection holds something lightable.
/// </summary>
internal sealed class BinDragSource
{
    private const double StartDistance = 8;

    private readonly ProjectBinTree _bin;
    private readonly InputElement _cursorHost;
    private readonly TimelineControl _timeline;
    private readonly EditorState _state;
    private readonly Cursor _dragCursor = new(StandardCursorType.DragCopy);
    private readonly EventHandler<PointerEventArgs> _moved;
    private readonly EventHandler<PointerReleasedEventArgs> _released;
    private Point _start;
    private bool _dragging;

    private BinDragSource(ProjectBinTree bin, InputElement cursorHost, TimelineControl timeline, EditorState state)
    {
        _bin = bin;
        _cursorHost = cursorHost;
        _timeline = timeline;
        _state = state;
        _moved = OnMoved;
        _released = OnReleased;
    }

    /// <summary>Starts listening for drags on <paramref name="bin"/>; the handlers live as long as the tree.</summary>
    /// <param name="bin">The tree to drag from.</param>
    /// <param name="cursorHost">Shows the drag cursor while dragging.</param>
    /// <param name="timeline">Previews and receives the drop.</param>
    /// <param name="state">Supplies the project for naming the new track.</param>
    public static void Attach(ProjectBinTree bin, InputElement cursorHost, TimelineControl timeline, EditorState state)
    {
        var source = new BinDragSource(bin, cursorHost, timeline, state);
        bin.Tree.AddHandler(InputElement.PointerPressedEvent, source.OnPressed, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private TreeView Tree => _bin.Tree;

    private BinDragPayload Payload() => BinDragPayload.Of(_bin.SelectedItems(), _state.Project);

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(Tree).Properties.IsLeftButtonPressed || e.ClickCount > 1)
            return;
        _start = e.GetPosition(Tree);
        _dragging = false;
        Tree.AddHandler(InputElement.PointerMovedEvent, _moved, RoutingStrategies.Bubble, handledEventsToo: true);
        Tree.AddHandler(InputElement.PointerReleasedEvent, _released, RoutingStrategies.Bubble, handledEventsToo: true);
        e.Pointer.Capture(Tree);
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        var p = e.GetPosition(Tree);
        if (!_dragging && Math.Abs(p.X - _start.X) + Math.Abs(p.Y - _start.Y) < StartDistance)
            return;
        if (!_dragging && Payload().IsEmpty)
            return;
        _dragging = true;
        _cursorHost.Cursor = _dragCursor;
        _timeline.PreviewDrop(e.GetPosition(_timeline), DropKind.Targets);
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        Tree.RemoveHandler(InputElement.PointerMovedEvent, _moved);
        Tree.RemoveHandler(InputElement.PointerReleasedEvent, _released);
        _cursorHost.Cursor = Cursor.Default;
        e.Pointer.Capture(null);
        if (!_dragging)
            return;
        // Read at release, after the tree has settled its own click handling.
        var (fixtures, groups, name) = Payload();
        _timeline.DropTargets(fixtures, groups, name, e.GetPosition(_timeline));
    }
}
