using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Lampion.App.Controls;

/// <summary>
/// A console-style XY pad for a moving head: drag left/right to pan, up/down to tilt. Pens, brushes and
/// readout text are cached, so rendering and dragging don't allocate once every label has been seen.
/// </summary>
internal sealed class PanTiltPad : Control
{
    private const double ReadoutSize = 10.5;
    private const string ReadoutGap = "   ";

    private static readonly IBrush PadBackground = new SolidColorBrush(Color.FromRgb(0x17, 0x17, 0x19));
    private static readonly IPen BorderPen = new Pen(new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x40)), 1);
    private static readonly IPen GridPen = new Pen(new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x30)), 1);
    private static readonly IPen CentrePen = new Pen(new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x4C)), 1, new DashStyle([3, 3], 0));
    private static readonly IBrush Dot = new SolidColorBrush(Color.FromRgb(0x2E, 0xC4, 0xB6));
    private static readonly IPen CrossPen = new Pen(new SolidColorBrush(Color.FromArgb(110, 0x2E, 0xC4, 0xB6)), 1);
    private static readonly IPen DotPen = new Pen(Brushes.White, 1);
    private static readonly IPen DraggingDotPen = new Pen(Brushes.White, 2);
    private static readonly IBrush Muted = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A));
    private static readonly DegreeLabels PanLabels = new("Pan ");
    private static readonly DegreeLabels TiltLabels = new("Tilt ");

    private bool _dragging;

    /// <summary>Creates a centred pad.</summary>
    public PanTiltPad()
    {
        Cursor = new Cursor(StandardCursorType.Cross);
        ClipToBounds = true;
    }

    /// <summary>0..1, 0.5 at centre.</summary>
    public double Pan { get; private set; } = 0.5;

    /// <summary>0..1, 0.5 at centre; up on the pad is more tilt.</summary>
    public double Tilt { get; private set; } = 0.5;

    /// <summary>Degrees the full pan travel covers, used only for the readout.</summary>
    public double PanRange { get; set; } = 540;

    /// <summary>Degrees the full tilt travel covers, used only for the readout.</summary>
    public double TiltRange { get; set; } = 270;

    /// <summary>Raised with (pan, tilt), both 0..1, while the user drags; not when <see cref="Set"/> is called.</summary>
    public event Action<double, double>? Changed;

    /// <summary>Moves the dot without raising <see cref="Changed"/>; ignored while the user is dragging. Values are clamped to 0..1.</summary>
    public void Set(double pan, double tilt)
    {
        if (_dragging)
            return;
        (Pan, Tilt) = (Math.Clamp(pan, 0, 1), Math.Clamp(tilt, 0, 1));
        InvalidateVisual();
    }

    /// <inheritdoc />
    public override void Render(DrawingContext ctx)
    {
        var bounds = new Rect(Bounds.Size).Deflate(0.5);
        ctx.DrawRectangle(PadBackground, BorderPen, bounds, 4, 4);
        for (var i = 1; i < 4; i++)
        {
            var x = bounds.Width * i / 4;
            var y = bounds.Height * i / 4;
            ctx.DrawLine(i == 2 ? CentrePen : GridPen, new Point(x, 0), new Point(x, bounds.Height));
            ctx.DrawLine(i == 2 ? CentrePen : GridPen, new Point(0, y), new Point(bounds.Width, y));
        }

        var dot = new Point(Pan * bounds.Width, (1 - Tilt) * bounds.Height);
        ctx.DrawLine(CrossPen, new Point(dot.X, 0), new Point(dot.X, bounds.Height));
        ctx.DrawLine(CrossPen, new Point(0, dot.Y), new Point(bounds.Width, dot.Y));
        ctx.DrawEllipse(Dot, _dragging ? DraggingDotPen : DotPen, dot, 6, 6);
        DrawReadout(ctx, bounds);
    }

    private void DrawReadout(DrawingContext ctx, Rect bounds)
    {
        var pan = TextCache.Get(PanLabels.For((Pan - 0.5) * PanRange), ReadoutSize, Muted);
        var gap = TextCache.Get(ReadoutGap, ReadoutSize, Muted);
        var tilt = TextCache.Get(TiltLabels.For((Tilt - 0.5) * TiltRange), ReadoutSize, Muted);
        var y = bounds.Height - pan.Height - 4;
        ctx.DrawText(pan, new Point(6, y));
        ctx.DrawText(tilt, new Point(6 + pan.WidthIncludingTrailingWhitespace + gap.WidthIncludingTrailingWhitespace, y));
    }

    private void Move(Point p)
    {
        Pan = Math.Clamp(p.X / Math.Max(1, Bounds.Width), 0, 1);
        Tilt = Math.Clamp(1 - p.Y / Math.Max(1, Bounds.Height), 0, 1);
        InvalidateVisual();
        Changed?.Invoke(Pan, Tilt);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.ClickCount == 2)
        {
            // Double-click homes the head.
            Move(new Point(Bounds.Width / 2, Bounds.Height / 2));
            return;
        }
        _dragging = true;
        e.Pointer.Capture(this);
        Move(e.GetPosition(this));
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging)
            Move(e.GetPosition(this));
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        e.Pointer.Capture(null);
        InvalidateVisual();
    }
}
