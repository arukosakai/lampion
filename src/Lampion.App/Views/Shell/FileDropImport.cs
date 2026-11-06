using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Lampion.App.Views.Shell;

/// <summary>Imports audio and video files dragged onto the window from the system file manager.</summary>
internal sealed class FileDropImport
{
    private readonly MediaImport _import;
    private readonly StatusBar _status;

    private FileDropImport(MediaImport import, StatusBar status) => (_import, _status) = (import, status);

    /// <summary>Lets <paramref name="window"/> take file drops and hands them to <paramref name="import"/>; failures go to <paramref name="status"/>.</summary>
    public static void Attach(Window window, MediaImport import, StatusBar status)
    {
        var drop = new FileDropImport(import, status);
        DragDrop.SetAllowDrop(window, true);
        window.AddHandler(DragDrop.DragOverEvent, drop.OnDragOver, RoutingStrategies.Bubble, handledEventsToo: true);
        window.AddHandler(DragDrop.DropEvent, drop.OnDrop, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    // Only show the copy cursor when something in the drag is importable, so a stray file reads as "no".
    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(DataFormat.File))
            return;
        e.DragEffects = LocalPaths(e).Any(p => MediaImport.KindOf(p) is not null) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(DataFormat.File))
            return;
        e.Handled = true;
        var paths = LocalPaths(e).ToList();
        if (paths.Count == 0)
            return;
        // An event handler can't be awaited, so a failure here would otherwise take the whole app down.
        try
        {
            await _import.Import(paths);
        }
        catch (Exception ex)
        {
            _status.Message = $"Could not import the dropped files: {ex.Message}";
        }
    }

    private static IEnumerable<string> LocalPaths(DragEventArgs e) =>
        (e.DataTransfer.TryGetFiles() ?? []).Select(f => f.TryGetLocalPath()).OfType<string>();
}
