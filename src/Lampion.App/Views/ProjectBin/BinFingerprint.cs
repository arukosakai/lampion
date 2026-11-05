using Lampion.Core.Model;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Views.ProjectBin;

/// <summary>
/// Remembers every field the bin displays, so an edit elsewhere (a clip drag, a keyframe) can be told apart
/// from one the bin must show. Runs on every <c>EditorState.Changed</c>, so it is allocation-free once warm.
/// </summary>
internal sealed class BinFingerprint
{
    private readonly List<string?> _text = [];
    private readonly List<int> _numbers = [];
    private int _textCursor;
    private int _numberCursor;
    private bool _changed;
    private bool _primed;

    /// <summary>Records what the bin would show now and compares it with the previous call.</summary>
    /// <param name="project">The project being shown.</param>
    /// <param name="search">The bin's search text; null reads as empty.</param>
    /// <returns>True when anything differs from the previous call, and always on the first.</returns>
    public bool Update(Project project, string? search)
    {
        _textCursor = 0;
        _numberCursor = 0;
        _changed = !_primed;
        _primed = true;

        Text(search ?? "");
        Number(project.Groups.Count);
        foreach (var group in project.Groups)
        {
            Text(group.Id);
            Text(group.Name);
            Text(group.Color);
            Number(group.FixtureIds.Count);
        }
        Number(project.Fixtures.Count);
        foreach (var fixture in project.Fixtures)
        {
            Text(fixture.Id);
            Text(fixture.Name);
            Number(fixture.Universe);
            Number(fixture.Address);
            Text(fixture.ProfileId);
        }
        Number(project.Profiles.Count);
        foreach (var profile in project.Profiles)
        {
            Text(profile.Id);
            Text(profile.Model);
            Number(profile.Channels.Count);
        }
        foreach (var track in project.Tracks)
        {
            if (track.Kind == TrackKind.Light)
                continue;
            foreach (var clip in track.Clips)
                if (clip is MediaClip media)
                {
                    Text(media.Id);
                    Text(media.Name);
                }
        }

        Trim(_text, _textCursor);
        Trim(_numbers, _numberCursor);
        return _changed;
    }

    private void Text(string? value)
    {
        if (_textCursor < _text.Count)
        {
            if (!string.Equals(_text[_textCursor], value, StringComparison.Ordinal))
            {
                _text[_textCursor] = value;
                _changed = true;
            }
        }
        else
        {
            _text.Add(value);
            _changed = true;
        }
        _textCursor++;
    }

    private void Number(int value)
    {
        if (_numberCursor < _numbers.Count)
        {
            if (_numbers[_numberCursor] != value)
            {
                _numbers[_numberCursor] = value;
                _changed = true;
            }
        }
        else
        {
            _numbers.Add(value);
            _changed = true;
        }
        _numberCursor++;
    }

    private void Trim<T>(List<T> list, int length)
    {
        if (list.Count <= length)
            return;
        list.RemoveRange(length, list.Count - length);
        _changed = true;
    }
}
