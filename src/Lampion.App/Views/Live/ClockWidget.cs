using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Lampion.App.Views.Live;

/// <summary>The time of day in big digits, with the date under it, for running a show against a schedule.</summary>
internal sealed class ClockWidget : Border
{
    private readonly TextBlock _time = new() { FontSize = 40, FontWeight = FontWeight.Bold, FontFamily = new FontFamily("Consolas, Menlo, monospace"), HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock _date = new() { FontSize = 12, Foreground = Brushes.Gray, HorizontalAlignment = HorizontalAlignment.Center };
    private long _shownSecond = -1;

    /// <summary>A clock; <paramref name="menu"/> is its right-click menu.</summary>
    public ClockWidget(ContextMenu menu)
    {
        WidgetFrame.Apply(this, menu);
        Child = new StackPanel { Children = { _time, _date } };
        Refresh();
    }

    /// <summary>Shows the current time. Call every UI tick; text is only rebuilt when the second changes.</summary>
    public void Refresh()
    {
        var now = DateTime.Now;
        var second = now.Ticks / TimeSpan.TicksPerSecond;
        if (second == _shownSecond)
            return;
        _shownSecond = second;
        _time.Text = now.ToString("HH:mm:ss");
        _date.Text = now.ToString("dddd, d MMMM");
    }
}
