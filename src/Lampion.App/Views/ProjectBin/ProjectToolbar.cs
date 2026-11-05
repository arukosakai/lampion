using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.App.Controls;

namespace Lampion.App.Views.ProjectBin;

/// <summary>The strip under the bin: the ＋ New menu, the fixture/group count and Delete.</summary>
internal sealed class ProjectToolbar : DockPanel
{
    private readonly TextBlock _count = new() { Foreground = Brushes.Gray, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };

    /// <param name="commands">Runs the New and Delete entries.</param>
    /// <param name="importAudio">Imports an audio file onto the timeline.</param>
    /// <param name="importVideo">Imports a video file onto the timeline.</param>
    public ProjectToolbar(BinCommands commands, Func<Task> importAudio, Func<Task> importVideo)
    {
        Margin = new Thickness(6, 4);
        LastChildFill = false;

        var menu = new MenuFlyout();
        Add(menu, "Fixtures…", () => _ = commands.NewFixtures());
        Add(menu, "Group from selection", commands.MakeGroup);
        Add(menu, "Fixture profile…", () => _ = commands.NewProfile());
        menu.Items.Add(new Separator());
        Add(menu, "Import audio…", () => _ = importAudio());
        Add(menu, "Import video…", () => _ = importVideo());

        var newButton = new Button { Content = "＋ New", Flyout = menu, Padding = new Thickness(8, 2) };
        ToolTip.SetTip(newButton, "New fixtures, groups, profiles, or import media");
        var delete = new GlyphButton("🗑", "Delete the selected items (Del)", commands.DeleteSelected);
        SetDock(newButton, Dock.Left);
        SetDock(delete, Dock.Right);
        SetDock(_count, Dock.Left);
        Children.Add(newButton);
        Children.Add(_count);
        Children.Add(delete);
    }

    /// <summary>Updates the "N fixtures · M groups" readout. Allocates; call when the bin is rebuilt.</summary>
    public void ShowCounts(int fixtures, int groups) => _count.Text = $"{fixtures} fixtures · {groups} groups";

    private static void Add(MenuFlyout menu, string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        menu.Items.Add(item);
    }
}
