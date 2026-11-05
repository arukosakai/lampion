using Lampion.Core.Model.Timeline;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// Clip captions with their decorations ("Rainbow  ◆2", "♪ song.wav"), rebuilt only when the clip's name
/// or keyed-lane count changes. Entries are keyed by clip instance. UI thread only.
/// </summary>
internal sealed class ClipLabelCache
{
    private const int Limit = 4096;

    private readonly Dictionary<Clip, (string Name, int Detail, string Label)> _labels = [];

    /// <summary>A light clip's caption: its name, plus "◆n" when <paramref name="keyedLanes"/> lanes are animated.</summary>
    public string Light(LightClip clip, int keyedLanes)
    {
        if (_labels.TryGetValue(clip, out var cached) && ReferenceEquals(cached.Name, clip.Name) && cached.Detail == keyedLanes)
            return cached.Label;
        var label = keyedLanes > 0 ? $"{clip.Name}  ◆{keyedLanes}" : clip.Name;
        Store(clip, keyedLanes, label);
        return label;
    }

    /// <summary>A media clip's caption: its name behind a note (audio) or play (video) glyph.</summary>
    public string Media(MediaClip clip, TrackKind kind)
    {
        var detail = (int)kind;
        if (_labels.TryGetValue(clip, out var cached) && ReferenceEquals(cached.Name, clip.Name) && cached.Detail == detail)
            return cached.Label;
        var label = (kind == TrackKind.Audio ? "♪ " : "▶ ") + clip.Name;
        Store(clip, detail, label);
        return label;
    }

    private void Store(Clip clip, int detail, string label)
    {
        if (_labels.Count >= Limit)
            _labels.Clear();
        _labels[clip] = (clip.Name, detail, label);
    }
}
