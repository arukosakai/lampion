using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Lampion.App.Editor;
using Lampion.App.Views.Start;
using Lampion.Core.Model;

namespace Lampion.App.Views;

/// <summary>
/// The home screen shown on launch (and from File → New): recent shows, templates, a blank show or the demo.
/// </summary>
internal sealed class StartWindow : Window
{
    private readonly Action<StartChoice>? _onChosen;
    private readonly TextBlock _error = new() { Foreground = Brushes.OrangeRed, TextWrapping = TextWrapping.Wrap, IsVisible = false };

    /// <summary>A start screen; shown modally (Escape closes it) when <paramref name="onChosen"/> is null.</summary>
    /// <param name="onChosen">Receives the choice before the window closes; null when the caller reads <see cref="Result"/> instead.</param>
    public StartWindow(Action<StartChoice>? onChosen = null)
    {
        _onChosen = onChosen;
        Title = "Lampion";
        Width = 1040;
        Height = 660;
        MinWidth = 820;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var actions = new StartActions(this, Choose, ShowError);
        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("300,*") };
        root.Children.Add(new StartSidebar(actions));
        var content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Thickness(32, 28, 32, 28),
                Spacing = 10,
                Children =
                {
                    StartCard.Heading("Recent shows"),
                    new RecentShowsList(actions),
                    StartCard.Heading("Start from a template", top: 18),
                    new TemplateGallery(actions),
                    _error,
                },
            },
        };
        Grid.SetColumn(content, 1);
        root.Children.Add(content);
        Content = root;

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && _onChosen is null)
                Close();
        };
    }

    /// <summary>What the user picked; null if the window was closed without choosing.</summary>
    public StartChoice? Result { get; private set; }

    private void Choose(Project project, string? path)
    {
        Result = new StartChoice(project, path);
        if (path is not null)
            UserData.AddRecent(path);
        _onChosen?.Invoke(Result);
        Close();
    }

    private void ShowError(string message)
    {
        _error.Text = message;
        _error.IsVisible = true;
    }
}
