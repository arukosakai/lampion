using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Lampion.App.Editor;

namespace Lampion.App.Views;

/// <summary>File → Project settings: name, length, tempo and the stage look. Opened through <see cref="Dialogs.ProjectSettings"/>.</summary>
internal static class ProjectSettingsDialog
{
    private const double LabelWidth = 120;
    private static readonly double[] GridSteps = [0.05, 0.1, 0.25, 0.5, 1.0];

    /// <summary>Shows the dialog; every change applies live (<see cref="EditorState.Touch"/>) and closing commits them as one undo step.</summary>
    public static async Task Show(Window owner, EditorState state)
    {
        var project = state.Project;
        var stage = project.Stage;
        var window = Dialogs.Modal("Project Settings", 440);
        var bounce = new CheckBox { IsChecked = stage.LightBounce };
        bounce.IsCheckedChanged += (_, _) =>
        {
            stage.LightBounce = bounce.IsChecked == true;
            state.Touch();
        };
        var close = new Button { Content = "Done", MinWidth = 90, HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true };
        close.Click += (_, _) => window.Close();
        window.Content = new StackPanel
        {
            Margin = new Thickness(18),
            Spacing = 6,
            Children =
            {
                Ui.Section("Show"),
                Row("Name", Ui.Text(project.Name, v => { project.Name = v; state.Touch(); })),
                Row("Length (s)", Ui.Number(project.Duration, v => { project.Duration = v; state.Touch(); }, 1, 36000, 1, "0.#")),
                Row("Tempo (BPM)", Ui.Number(project.Bpm, v => { project.Bpm = v; state.Touch(); }, 20, 300, 1, "0.#")),
                Row("First beat (s)", Ui.Number(project.BeatOffset, v => { project.BeatOffset = v; state.Touch(); }, 0, 60, 0.01, "0.00")),
                Ui.Section("Stage look"),
                Row("Haze", Ui.Slider(stage.Haze, 0, 1, v => { stage.Haze = v; state.Touch(); })),
                Row("House light", Ui.Slider(stage.Ambient, 0, 1, v => { stage.Ambient = v; state.Touch(); })),
                Row("Light bounce", bounce),
                Row("Snap step", Ui.Combo(GridSteps, stage.GridStep, v => { stage.GridStep = v; state.Touch(); }, v => $"{v:0.##} m")),
                close,
            },
        };
        await window.ShowDialog(owner);
        // One undo step for everything changed while the dialog was open.
        state.Commit();
    }

    private static Grid Row(string label, Control editor) => Ui.Field(label, editor, LabelWidth);
}
