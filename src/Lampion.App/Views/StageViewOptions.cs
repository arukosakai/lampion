using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lampion.App.Controls;
using Lampion.App.Editor;
using Lampion.App.Rendering;

namespace Lampion.App.Views;

/// <summary>The stage view's display options, tucked into one dropdown like Unity's: labels, bounce, haze, reset and quality.</summary>
internal sealed class StageViewOptions : StackPanel
{
    /// <summary>Options for <paramref name="view"/>; the bounce and haze controls edit <paramref name="state"/>'s stage and follow project changes.</summary>
    public StageViewOptions(StageView view, EditorState state)
    {
        var labels = new CheckBox { Content = "Labels", IsChecked = view.ShowLabels };
        labels.IsCheckedChanged += (_, _) => view.ShowLabels = labels.IsChecked == true;
        var bounce = new CheckBox { Content = "Light bounce", IsChecked = state.Project.Stage.LightBounce };
        ToolTip.SetTip(bounce, "Let the lit rig spill a little coloured light into the room");
        bounce.IsCheckedChanged += (_, _) =>
        {
            if (state.Project.Stage.LightBounce == (bounce.IsChecked == true))
                return;
            state.Project.Stage.LightBounce = bounce.IsChecked == true;
            state.Commit();
        };
        var haze = Ui.Slider(state.Project.Stage.Haze, 0, 1, v => state.Project.Stage.Haze = v, () => state.Commit());
        haze.Width = 150;
        state.ProjectReplaced += () =>
        {
            bounce.IsChecked = state.Project.Stage.LightBounce;
            haze.Value = state.Project.Stage.Haze;
        };
        var reset = Ui.Button("Reset view", () => view.ApplyPreset(ViewPreset.Free), "Back to the default perspective view (1)");
        reset.Margin = new Thickness(0, 6, 0, 0);

        Spacing = 4;
        Width = 250;
        Children.Add(labels);
        Children.Add(bounce);
        Children.Add(new TextBlock { Text = "Haze", Foreground = Brushes.Gray, Margin = new Thickness(0, 6, 0, 0) });
        Children.Add(haze);
        Children.Add(reset);
        Children.Add(new StageQualityOptions(view));
    }
}
