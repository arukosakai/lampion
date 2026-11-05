using Avalonia.Controls;

namespace Lampion.App.Views.ProjectBin;

/// <summary>The right-click menu of each kind of bin row; every entry runs a <see cref="BinCommands"/> command.</summary>
internal sealed class BinContextMenus
{
    private readonly BinCommands _commands;

    /// <param name="commands">What the menu entries run.</param>
    public BinContextMenus(BinCommands commands) => _commands = commands;

    /// <summary>A new menu for <paramref name="item"/>'s row; every kind ends with Delete.</summary>
    public ContextMenu For(BinItem item)
    {
        var menu = new ContextMenu();
        switch (item.Kind)
        {
            case BinItemKind.Group:
                Add(menu, "New light track for this group", () => _commands.TrackForGroup(item.Id));
                Add(menu, "Rename…", () => _ = _commands.Rename(item));
                Add(menu, "Change colour…", () => _ = _commands.Recolour(item.Id));
                break;
            case BinItemKind.Fixture:
                Add(menu, "New light track for the selection", () => _commands.TrackForSelectedFixtures(item));
                Add(menu, "Group the selection", _commands.MakeGroup);
                Add(menu, "Rename…", () => _ = _commands.Rename(item));
                break;
            case BinItemKind.Profile:
                Add(menu, "Edit profile…", () => _commands.Open(item));
                Add(menu, "Patch fixtures of this type…", () => _ = _commands.NewFixtures());
                break;
            case BinItemKind.Media:
                Add(menu, "Rename…", () => _ = _commands.Rename(item));
                break;
        }
        menu.Items.Add(new Separator());
        Add(menu, "Delete", _commands.DeleteSelected);
        return menu;
    }

    private static void Add(ContextMenu menu, string header, Action action)
    {
        var entry = new MenuItem { Header = header };
        entry.Click += (_, _) => action();
        menu.Items.Add(entry);
    }
}
