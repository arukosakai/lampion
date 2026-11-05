using Avalonia.Threading;
using Lampion.App.Editor;
using Lampion.Core.Media;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Views.Shell;

/// <summary>
/// Reads waveform peaks for the project's media clips in the background, into
/// <see cref="EditorState.Waveforms"/>, whenever a project is loaded. Each file is read once per session.
/// </summary>
internal sealed class WaveformLoader
{
    private readonly EditorState _state;
    private readonly Action _loaded;

    /// <summary>Loads for <paramref name="state"/>, now and after every project replacement.</summary>
    /// <param name="state">The editor state.</param>
    /// <param name="loaded">Runs on the UI thread after each file's peaks arrived (e.g. to redraw the timeline).</param>
    public WaveformLoader(EditorState state, Action loaded)
    {
        _state = state;
        _loaded = loaded;
        state.ProjectReplaced += LoadMissing;
        LoadMissing();
    }

    /// <summary>Starts reading every media file that has no entry yet and exists on disk.</summary>
    public void LoadMissing()
    {
        foreach (var track in _state.Project.Tracks)
            foreach (var item in track.Clips)
                if (item is MediaClip clip)
                    Load(clip.Path);
    }

    private void Load(string path)
    {
        if (_state.Waveforms.ContainsKey(path) || !File.Exists(path))
            return;
        // A null entry marks the read as in progress, so the same file is not read twice.
        _state.Waveforms[path] = null;
        Task.Run(() => WaveformReader.TryRead(path)).ContinueWith(t =>
            Dispatcher.UIThread.Post(() =>
            {
                _state.Waveforms[path] = t.Result;
                _loaded();
            })
        );
    }
}
