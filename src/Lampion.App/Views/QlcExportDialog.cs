using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.Core.Export;
using Lampion.Core.Model;

namespace Lampion.App.Views;

/// <summary>File → Export to QLC+: options, the target file and the warnings report. Opened through <see cref="Dialogs.ExportQlc"/>.</summary>
internal static class QlcExportDialog
{
    private const string NoBase = "None — build a new workspace";
    private const double LabelWidth = 130;
    private static readonly double[] SampleRates = [10.0, 20.0, 25.0, 40.0, 50.0];

    /// <summary>Shows the dialog; the export itself runs off the UI thread.</summary>
    /// <param name="owner">The window the dialog is modal to.</param>
    /// <param name="project">The show to export.</param>
    /// <param name="projectPath">The show's file, so media paths can be made relative; null while unsaved.</param>
    public static async Task Show(Window owner, Project project, string? projectPath)
    {
        var options = new QlcExportOptions { ProjectDirectory = projectPath is null ? null : Path.GetDirectoryName(projectPath) };
        var window = Dialogs.Modal("Export to QLC+", 520);
        var baseLabel = new TextBlock { Text = NoBase, Foreground = Brushes.Gray, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        var rate = Ui.Combo(SampleRates, options.SampleRate, v => options.SampleRate = v, v => $"{v:0} Hz");
        var tolerance = Ui.Number(options.Tolerance, v => options.Tolerance = (int)v, 0, 10, 1, "0");
        var chooseBase = Ui.Button("Choose…", async () =>
        {
            var path = await Dialogs.PickOpen(window, "Base QLC+ workspace", ("QLC+ workspace", ["*.qxw"]));
            options.BaseWorkspacePath = path;
            baseLabel.Text = path ?? NoBase;
        });
        var clearBase = Ui.Button("✕", () =>
        {
            options.BaseWorkspacePath = null;
            baseLabel.Text = NoBase;
        });
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Gray };
        var export = new Button { Content = "Export…", MinWidth = 100, HorizontalAlignment = HorizontalAlignment.Right };
        export.Click += async (_, _) => await Export(window, project, options, status);
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
                Ui.Field("Sample rate", rate, LabelWidth),
                Ui.Field("Fade tolerance (DMX)", tolerance, LabelWidth),
                Ui.Field("Merge into", new DockPanel { Children = { clearBase, chooseBase, baseLabel } }, LabelWidth),
                export,
                status,
            },
        };
        DockPanel.SetDock(clearBase, Dock.Right);
        DockPanel.SetDock(chooseBase, Dock.Right);
        await window.ShowDialog(owner);
    }

    private static async Task Export(Window window, Project project, QlcExportOptions options, TextBlock status)
    {
        var suggested = (options.BaseWorkspacePath is { } b ? Path.GetFileNameWithoutExtension(b) : project.Name) + ".qxw";
        var target = await Dialogs.PickSave(window, "Save QLC+ workspace", suggested, "qxw", "QLC+ workspace");
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
    }
}
