using Lampion.Core.Persistence;
using LibVLCSharp.Shared;

namespace Lampion.App.Live;

/// <summary>The live console's pad sounds, one <see cref="PadSound"/> per pad id. UI thread only.</summary>
/// <param name="vlc">LibVLC to play through; null when live audio is unavailable, and then nothing plays.</param>
internal sealed class PadSounds(LibVLC? vlc) : IDisposable
{
    private readonly Dictionary<string, PadSound> _sounds = [];
    private readonly List<string> _gone = [];

    /// <summary>
    /// Makes sure pad <paramref name="padId"/> has <paramref name="soundPath"/> (as stored in the project, resolved against
    /// <paramref name="projectPath"/>) open and ready. Returns false when the file is missing or live audio is unavailable.
    /// </summary>
    public bool Arm(string padId, string soundPath, string? projectPath)
    {
        if (_sounds.TryGetValue(padId, out var sound) && sound.Source == soundPath)
            return true;
        sound?.Dispose();
        _sounds.Remove(padId);
        var path = ShowPaths.Resolve(projectPath, soundPath);
        if (vlc is null || !File.Exists(path))
            return false;
        _sounds[padId] = new PadSound(vlc, soundPath, path);
        return true;
    }

    /// <summary>Plays pad <paramref name="padId"/>'s sound from the start at <paramref name="volume"/> (0..2); nothing if it isn't armed.</summary>
    public void Fire(string padId, double volume) => _sounds.GetValueOrDefault(padId)?.Fire(volume);

    /// <summary>Cuts pad <paramref name="padId"/>'s sound off.</summary>
    public void Stop(string padId) => _sounds.GetValueOrDefault(padId)?.Stop();

    /// <summary>Closes the sounds of pads not in <paramref name="keep"/> (removed, or their sound cleared) and plays that have finished.</summary>
    public void Tidy(IReadOnlySet<string> keep)
    {
        foreach (var (id, sound) in _sounds)
            if (keep.Contains(id))
                sound.Tidy();
            else
                _gone.Add(id);
        foreach (var id in _gone)
            if (_sounds.Remove(id, out var sound))
                sound.Dispose();
        _gone.Clear();
    }

    /// <summary>Stops every pad sound and closes its players; the next <see cref="Arm"/> opens them again.</summary>
    public void StopAll()
    {
        foreach (var sound in _sounds.Values)
            sound.Dispose();
        _sounds.Clear();
    }

    /// <summary>Same as <see cref="StopAll"/>.</summary>
    public void Dispose() => StopAll();
}
