using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Lampion.App.Editor;
using Lampion.Core.Model;

namespace Lampion.App.Views;

/// <summary>
/// Modal dialogs and file pickers, each awaited on the UI thread. Every dialog is modal to
/// <c>owner</c> and returns once it closes.
/// </summary>
internal static class Dialogs
{
    /// <summary>A small modal with a message and buttons; returns the label pressed, or null if closed.</summary>
    /// <param name="owner">The window the dialog is modal to.</param>
    /// <param name="title">Window title.</param>
    /// <param name="message">The question or explanation, in plain words.</param>
    /// <param name="buttons">Button labels, left to right.</param>
    public static async Task<string?> Ask(Window owner, string title, string message, params string[] buttons)
    {
        string? result = null;
        var window = Modal(title, 460);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var label in buttons)
        {
            var button = new Button { Content = label, MinWidth = 80 };
            button.Click += (_, _) =>
            {
                result = label;
                window.Close();
            };
            row.Children.Add(button);
        }
        window.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 14,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                row,
            },
        };
        await window.ShowDialog(owner);
        return result;
    }

    /// <summary>After Effects-style Project Settings: show-wide values that belong to no clip. Changes apply live, as one undo step.</summary>
    public static Task ProjectSettings(Window owner, EditorState state) => ProjectSettingsDialog.Show(owner, state);

    /// <summary>Asks for one line of text; null if cancelled.</summary>
    /// <param name="owner">The window the dialog is modal to.</param>
    /// <param name="title">Window title.</param>
    /// <param name="label">What to type, shown above the box.</param>
    /// <param name="initial">The text the box starts with, selected.</param>
    public static async Task<string?> Prompt(Window owner, string title, string label, string initial)
    {
        string? result = null;
        var box = new TextBox { Text = initial };
        var window = Modal(title, 420);
        var ok = new Button { Content = "OK", MinWidth = 80, IsDefault = true };
        ok.Click += (_, _) =>
        {
            result = box.Text;
            window.Close();
        };
        var cancel = new Button { Content = "Cancel", MinWidth = 80, IsCancel = true };
        cancel.Click += (_, _) => window.Close();
        window.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = label },
                box,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, cancel } },
            },
        };
        window.Opened += (_, _) =>
        {
            box.Focus();
            box.SelectAll();
        };
        await window.ShowDialog(owner);
        return result;
    }

    /// <summary>The system open-file picker for one file; returns its local path, or null if cancelled or not a local file.</summary>
    /// <param name="owner">The window the picker belongs to.</param>
    /// <param name="title">Picker title.</param>
    /// <param name="types">File type filters: a name and glob patterns such as "*.lampion".</param>
    public static async Task<string?> PickOpen(Window owner, string title, params (string Name, string[] Patterns)[] types)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = types.Select(t => new FilePickerFileType(t.Name) { Patterns = t.Patterns }).ToList(),
            }
        );
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    /// <summary>The system save-file picker, confirming overwrites; returns the local path, or null if cancelled.</summary>
    /// <param name="owner">The window the picker belongs to.</param>
    /// <param name="title">Picker title.</param>
    /// <param name="suggested">Suggested file name, with extension.</param>
    /// <param name="extension">Default extension without the dot, e.g. "lampion".</param>
    /// <param name="typeName">The file type's name in the picker.</param>
    public static async Task<string?> PickSave(Window owner, string title, string suggested, string extension, string typeName)
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = title,
                SuggestedFileName = suggested,
                DefaultExtension = extension,
                ShowOverwritePrompt = true,
                FileTypeChoices = [new FilePickerFileType(typeName) { Patterns = [$"*.{extension}"] }],
            }
        );
        return file?.TryGetLocalPath();
    }

    /// <summary>Options, file choice and the warnings report for a QLC+ export.</summary>
    /// <param name="owner">The window the dialog is modal to.</param>
    /// <param name="project">The show to export.</param>
    /// <param name="projectPath">The show's file, so media paths can be made relative; null while unsaved.</param>
    public static Task ExportQlc(Window owner, Project project, string? projectPath) => QlcExportDialog.Show(owner, project, projectPath);

    /// <summary>A fixed-size modal window, centred on its owner and as tall as its content.</summary>
    internal static Window Modal(string title, double width) =>
        new()
        {
            Title = title,
            Width = width,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
}
