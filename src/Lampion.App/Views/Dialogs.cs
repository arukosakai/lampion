using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Lampion.Core.Export;
using Lampion.Core.Model;

namespace Lampion.App.Views;

internal static class Dialogs
{
    /// <summary>A small modal with a message and buttons; returns the label pressed, or null if closed.</summary>
    public static async Task<string?> Ask(Window owner, string title, string message, params string[] buttons)
    {
        string? result = null;
        var window = new Window
        {
            Title = title,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
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

    /// <summary>Asks for one line of text; null if cancelled.</summary>
    public static async Task<string?> Prompt(Window owner, string title, string label, string initial)
    {
        string? result = null;
        var box = new TextBox { Text = initial };
        var window = new Window
        {
            Title = title,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        void Accept()
        {
            result = box.Text;
            window.Close();
        }
        var ok = new Button { Content = "OK", MinWidth = 80, IsDefault = true };
        ok.Click += (_, _) => Accept();
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
    public static async Task ExportQlc(Window owner, Project project, string? projectPath)
    {
        var options = new QlcExportOptions { ProjectDirectory = projectPath is null ? null : Path.GetDirectoryName(projectPath) };
        var window = new Window
        {
            Title = "Export to QLC+",
            Width = 520,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        var baseLabel = new TextBlock { Text = "None — build a new workspace", Foreground = Brushes.Gray, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        var rate = Ui.Combo([10.0, 20.0, 25.0, 40.0, 50.0], options.SampleRate, v => options.SampleRate = v, v => $"{v:0} Hz");
        var tolerance = Ui.Number(options.Tolerance, v => options.Tolerance = (int)v, 0, 10, 1, "0");
        var chooseBase = Ui.Button("Choose…", async () =>
        {
            var path = await PickOpen(window, "Base QLC+ workspace", ("QLC+ workspace", ["*.qxw"]));
            options.BaseWorkspacePath = path;
            baseLabel.Text = path ?? "None — build a new workspace";
        });
        var clearBase = Ui.Button("✕", () =>
        {
            options.BaseWorkspacePath = null;
            baseLabel.Text = "None — build a new workspace";
        });
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Gray };
        var export = new Button { Content = "Export…", MinWidth = 100, HorizontalAlignment = HorizontalAlignment.Right };
        export.Click += async (_, _) =>
        {
            var target = await PickSave(window, "Save QLC+ workspace", (options.BaseWorkspacePath is { } b ? Path.GetFileNameWithoutExtension(b) : project.Name) + ".qxw", "qxw", "QLC+ workspace");
            if (target is null)
                return;
            options.OutputDirectory = Path.GetDirectoryName(target);
            try
            {
                status.Text = "Rendering show…";
                var result = await Task.Run(() => new QlcExporter(project, options).Export());
                await File.WriteAllTextAsync(target, result.Xml);
                var warnings = result.Warnings.Count == 0 ? "No warnings." : string.Join("\n", result.Warnings.Select(w => "⚠ " + w));
                status.Foreground = Brushes.Gainsboro;
                status.Text = $"Saved {Path.GetFileName(target)}: {result.FixtureCount} fixtures, {result.StepCount} sequence steps.\n{warnings}\n\nIn QLC+, open the workspace and play the show from the Show Manager. Media files are referenced relative to the .qxw.";
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                status.Foreground = Brushes.OrangeRed;
                status.Text = "Export failed: " + ex.Message;
            }
        };
        window.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = "Every fixture becomes a QLC+ fixture at its DMX address (RGB/RGBW profiles as RGB panels). Each group gets a Sequence of the exact DMX Lampion computes, reduced to linear fades, all played by one Show with the audio and video.",
                    TextWrapping = TextWrapping.Wrap,
                },
                Ui.Field("Sample rate", rate, 130),
                Ui.Field("Fade tolerance (DMX)", tolerance, 130),
                Ui.Field("Merge into", new DockPanel { Children = { clearBase, chooseBase, baseLabel } }, 130),
                export,
                status,
            },
        };
        DockPanel.SetDock(clearBase, Dock.Right);
        DockPanel.SetDock(chooseBase, Dock.Right);
        await window.ShowDialog(owner);
    }
}
