using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lampion.App.Editor;
using Lampion.Core.Engine;

namespace Lampion.App.Controls;

/// <summary>
/// Live view of the DMX Lampion is computing: 512 cells per universe, the selected fixtures' channels
/// outlined, and the channel role under the pointer — the place to check what the timeline really sends.
/// Repaints whenever the playhead moves, allocation-free in steady state. UI thread only.
/// </summary>
internal sealed class DmxMonitor : Control
{
    private const int Columns = 32;
    private const double Cell = 30;
    private const double CellHeight = 22;
    private const double GridInset = 4;
    private const int Rows = DmxFrame.UniverseSize / Columns;

    private static readonly string[] ByteLabels = BuildByteLabels();
    private static readonly byte[] Silence = new byte[DmxFrame.UniverseSize];
    private static readonly IBrush Background = new ImmutableSolidColorBrush(Color.FromRgb(0x18, 0x18, 0x18));
    private static readonly IBrush Unpatched = new ImmutableSolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22));
    private static readonly IBrush[] Levels = BuildLevels();
    private static readonly Pen SelectedPen = new(new ImmutableSolidColorBrush(Color.FromRgb(0x4D, 0xA3, 0xFF)), 1.5);

    private readonly EditorState _state;
    private readonly DmxChannelMap _map = new();
    private readonly DmxFooter _footer = new();
    private bool _mapStale = true;
    private int _universe = 1;
    private Point? _hover;

    /// <summary>A monitor of <paramref name="state"/>'s current DMX frame.</summary>
    public DmxMonitor(EditorState state)
    {
        _state = state;
        state.PlayheadChanged += InvalidateVisual;
        state.FrameChanged += InvalidateVisual;
        state.Changed += OnPatchOrSelectionChanged;
        state.SelectionChanged += OnPatchOrSelectionChanged;
    }

    /// <summary>The 1-based universe on show.</summary>
    public int Universe
    {
        get => _universe;
        set
        {
            _universe = value;
            _mapStale = true;
        }
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Background, new Rect(Bounds.Size));
        if (_mapStale)
        {
            _map.Rebuild(_state.Project, _universe, _state.SelectedFixtureIds);
            _mapStale = false;
        }
        ReadOnlySpan<byte> data = _state.CurrentFrame.TryGetUniverse(_universe, out var channels) ? channels : Silence;
        for (var ch = 0; ch < DmxFrame.UniverseSize; ch++)
        {
            var rect = CellRect(ch);
            var v = data[ch];
            var patched = _map.Owner(ch) is not null;
            context.DrawRectangle(patched ? Levels[v] : Unpatched, _map.IsSelected(ch) ? SelectedPen : null, rect, 2, 2);
            var text = TextCache.Get(ByteLabels[v], 10, patched ? Brushes.White : Brushes.DimGray);
            context.DrawText(text, new Point(rect.Center.X - text.Width / 2, rect.Top + 4));
        }
        var hovered = HoveredChannel();
        _footer.Render(context, new Point(6, Rows * CellHeight + 12), _universe, hovered, hovered < 0 ? (byte)0 : data[hovered], _map, ByteLabels);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) => new(Columns * Cell + 8, Rows * CellHeight + 40);

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _hover = e.GetPosition(this);
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = null;
        InvalidateVisual();
    }

    private static Rect CellRect(int channel) =>
        new(GridInset + channel % Columns * Cell, GridInset + channel / Columns * CellHeight, Cell - 2, CellHeight - 2);

    private void OnPatchOrSelectionChanged()
    {
        _mapStale = true;
        InvalidateVisual();
    }

    /// <summary>The 0-based channel whose cell (not the gap around it) is under the pointer, or -1.</summary>
    private int HoveredChannel()
    {
        if (_hover is not { } h)
            return -1;
        var column = (int)Math.Floor((h.X - GridInset) / Cell);
        var row = (int)Math.Floor((h.Y - GridInset) / CellHeight);
        if (column is < 0 or >= Columns || row is < 0 or >= Rows)
            return -1;
        var channel = row * Columns + column;
        return CellRect(channel).Contains(h) ? channel : -1;
    }

    private static string[] BuildByteLabels()
    {
        var labels = new string[256];
        for (var i = 0; i < labels.Length; i++)
            labels[i] = i.ToString(CultureInfo.InvariantCulture);
        return labels;
    }

    private static IBrush[] BuildLevels()
    {
        var levels = new IBrush[256];
        for (var v = 0; v < levels.Length; v++)
            levels[v] = new ImmutableSolidColorBrush(Color.FromRgb((byte)(0x26 + v * 0.55), (byte)(0x2A + v * 0.4), (byte)(0x30 + v * 0.1)));
        return levels;
    }
}
