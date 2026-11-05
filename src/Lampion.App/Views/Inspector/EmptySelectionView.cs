using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Lampion.App.Views.Inspector;

/// <summary>What the inspector shows with nothing selected: a hint and a way into the project settings.</summary>
internal static class EmptySelectionView
{
    /// <summary>Builds the placeholder.</summary>
    /// <param name="openProjectSettings">Runs when "Project settings…" is clicked.</param>
    public static Control Create(Action openProjectSettings) =>
        new StackPanel
        {
            Margin = new Thickness(0, 60, 0, 0),
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                new TextBlock { Text = "(no selection)", Foreground = Brushes.Gray, HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock
                {
                    Text = "Select a clip, track, fixture or set piece\nto see its controls here.",
                    Foreground = Brushes.DimGray,
                    FontSize = 11,
                    TextAlignment = TextAlignment.Center,
                },
                Ui.Button("Project settings…", openProjectSettings),
            },
        };
}
