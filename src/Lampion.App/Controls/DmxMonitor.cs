using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Lampion.App.Editor;

namespace Lampion.App.Controls;

/// <summary>
/// Live view of the DMX Lampion is computing: 512 cells per universe, the selected fixtures' channels
/// outlined, and the channel role under the pointer — the place to check what the timeline really sends.
/// </summary>
public sealed class DmxMonitor : Control
{
    private const int Columns = 32;
    private const double Cell = 30;
    private const double CellHeight = 22;
    private static readonly string[] ByteLabels = Enumerable.Range(0, 256).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToArray();
    private readonly EditorState _state;
    private Point? _hover;

    public DmxMonitor(EditorState state)
    {
        _state = state;
        state.PlayheadChanged += InvalidateVisual;
        state.Changed += InvalidateVisual;
        state.SelectionChanged += InvalidateVisual;
    }

    public int Universe { get; set; } = 1;

    protected override Size MeasureOverride(Size availableSize) => new(Columns * Cell + 8, 512 / Columns * CellHeight + 40);

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x18)), new Rect(Bounds.Size));
        var frame = _state.CurrentFrame;
        var data = frame.Universes.TryGetValue(Universe, out var d) ? d : new byte[512];
        var owners = new string?[512];
        var roles = new string?[512];
        var selected = new bool[512];
        foreach (var fixture in _state.Project.Fixtures)
        {
            if (fixture.Universe != Universe || _state.Project.ProfileOf(fixture) is not { } profile)
                continue;
            for (var i = 0; i < profile.Channels.Count; i++)
            {
                var a = fixture.Address - 1 + i;
                if (a is < 0 or >= 512)
                    continue;
                owners[a] = fixture.Name;
                roles[a] = profile.Channels[i].Name;
                selected[a] = _state.SelectedFixtureIds.Contains(fixture.Id);
            }
        }

        string? hoverText = null;
        for (var ch = 0; ch < 512; ch++)
        {
            var rect = new Rect(4 + ch % Columns * Cell, 4 + ch / Columns * CellHeight, Cell - 2, CellHeight - 2);
            var v = data[ch];
            var fill = owners[ch] is null ? Color.FromRgb(0x22, 0x22, 0x22) : Color.FromRgb((byte)(0x26 + v * 0.55), (byte)(0x2A + v * 0.4), (byte)(0x30 + v * 0.1));
            ctx.DrawRectangle(new SolidColorBrush(fill), selected[ch] ? new Pen(new SolidColorBrush(Color.FromRgb(0x4D, 0xA3, 0xFF)), 1.5) : null, rect, 2, 2);
            var text = TextCache.Get(ByteLabels[v], 10, owners[ch] is null ? Brushes.DimGray : Brushes.White);
            ctx.DrawText(text, new Point(rect.Center.X - text.Width / 2, rect.Top + 4));
            if (_hover is { } h && rect.Contains(h))
                hoverText = owners[ch] is null ? $"{Universe}.{ch + 1}: {v} (unpatched)" : $"{Universe}.{ch + 1}: {v}  —  {owners[ch]} · {roles[ch]}";
        }
        var footer = TextCache.Get(hoverText ?? $"Universe {Universe} · hover a channel for details", 11, Brushes.Gray);
        ctx.DrawText(footer, new Point(6, 512 / Columns * CellHeight + 12));
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _hover = e.GetPosition(this);
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = null;
        InvalidateVisual();
    }
}
