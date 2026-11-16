using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Runtime.InteropServices;
using Lampion.Core.Color;

namespace Lampion.App.Controls;

/// <summary>
/// A hue/saturation disc: hue goes around (red on the right, counter-clockwise through yellow and green),
/// saturation grows from the grey centre to the rim. Click or drag to pick. UI thread only.
/// </summary>
internal sealed class ColorWheel : Control
{
    private static readonly IPen MarkerOuter = new Pen(Brushes.Black, 3);
    private static readonly IPen MarkerInner = new Pen(Brushes.White, 1.5);
    private const double MarkerRadius = 5;

    private WriteableBitmap? _disc;
    private double _discBrightness = -1;
    private bool _dragging;

    /// <summary>Raised while the user clicks or drags a new hue/saturation; not raised when set from code.</summary>
    public event Action? Picked;

    /// <summary>Hue in turns, 0..1 (0 = red, 1/3 = green, 2/3 = blue).</summary>
    public double Hue { get; private set; }

    /// <summary>Saturation, 0 (centre) to 1 (rim).</summary>
    public double Saturation { get; private set; }

    /// <summary>Brightness the disc is painted at, 0..1; the wheel shows how hue and saturation look at this level.</summary>
    public double Brightness { get; private set; } = 1;

    /// <summary>Moves the marker and repaints at <paramref name="brightness"/> without raising <see cref="Picked"/>.</summary>
    public void Set(double hue, double saturation, double brightness)
    {
        Hue = hue;
        Saturation = Math.Clamp(saturation, 0, 1);
        Brightness = Math.Clamp(brightness, 0, 1);
        InvalidateVisual();
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var diameter = Diameter;
        if (diameter < 4)
            return;
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var pixels = Math.Max(1, (int)Math.Round(diameter * scale));
        if (_disc is null || _disc.PixelSize.Width != pixels || Math.Abs(_discBrightness - Brightness) > 1e-4)
            Paint(pixels);
        context.DrawImage(_disc!, new Rect(0, 0, diameter, diameter));

        var radius = diameter / 2;
        var angle = Hue * 2 * Math.PI;
        var marker = new Point(radius + Math.Cos(angle) * Saturation * radius, radius - Math.Sin(angle) * Saturation * radius);
        context.DrawEllipse(null, MarkerOuter, marker, MarkerRadius, MarkerRadius);
        context.DrawEllipse(null, MarkerInner, marker, MarkerRadius, MarkerRadius);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        _dragging = true;
        e.Pointer.Capture(this);
        Pick(e.GetPosition(this));
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging)
            Pick(e.GetPosition(this));
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        e.Pointer.Capture(null);
    }

    private double Diameter => Math.Min(Bounds.Width, Bounds.Height);

    private void Pick(Point p)
    {
        var radius = Diameter / 2;
        if (radius <= 0)
            return;
        var dx = p.X - radius;
        var dy = radius - p.Y;
        Hue = (Math.Atan2(dy, dx) / (2 * Math.PI) + 1) % 1;
        Saturation = Math.Min(1, Math.Sqrt(dx * dx + dy * dy) / radius);
        InvalidateVisual();
        Picked?.Invoke();
    }

    // Only reruns when the size or brightness changes, so dragging the marker never repaints the disc.
    private void Paint(int size)
    {
        _disc?.Dispose();
        _disc = new WriteableBitmap(new PixelSize(size, size), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        _discBrightness = Brightness;
        var radius = size / 2.0;
        var row = new int[size];
        using var buffer = _disc.Lock();
        for (var y = 0; y < size; y++)
        {
            var dy = radius - (y + 0.5);
            for (var x = 0; x < size; x++)
            {
                var dx = x + 0.5 - radius;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                var coverage = Math.Clamp(radius - distance, 0, 1);
                if (coverage <= 0)
                {
                    row[x] = 0;
                    continue;
                }
                var hue = Math.Atan2(dy, dx) / (2 * Math.PI);
                var color = Rgb.FromHsv(hue, Math.Min(1, distance / radius), Brightness);
                row[x] = Premultiplied(color, coverage);
            }
            Marshal.Copy(row, 0, buffer.Address + y * buffer.RowBytes, size);
        }
    }

    private static int Premultiplied(Rgb color, double alpha)
    {
        var a = (int)Math.Round(alpha * 255);
        var r = (int)Math.Round(Rgb.Clamp01(color.R) * a);
        var g = (int)Math.Round(Rgb.Clamp01(color.G) * a);
        var b = (int)Math.Round(Rgb.Clamp01(color.B) * a);
        return (a << 24) | (r << 16) | (g << 8) | b;
    }
}
