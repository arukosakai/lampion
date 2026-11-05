using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lampion.App.Views.Shell;

/// <summary>Docked panel frames and the splitters between them, in the After Effects workspace style.</summary>
internal static class PanelLayout
{
    private static readonly IBrush HeaderBrush = new SolidColorBrush(Color.FromRgb(0x2B, 0x2B, 0x2B));

    /// <summary>A framed panel with a title bar, placed in grid column <paramref name="column"/>.</summary>
    public static Border Panel(string title, Control content, int column)
    {
        var header = new Border
        {
            Background = HeaderBrush,
            Child = new TextBlock { Text = title, Classes = { "header" } },
        };
        var dock = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        dock.Children.Add(header);
        dock.Children.Add(content);
        return Frame(dock, column);
    }

    /// <summary>A panel frame whose tabs are its header, like After Effects' Project / Effects &amp; Presets group.</summary>
    public static Border TabbedPanel(int column, params (string Title, Control Content)[] tabs)
    {
        var tabControl = new TabControl();
        foreach (var (title, content) in tabs)
            tabControl.Items.Add(new TabItem { Header = title, Content = content });
        return Frame(tabControl, column);
    }

    /// <summary>A transparent splitter: resizes columns when <paramref name="vertical"/> (placed in column <paramref name="index"/>), rows otherwise (place it yourself).</summary>
    public static GridSplitter Splitter(int index, bool vertical)
    {
        var splitter = new GridSplitter
        {
            Background = Brushes.Transparent,
            ResizeDirection = vertical ? GridResizeDirection.Columns : GridResizeDirection.Rows,
        };
        if (vertical)
            Grid.SetColumn(splitter, index);
        return splitter;
    }

    private static Border Frame(Control child, int column)
    {
        var border = new Border { Child = child, Classes = { "panel" }, Margin = new Thickness(0, 2) };
        Grid.SetColumn(border, column);
        return border;
    }
}
