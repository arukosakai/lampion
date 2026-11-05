using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Lampion.App.Controls;
using Lampion.App.Editor;

namespace Lampion.App.Views.Shell;

/// <summary>The DMX output tab of the program monitor: a universe picker over the channel grid.</summary>
internal sealed class DmxOutputView : DockPanel
{
    private const int MaxUniverse = 64;

    /// <summary>Shows the output of <paramref name="state"/>, starting at universe 1.</summary>
    public DmxOutputView(EditorState state)
    {
        var monitor = new DmxMonitor(state);
        var universe = Ui.Number(1, v =>
        {
            monitor.Universe = (int)v;
            monitor.InvalidateVisual();
        }, 1, MaxUniverse, 1, "0");
        universe.Width = 100;
        var bar = Ui.Row(Ui.Label("Universe"), universe);
        bar.Margin = new Thickness(6);
        SetDock(bar, Dock.Top);
        Children.Add(bar);
        Children.Add(new ScrollViewer { Content = monitor, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto });
    }
}
