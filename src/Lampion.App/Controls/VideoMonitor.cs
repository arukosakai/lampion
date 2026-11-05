using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Lampion.App.Rendering;

namespace Lampion.App.Controls;

/// <summary>The "Program monitor": shows the latest decoded video frame, letterboxed.</summary>
public sealed class VideoMonitor : Control
{
    private readonly VideoFrame _frame;
    private WriteableBitmap? _bitmap;
    private int _version = -1;

    public VideoMonitor(VideoFrame frame) => _frame = frame;

    public string? Message { get; set; }

    /// <summary>Copies a new frame into the bitmap, if one arrived; call once per UI tick.</summary>
    public void Update()
    {
        if (_frame.Version == _version || _frame.Width == 0)
            return;
        lock (_frame)
        {
            _version = _frame.Version;
            if (_bitmap is null || _bitmap.PixelSize.Width != _frame.Width || _bitmap.PixelSize.Height != _frame.Height)
            {
                _bitmap?.Dispose();
                _bitmap = new WriteableBitmap(new PixelSize(_frame.Width, _frame.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            }
            using var target = _bitmap.Lock();
            var rowBytes = _frame.Width * 4;
            for (var y = 0; y < _frame.Height; y++)
                Marshal.Copy(_frame.Pixels, y * rowBytes, target.Address + y * target.RowBytes, rowBytes);
        }
        InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
        if (_bitmap is null)
        {
            var text = new FormattedText(
                Message ?? "No video at the playhead. Import one from the Media tab.",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter"),
                13,
                Brushes.Gray
            );
            ctx.DrawText(text, new Point((Bounds.Width - text.Width) / 2, (Bounds.Height - text.Height) / 2));
            return;
        }
        var scale = Math.Min(Bounds.Width / _bitmap.PixelSize.Width, Bounds.Height / _bitmap.PixelSize.Height);
        var w = _bitmap.PixelSize.Width * scale;
        var h = _bitmap.PixelSize.Height * scale;
        ctx.DrawImage(_bitmap, new Rect((Bounds.Width - w) / 2, (Bounds.Height - h) / 2, w, h));
    }
}
