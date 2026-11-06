using System.Text.Json;
using Avalonia.Controls;
using Lampion.App.Editor;
using Lampion.Core.Model;
using Lampion.Core.Persistence;

namespace Lampion.App.Views.Start;

/// <summary>What the start screen's cards do: create or open a show and hand it on, or explain why not.</summary>
internal sealed class StartActions
{
    private const string UntitledName = "Untitled show";

    private readonly Window _owner;
    private readonly Action<Project, string?> _choose;
    private readonly Action<string> _showError;

    /// <summary>Actions for the start screen <paramref name="owner"/>.</summary>
    /// <param name="owner">Owner of the file pickers.</param>
    /// <param name="choose">Receives the chosen show and its file (null when new).</param>
    /// <param name="showError">Shows a problem in plain words on the start screen.</param>
    public StartActions(Window owner, Action<Project, string?> choose, Action<string> showError)
    {
        _owner = owner;
        _choose = choose;
        _showError = showError;
    }

    /// <summary>Starts a bare show with no fixtures.</summary>
    public void NewEmpty() => _choose(ProjectFactory.Empty(), null);

    /// <summary>Starts an untitled show on <paramref name="layout"/>'s stage and rig.</summary>
    public void NewFromLayout(StageLayout layout)
    {
        var project = ProjectFactory.FromLayout(layout);
        project.Name = UntitledName;
        _choose(project, null);
    }

    /// <summary>Opens the demo show.</summary>
    public void OpenDemo() => _choose(ProjectFactory.Demo(), null);

    /// <summary>Opens the festival showcase.</summary>
    public void OpenShowcase() => _choose(ProjectFactory.Showcase(), null);

    /// <summary>Picks a show file and opens it.</summary>
    public async Task OpenFile()
    {
        var path = await Dialogs.PickOpen(_owner, "Open show", ("Lampion show", ["*" + ProjectSerializer.Extension]));
        if (path is not null)
            Open(path);
    }

    /// <summary>Opens the show in <paramref name="path"/>, or says why it can't.</summary>
    public void Open(string path)
    {
        Project project;
        try
        {
            project = ProjectSerializer.Load(path);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            ShowError($"Could not open {Path.GetFileName(path)}: {ex.Message}");
            return;
        }
        _choose(project, path);
    }

    /// <summary>Picks any stage layout file and starts a show on it.</summary>
    public async Task BrowseLayout()
    {
        var path = await Dialogs.PickOpen(_owner, "Start from a stage layout", ("Lampion stage layout", ["*" + StageLayout.Extension]));
        if (path is null)
            return;
        if (UserData.TryLoadLayout(path) is { } layout)
            NewFromLayout(layout);
        else
            ShowError($"{Path.GetFileName(path)} is not a readable stage layout.");
    }

    /// <summary>Shows <paramref name="message"/> on the start screen.</summary>
    public void ShowError(string message) => _showError(message);
}
