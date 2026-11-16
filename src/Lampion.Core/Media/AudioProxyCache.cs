using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Lampion.Core.Media;

/// <summary>
/// Makes WAV copies of MP3s so playback can start mid-song in the right place. Media players seek a (VBR) MP3 by its
/// average bitrate and can land over a second off while still reporting the requested time; a WAV seeks exactly.
/// Copies live in <c>folder</c>, keyed by the source's path, size and modification time. Thread-safe.
/// </summary>
/// <param name="folder">Where the copies are kept; created when the first one is written.</param>
public sealed class AudioProxyCache(string folder)
{
    private readonly ConcurrentDictionary<string, string> _ready = new();
    private readonly ConcurrentDictionary<string, Task> _pending = new();

    /// <summary>Raised on a worker thread with the source path and a message for the status bar when a copy can't be made.</summary>
    public event Action<string, string>? Failed;

    /// <summary>
    /// The file a media player should open for <paramref name="path"/>: its WAV copy once ready, otherwise the file
    /// itself while the copy is made in the background. Allocation-free once the path has been seen.
    /// </summary>
    public string Resolve(string path)
    {
        if (_ready.TryGetValue(path, out var proxy))
            return proxy;
        Prepare(path);
        return path;
    }

    /// <summary>Starts making the copy for <paramref name="path"/> unless it's started already or the file isn't an MP3; completes when it's ready or has failed.</summary>
    public Task Prepare(string path)
    {
        if (_pending.TryGetValue(path, out var task))
            return task;
        if (!path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;
        return _pending.GetOrAdd(path, p => Task.Run(() => Conform(p)));
    }

    private void Conform(string path)
    {
        try
        {
            // A missing file stays unprepared and silent: the timeline already shows it as offline.
            var source = new FileInfo(path);
            if (!source.Exists)
                return;
            var target = Path.Combine(folder, Key(source) + ".wav");
            if (!File.Exists(target))
            {
                Directory.CreateDirectory(folder);
                Mp3Conformer.ToWav(path, target);
            }
            _ready[path] = target;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or IndexOutOfRangeException)
        {
            Failed?.Invoke(path, $"Couldn't prepare {Path.GetFileName(path)} for playback from mid-song ({ex.Message}). It plays fine from its start but may be out of sync when started later.");
        }
    }

    private static string Key(FileInfo source)
    {
        var identity = $"{source.FullName}|{source.Length}|{source.LastWriteTimeUtc.Ticks}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)), 0, 8);
        return Path.GetFileNameWithoutExtension(source.Name) + "-" + hash;
    }
}
