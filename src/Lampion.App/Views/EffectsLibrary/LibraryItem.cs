using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Lampion.App.Views.EffectsLibrary;

/// <summary>
/// One entry in the Effects &amp; Presets library: an icon and a name you drag onto the timeline (with a
/// live drop preview) or double-click to apply.
/// </summary>
internal sealed class LibraryItem : Border
{
    private static readonly IBrush HoverBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x35, 0x3A));
    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);

    private readonly Visual _dropTarget;
    private readonly Action _onDoubleClick;
    private readonly Action<Point?> _preview;
    private readonly Func<Point, bool> _drop;
    private bool _dragging;

    /// <summary>Creates the entry.</summary>
    /// <param name="icon">Swatch or glyph shown before the name.</param>
    /// <param name="name">Entry name.</param>
    /// <param name="description">Tooltip text; a usage hint is appended.</param>
    /// <param name="dropTarget">The control drop positions are reported in, i.e. the timeline.</param>
    /// <param name="onDoubleClick">Applies the entry without dragging.</param>
    /// <param name="preview">Shows the drop preview at a point over <paramref name="dropTarget"/>; null clears it.</param>
    /// <param name="drop">Drops the entry at a point over <paramref name="dropTarget"/>; returns whether it landed.</param>
    public LibraryItem(Control icon, string name, string description, Visual dropTarget, Action onDoubleClick, Action<Point?> preview, Func<Point, bool> drop)
    {
        _dropTarget = dropTarget;
        _onDoubleClick = onDoubleClick;
        _preview = preview;
        _drop = drop;
        icon.VerticalAlignment = VerticalAlignment.Center;
        Padding = new Thickness(6, 4);
        CornerRadius = new CornerRadius(3);
        Background = Brushes.Transparent;
        Cursor = HandCursor;
        Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { icon, new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center } } };
        ToolTip.SetTip(this, description + "\nDrag onto the timeline, or double-click.");
        PointerEntered += (_, _) => Background = HoverBrush;
        PointerExited += (_, _) => Background = Brushes.Transparent;
        PointerPressed += OnPressed;
        PointerMoved += OnMoved;
        PointerReleased += OnReleased;
    }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(Border);

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        // Right-click opens the context menu; only the left button picks the item up.
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        if (e.ClickCount == 2)
        {
            _onDoubleClick();
            return;
        }
        _dragging = true;
        e.Pointer.Capture(this);
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        if (_dragging)
            _preview(e.GetPosition(_dropTarget));
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging)
            return;
        _dragging = false;
        e.Pointer.Capture(null);
        _preview(null);
        _drop(e.GetPosition(_dropTarget));
    }
}
