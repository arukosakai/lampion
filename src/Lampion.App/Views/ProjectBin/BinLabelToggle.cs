using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Lampion.App.Views.ProjectBin;

/// <summary>
/// Clicking a folder's or group's label twirls it open or closed, like the chevron does. Listens on the tree
/// itself because <see cref="BinDragSource"/> captures the pointer there, so rows never see their own release.
/// </summary>
internal sealed class BinLabelToggle
{
    // Further than this between press and release is a drag, not a click.
    private const double ClickSlop = 4;

    private readonly TreeView _tree;
    private TreeViewItem? _pressed;
    private Point _start;

    private BinLabelToggle(TreeView tree) => _tree = tree;

    /// <summary>Starts toggling rows of <paramref name="tree"/> on label clicks; the handlers live as long as the tree.</summary>
    public static void Attach(TreeView tree)
    {
        var toggle = new BinLabelToggle(tree);
        tree.AddHandler(InputElement.PointerPressedEvent, toggle.OnPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        tree.AddHandler(InputElement.PointerReleasedEvent, toggle.OnReleased, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        _pressed = null;
        if (!e.GetCurrentPoint(_tree).Properties.IsLeftButtonPressed || e.ClickCount > 1)
            return;
        _pressed = RowUnder(e.Source as Visual);
        _start = e.GetPosition(_tree);
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        var row = _pressed;
        _pressed = null;
        if (row is null || e.InitialPressMouseButton != MouseButton.Left || row.ItemCount == 0)
            return;
        var p = e.GetPosition(_tree);
        if (Math.Abs(p.X - _start.X) + Math.Abs(p.Y - _start.Y) > ClickSlop)
            return;
        row.IsExpanded = !row.IsExpanded;
    }

    // The row whose label is under the pointer; null on the chevron, which toggles by itself.
    private static TreeViewItem? RowUnder(Visual? source)
    {
        for (var visual = source; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is ToggleButton)
                return null;
            if (visual is TreeViewItem row)
                return row;
        }
        return null;
    }
}
