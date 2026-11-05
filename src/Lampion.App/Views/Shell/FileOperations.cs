using System.Text.Json;
using Avalonia.Controls;
using Lampion.App.Editor;
using Lampion.App.Media;
using Lampion.Core.Model;
using Lampion.Core.Persistence;

namespace Lampion.App.Views.Shell;

/// <summary>
/// New, open, save, save as and QLC+ export for the workspace, plus the unsaved-changes prompt they and
/// closing the window share. Failures are explained in a dialog; nothing here throws.
/// </summary>
internal sealed class FileOperations
{
    private readonly Window _owner;
    private readonly EditorState _state;
    private readonly MediaEngine _media;
    private readonly StatusBar _status;
    private readonly Action _projectLoaded;
    private bool _closeConfirmed;

    /// <summary>File operations for the show in <paramref name="state"/>.</summary>
    /// <param name="owner">Owner of the dialogs; closed again by <see cref="ConfirmClose"/> once the user decided.</param>
    /// <param name="state">The editor state.</param>
    /// <param name="media">Stopped before a project is replaced.</param>
    /// <param name="status">Where outcomes are reported.</param>
    /// <param name="projectLoaded">Runs after a different show was loaded (e.g. to zoom the timeline to fit).</param>
    public FileOperations(Window owner, EditorState state, MediaEngine media, StatusBar status, Action projectLoaded)
    {
        _owner = owner;
        _state = state;
        _media = media;
        _status = status;
        _projectLoaded = projectLoaded;
    }

    /// <summary>Asks to save unsaved changes; false means the user cancelled (or saving failed).</summary>
    public async Task<bool> ConfirmDiscard()
    {
        if (!_state.Dirty)
            return true;
        var answer = await Dialogs.Ask(_owner, "Unsaved changes", $"Save changes to \"{_state.Project.Name}\" first?", "Save", "Don't save", "Cancel");
        return answer switch
        {
            "Save" => await Save(false),
            "Don't save" => true,
            _ => false,
        };
    }

    /// <summary>Shows the start screen and loads what the user picks there.</summary>
    public async Task NewProject()
    {
        if (!await ConfirmDiscard())
            return;
        var start = new StartWindow();
        await start.ShowDialog(_owner);
        if (start.Result is not { } choice)
            return;
        Load(choice.Project, choice.Path);
    }

    /// <summary>Picks a show file and opens it.</summary>
    public async Task OpenProject()
    {
        if (!await ConfirmDiscard())
            return;
        var path = await Dialogs.PickOpen(_owner, "Open show", ("Lampion show", ["*" + ProjectSerializer.Extension]));
        if (path is null)
            return;
        try
        {
            var project = ProjectSerializer.Load(path);
            Load(project, path);
            UserData.AddRecent(path);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            await Dialogs.Ask(_owner, "Could not open", ex.Message, "OK");
        }
    }

    /// <summary>Saves to the show's file, asking for one first when <paramref name="saveAs"/> or never saved.</summary>
    /// <returns>True when the show is now on disk.</returns>
    public async Task<bool> Save(bool saveAs)
    {
        var path = _state.FilePath;
        if (saveAs || path is null)
            path = await Dialogs.PickSave(_owner, "Save show", _state.Project.Name + ProjectSerializer.Extension, ProjectSerializer.Extension.TrimStart('.'), "Lampion show");
        if (path is null)
            return false;
        try
        {
            ProjectSerializer.Save(_state.Project, path);
            _state.MarkSaved(path);
            UserData.AddRecent(path);
            _status.Message = $"Saved {Path.GetFileName(path)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await Dialogs.Ask(_owner, "Could not save", ex.Message, "OK");
            return false;
        }
    }

    /// <summary>Opens the QLC+ export dialog for the show.</summary>
    public Task ExportQlc() => Dialogs.ExportQlc(_owner, _state.Project, _state.FilePath);

    /// <summary>
    /// Call from <see cref="Window.OnClosing"/>. With unsaved changes the first close is cancelled while the
    /// user is asked; if they don't cancel, the window is closed again and this returns true.
    /// </summary>
    /// <param name="e">The closing event; cancelled while the user is asked.</param>
    /// <returns>True when the window really closes now, so its resources can be released.</returns>
    public bool ConfirmClose(WindowClosingEventArgs e)
    {
        if (_closeConfirmed || !_state.Dirty)
            return true;
        e.Cancel = true;
        _ = AskThenClose();
        return false;
    }

    private async Task AskThenClose()
    {
        if (!await ConfirmDiscard())
            return;
        _closeConfirmed = true;
        _owner.Close();
    }

    private void Load(Project project, string? path)
    {
        _media.StopAll();
        _state.Load(project, path);
        _projectLoaded();
    }
}
