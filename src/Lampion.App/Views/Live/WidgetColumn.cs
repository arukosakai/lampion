using Avalonia;
using Avalonia.Controls;
using Lampion.App.Editor;
using Lampion.App.Live;
using Lampion.Core.Model.Live;

namespace Lampion.App.Views.Live;

/// <summary>The live console's right-hand column of widgets (clocks, photos), added with + Widget and removed by right-click.</summary>
internal sealed class WidgetColumn : DockPanel
{
    private static readonly (string Name, string[] Patterns) Images = ("Pictures", ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp", "*.gif"]);

    private readonly Window _owner;
    private readonly EditorState _state;
    private readonly StackPanel _widgets = new();
    private readonly List<ClockWidget> _clocks = [];

    /// <summary>A column over <paramref name="state"/>'s live set, rebuilt whenever <paramref name="session"/> rebuilds its patch.</summary>
    public WidgetColumn(Window owner, EditorState state, LiveSession session)
    {
        _owner = owner;
        _state = state;
        Width = 280;
        Margin = new Thickness(10, 0, 0, 0);

        var add = new Button { Content = "+ Widget" };
        ToolTip.SetTip(add, "Add a clock or a photo to this column");
        add.Flyout = new MenuFlyout
        {
            Items =
            {
                MenuItem("Clock", () => LiveEdits.AddWidget(_state, LiveWidgetKind.Clock)),
                MenuItem("Photo…", () => _ = AddPhoto()),
            },
        };
        var header = Ui.Row(Ui.Section("Widgets"), add);
        header.Margin = new Thickness(0, 0, 0, 6);
        SetDock(header, Dock.Top);
        Children.Add(header);
        Children.Add(new ScrollViewer { Content = _widgets });

        session.Changed += Rebuild;
        Rebuild();
    }

    /// <summary>Ticks the clocks. Call every UI tick.</summary>
    public void Refresh()
    {
        foreach (var clock in _clocks)
            clock.Refresh();
    }

    private void Rebuild()
    {
        _widgets.Children.Clear();
        _clocks.Clear();
        foreach (var widget in _state.Project.Live.Widgets)
        {
            var remove = MenuItem("Remove", () => LiveEdits.RemoveWidget(_state, widget));
            if (widget.Kind == LiveWidgetKind.Clock)
            {
                var clock = new ClockWidget(new ContextMenu { Items = { remove } });
                _clocks.Add(clock);
                _widgets.Children.Add(clock);
            }
            else
            {
                var change = MenuItem("Change photo…", () => _ = ChangePhoto(widget));
                _widgets.Children.Add(new PhotoWidget(LiveEdits.Resolve(_state.FilePath, widget.ImagePath), new ContextMenu { Items = { change, remove } }));
            }
        }
        if (_state.Project.Live.Widgets.Count == 0)
            _widgets.Children.Add(Ui.Label("+ Widget adds a clock, or a photo to keep you company.", null));
    }

    private async Task AddPhoto()
    {
        if (await Dialogs.PickOpen(_owner, "Choose a photo", Images) is { } path)
            LiveEdits.AddWidget(_state, LiveWidgetKind.Photo, path);
    }

    private async Task ChangePhoto(LiveWidget widget)
    {
        if (await Dialogs.PickOpen(_owner, "Choose a photo", Images) is { } path)
            LiveEdits.SetPhoto(_state, widget, path);
    }

    private static MenuItem MenuItem(string header, Action run)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => run();
        return item;
    }
}
