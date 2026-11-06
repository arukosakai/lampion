using Avalonia.Controls;
using Avalonia.Input;

namespace Lampion.App.Views.Shell;

/// <summary>Builds the menu bar from <see cref="EditorCommands"/>; a null entry is a separator.</summary>
internal static class MainMenu
{
    /// <summary>File, Edit, Timeline, Stage and Output menus over <paramref name="c"/>.</summary>
    public static Menu Build(EditorCommands c) =>
        new()
        {
            Items =
            {
                Top("_File", c.NewProject, c.Open, c.Save, c.SaveAs, null, c.ProjectSettings, null, c.ImportAudio, c.ImportVideo, null, c.ExportQlc, null, c.Exit),
                Top("_Edit", c.Undo, c.Redo, null, c.Copy, c.Paste, c.Duplicate, c.Delete),
                Top("_Timeline", c.PlayPause, c.GoToStart, c.SplitAtPlayhead, c.AddMarker, c.NewLightTrack, c.ZoomToFit),
                Top(
                    "_Stage",
                    c.PerspectiveView,
                    c.TopView,
                    c.FrontView,
                    c.RightView,
                    c.ToggleProjection,
                    null,
                    c.MoveTool,
                    c.AimTool,
                    c.FrameSelection,
                    null,
                    c.BuildTruss,
                    c.ImportModel,
                    null,
                    c.ExportLayout,
                    c.ImportLayout,
                    c.SaveTemplate,
                    c.UseAsDefaultLayout,
                    c.ForgetDefaultLayout
                ),
                Top("_Output", c.ToggleOutput, null, c.OutputSetup),
            },
        };

    private static MenuItem Top(string header, params ShellCommand?[] commands)
    {
        var menu = new MenuItem { Header = header };
        foreach (var command in commands)
            menu.Items.Add(command is null ? new Separator() : Item(command));
        return menu;
    }

    private static MenuItem Item(ShellCommand command)
    {
        var item = new MenuItem { Header = command.Header };
        if (command.Gesture is not null)
            item.InputGesture = KeyGesture.Parse(command.Gesture);
        if (command.IsChecked is { } isChecked)
        {
            item.ToggleType = MenuItemToggleType.CheckBox;
            item.Click += (_, _) =>
            {
                command.Run();
                item.IsChecked = isChecked();
            };
        }
        else
            item.Click += (_, _) => command.Run();
        return item;
    }
}
