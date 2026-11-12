using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Lampion.App.Views.Live;

/// <summary>A picture in the widget column. Decoded off the UI thread at widget size, so a big camera photo can't stall the console.</summary>
internal sealed class PhotoWidget : Border
{
    // Twice the column width, so the picture stays sharp on high-DPI screens.
    private const int DecodeWidth = 560;

    private static readonly Dictionary<string, Task<Bitmap?>> Cache = [];

    /// <summary>Shows the picture at <paramref name="path"/> (a full path); <paramref name="menu"/> is its right-click menu.</summary>
    public PhotoWidget(string path, ContextMenu menu)
    {
        WidgetFrame.Apply(this, menu);
        Child = Message("Loading…");
        _ = Show(path);
    }

    private async Task Show(string path)
    {
        if (!Cache.TryGetValue(path, out var load))
            Cache[path] = load = Task.Run(() => Decode(path));
        if (await load is { } bitmap)
        {
            Child = new Image { Source = bitmap, Stretch = Stretch.Uniform, MaxHeight = 360 };
            return;
        }
        // Not cached, so the picture shows up once the file is put back.
        Cache.Remove(path);
        Child = Message($"Can't show {Path.GetFileName(path)}: the file is missing or isn't a picture. Right-click → Change photo…");
    }

    private static Bitmap? Decode(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return Bitmap.DecodeToWidth(stream, DecodeWidth, BitmapInterpolationMode.HighQuality);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or InvalidOperationException)
        {
            return null;
        }
    }

    private static TextBlock Message(string text) =>
        new() { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Gray, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center };
}
