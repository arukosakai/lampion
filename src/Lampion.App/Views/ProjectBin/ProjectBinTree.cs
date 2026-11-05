using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Lampion.App.Views.ProjectBin;

/// <summary>
/// The bin's multi-select tree. Tells the user's own selection changes apart from the ones made in code
/// (rebuilding, mirroring the editor's selection), so only the former are reported.
/// </summary>
internal sealed class ProjectBinTree : Border
{
    private readonly TreeView _tree = new() { SelectionMode = SelectionMode.Multiple };
    private bool _quiet;

    /// <summary>Creates an empty tree; call <see cref="Rebuild"/> to fill it.</summary>
    public ProjectBinTree()
    {
        BorderBrush = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x14));
        BorderThickness = new Thickness(0, 1);
        Child = _tree;
        _tree.SelectionChanged += (_, _) =>
        {
            if (!_quiet)
                UserSelectionChanged?.Invoke();
        };
        _tree.KeyDown += (_, e) =>
        {
            if (e.Key == Key.F2 && Primary() is { } item)
                RenameRequested?.Invoke(item);
        };
    }

    /// <summary>The tree itself, for attaching pointer handlers such as <see cref="BinDragSource"/>.</summary>
    public TreeView Tree => _tree;

    /// <summary>Raised when the user changes the selection; not raised for changes made by this class.</summary>
    public event Action? UserSelectionChanged;

    /// <summary>Raised when F2 is pressed with something selected, with the first selected item.</summary>
    public event Action<BinItem>? RenameRequested;

    /// <summary>Replaces every row with what <paramref name="builder"/> produces for <paramref name="filter"/>.</summary>
    public void Rebuild(BinTreeBuilder builder, string filter)
    {
        _quiet = true;
        try
        {
            _tree.Items.Clear();
            builder.Fill(_tree.Items, filter);
        }
        finally
        {
            _quiet = false;
        }
    }

    /// <summary>The selected rows' items, in selection order. A fresh list each call.</summary>
    public List<BinItem> SelectedItems()
    {
        var result = new List<BinItem>();
        if (_tree.SelectedItems is not { } selected)
            return result;
        foreach (var entry in selected)
            if (entry is TreeViewItem { Tag: BinItem item })
                result.Add(item);
        return result;
    }

    /// <summary>The first selected item, or null when nothing is selected.</summary>
    public BinItem? Primary()
    {
        var items = SelectedItems();
        return items.Count > 0 ? items[0] : null;
    }

    /// <summary>
    /// Selects exactly the rows of the Fixtures folder whose ids are in <paramref name="fixtureIds"/>, so a pick
    /// in the 3D view or timeline shows up here. Rows in group folders are left alone.
    /// </summary>
    public void ShowSelectedFixtures(IReadOnlySet<string> fixtureIds)
    {
        _quiet = true;
        try
        {
            foreach (var folder in _tree.Items.OfType<TreeViewItem>())
            {
                if (folder.Tag is not BinItem { Id: "fixtures" })
                    continue;
                foreach (var node in folder.Items.OfType<TreeViewItem>())
                    node.IsSelected = node.Tag is BinItem { Kind: BinItemKind.Fixture } item && fixtureIds.Contains(item.Id);
            }
        }
        finally
        {
            _quiet = false;
        }
    }
}
