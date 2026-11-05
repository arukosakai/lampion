using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Lampion.App.Controls;

/// <summary>A console-style XY pad for a moving head: drag left/right to pan, up/down to tilt.</summary>
public sealed class PanTiltPad : Control
{
    private static readonly IBrush Background = new SolidColorBrush(Color.FromRgb(0x17, 0x17, 0x19));
    private static readonly IPen Border = new Pen(new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x40)), 1);
    private static readonly IPen GridPen = new Pen(new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x30)), 1);
    private static readonly IPen CentrePen = new Pen(new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x4C)), 1, new DashStyle([3, 3], 0));
    private static readonly IBrush Dot = new SolidColorBrush(Color.FromRgb(0x2E, 0xC4, 0xB6));
    private static readonly IPen CrossPen = new Pen(new SolidColorBrush(Color.FromArgb(110, 0x2E, 0xC4, 0xB6)), 1);
    private static readonly IBrush Muted = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A));

    private bool _dragging;

    public PanTiltPad()
    {
        Cursor = new Cursor(StandardCursorType.Cross);
        ClipToBounds = true;
    }

    /// <summary>0..1, 0.5 at centre.</summary>
    public double Pan { get; private set; } = 0.5;

    /// <summary>0..1, 0.5 at centre; up on the pad is more tilt.</summary>
    public double Tilt { get; private set; } = 0.5;

    public double PanRange { get; set; } = 540;
    public double TiltRange { get; set; } = 270;

    /// <summary>Raised while the user drags, not when <see cref="Set"/> is called.</summary>
    public event Action<double, double>? Changed;

    public void Set(double pan, double tilt)
    {
        if (_dragging)
            return;
        (Pan, Tilt) = (Math.Clamp(pan, 0, 1), Math.Clamp(tilt, 0, 1));
        InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        var bounds = new Rect(Bounds.Size).Deflate(0.5);
        ctx.DrawRectangle(Background, Border, bounds, 4, 4);
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
        ctx.DrawEllipse(Dot, new Pen(Brushes.White, _dragging ? 2 : 1), dot, 6, 6);

        var text = $"Pan {(Pan - 0.5) * PanRange:0}°   Tilt {(Tilt - 0.5) * TiltRange:0}°";
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 10.5, Muted);
        ctx.DrawText(formatted, new Point(6, bounds.Height - formatted.Height - 4));
    }

    private void Move(Point p)
    {
        Pan = Math.Clamp(p.X / Math.Max(1, Bounds.Width), 0, 1);
        Tilt = Math.Clamp(1 - p.Y / Math.Max(1, Bounds.Height), 0, 1);
        InvalidateVisual();
        Changed?.Invoke(Pan, Tilt);
    }

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

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging)
            Move(e.GetPosition(this));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        e.Pointer.Capture(null);
        InvalidateVisual();
    }
}
