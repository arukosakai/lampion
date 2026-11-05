using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Lampion.App.Editor;
using Lampion.App.Views;
using Lampion.Core.Model;
using Lampion.Core.Persistence;

namespace Lampion.App;

/// <summary>
/// The Avalonia application: loads the theme from App.axaml, then opens the show passed on the command
/// line, or the start screen when there is none (or it can't be read).
/// </summary>
public partial class App : Application
{
    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var path = desktop.Args?.FirstOrDefault();
            if (path is not null && TryLoad(path) is { } project)
            {
                UserData.AddRecent(path);
                desktop.MainWindow = new MainWindow(project, path);
            }
            else
            {
                // The editor opens before the start screen closes, so the app never runs out of windows in between.
                desktop.MainWindow = new StartWindow(choice =>
                {
                    var main = new MainWindow(choice.Project, choice.Path);
                    desktop.MainWindow = main;
                    main.Show();
                });
            }
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static Project? TryLoad(string path)
    {
        try
        {
            return File.Exists(path) ? ProjectSerializer.Load(path) : null;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
