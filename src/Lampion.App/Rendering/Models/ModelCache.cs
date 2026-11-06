using Silk.NET.OpenGL;

namespace Lampion.App.Rendering.Models;

/// <summary>
/// The imported models the stage draws, by file. A model asked for the first time is loaded on a worker thread and
/// uploaded on a later frame; until then, and for files that can't be read, <see cref="TryGet"/> says no and the
/// caller draws a stand-in, and failed files are tried again every couple of seconds. Models nobody has asked for
/// in a while are freed. Render thread only.
/// </summary>
internal sealed class ModelCache(GL gl) : IDisposable
{
    // Frames a model may go undrawn before it is freed: ten seconds at 60 fps, so undo/redo doesn't reload it.
    private const int IdleFrames = 600;

    // Frames before a model that failed to load is tried again: the file may still be on its way (Save As copies
    // the assets folder after the show already points at it) or the user may put it back.
    private const int RetryFrames = 120;

    private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _idle = [];
    private int _frame;

    /// <summary>The uploaded model for <paramref name="path"/>, starting a load when it's new; false while loading or when it failed.</summary>
    public bool TryGet(string path, out GpuModel model)
    {
        if (!_entries.TryGetValue(path, out var entry))
        {
            entry = new Entry(Task.Run(() => ModelLoader.Load(path)));
            _entries[path] = entry;
        }
        entry.LastUsed = _frame;
        if (entry.Model is null && entry.Loading is { IsCompleted: true } done)
        {
            entry.Loading = null;
            // A file that can't be read stays a stand-in box rather than failing the whole frame.
            if (done.IsCompletedSuccessfully)
                entry.Model = new GpuModel(gl, done.Result);
            else
            {
                _ = done.Exception;
                entry.FailedAt = _frame;
            }
        }
        model = entry.Model!;
        return model is not null;
    }

    /// <summary>Frees models that went undrawn for a while. Call once per frame, after drawing.</summary>
    public void EndFrame()
    {
        _frame++;
        foreach (var (path, entry) in _entries)
            if (entry.Loading is null && (_frame - entry.LastUsed > IdleFrames || (entry.FailedAt is { } failed && _frame - failed > RetryFrames)))
                _idle.Add(path);
        if (_idle.Count == 0)
            return;
        foreach (var path in _idle)
        {
            _entries[path].Model?.Dispose();
            _entries.Remove(path);
        }
        _idle.Clear();
    }

    /// <summary>Frees every uploaded model; loads still running are left to finish and be dropped.</summary>
    public void Dispose()
    {
        foreach (var entry in _entries.Values)
            entry.Model?.Dispose();
        _entries.Clear();
    }

    private sealed class Entry(Task<LoadedModel> loading)
    {
        public Task<LoadedModel>? Loading { get; set; } = loading;
        public GpuModel? Model { get; set; }
        public int LastUsed { get; set; }

        public int? FailedAt { get; set; }
    }
}
