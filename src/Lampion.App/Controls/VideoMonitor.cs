using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Lampion.App.Rendering;

namespace Lampion.App.Controls;

/// <summary>The Video tab of the program monitor: shows the latest decoded video frame, letterboxed.</summary>
internal sealed class VideoMonitor : Control
{
    private const string NoVideo = "No video at the playhead. Import one from the Media tab.";
    private static readonly Typeface Font = new("Inter");

    private readonly VideoFrame _frame;
    private WriteableBitmap? _bitmap;
    private int _version = -1;
    private FormattedText? _placeholder;
    private string? _message;

    /// <summary>A monitor showing <paramref name="frame"/>, which a decoder fills from another thread under a lock on itself.</summary>
    public VideoMonitor(VideoFrame frame) => _frame = frame;

    /// <summary>Shown instead of the usual hint while there is no video; null for the hint.</summary>
    public string? Message
    {
        get => _message;
        set
        {
            if (value == _message)
                return;
            _message = value;
            _placeholder = null;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Copies a new frame into the bitmap, if one arrived; call once per UI tick. Allocation-free while the
    /// video size stays the same, apart from Avalonia's framebuffer lock handle per new frame.
    /// </summary>
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

    /// <inheritdoc />
    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
        if (_bitmap is null)
        {
            var text = _placeholder ??= new FormattedText(_message ?? NoVideo, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Font, 13, Brushes.Gray);
            ctx.DrawText(text, new Point((Bounds.Width - text.Width) / 2, (Bounds.Height - text.Height) / 2));
            return;
        }
        var scale = Math.Min(Bounds.Width / _bitmap.PixelSize.Width, Bounds.Height / _bitmap.PixelSize.Height);
        var w = _bitmap.PixelSize.Width * scale;
        var h = _bitmap.PixelSize.Height * scale;
        ctx.DrawImage(_bitmap, new Rect((Bounds.Width - w) / 2, (Bounds.Height - h) / 2, w, h));
    }
}
