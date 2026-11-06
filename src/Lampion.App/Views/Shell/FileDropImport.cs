using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Lampion.Core.Meshes;

namespace Lampion.App.Views.Shell;

/// <summary>Imports files dragged onto the window from the system file manager: 3D models onto the stage, audio and video onto the timeline.</summary>
internal sealed class FileDropImport
{
    private readonly MediaImport _media;
    private readonly ModelImport _models;
    private readonly StatusBar _status;

    private FileDropImport(MediaImport media, ModelImport models, StatusBar status) => (_media, _models, _status) = (media, models, status);

    /// <summary>Lets <paramref name="window"/> take file drops and hands each file to the import that handles it; failures go to <paramref name="status"/>.</summary>
    public static void Attach(Window window, MediaImport media, ModelImport models, StatusBar status)
    {
        var drop = new FileDropImport(media, models, status);
        DragDrop.SetAllowDrop(window, true);
        window.AddHandler(DragDrop.DragOverEvent, drop.OnDragOver, RoutingStrategies.Bubble, handledEventsToo: true);
        window.AddHandler(DragDrop.DropEvent, drop.OnDrop, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    // Only show the copy cursor when something in the drag is importable, so a stray file reads as "no".
    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(DataFormat.File))
            return;
        e.DragEffects = LocalPaths(e).Any(Importable) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(DataFormat.File))
            return;
        e.Handled = true;
        var paths = LocalPaths(e).ToList();
        var models = paths.Where(MeshImport.CanImport).ToList();
        // Everything that isn't a model goes to the media import, which names what it can't use.
        var rest = paths.Except(models).ToList();
        // An event handler can't be awaited, so a failure here would otherwise take the whole app down.
        try
        {
            if (models.Count > 0)
                await _models.Import(models);
            if (rest.Count > 0)
                await _media.Import(rest);
        }
        catch (Exception ex)
        {
            _status.Message = $"Could not import the dropped files: {ex.Message}";
        }
    }

    private static bool Importable(string path) => MeshImport.CanImport(path) || MediaImport.KindOf(path) is not null;

    private static IEnumerable<string> LocalPaths(DragEventArgs e) =>
        (e.DataTransfer.TryGetFiles() ?? []).Select(f => f.TryGetLocalPath()).OfType<string>();
}
