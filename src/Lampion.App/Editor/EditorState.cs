using System.Diagnostics;
using Lampion.Core.Engine;
using Lampion.Core.Media;
using Lampion.Core.Model;
using Lampion.Core.Persistence;

namespace Lampion.App.Editor;

public enum SelectionKind
{
    None,
    Clip,
    Track,
    Fixture,
    StageObject,
}

/// <summary>Channel values held on patched fixtures while testing a profile.</summary>
public sealed record FixtureTest(IReadOnlySet<string> FixtureIds, byte[] Values);

/// <summary>
/// The single source of truth the panels share: the project, what is selected, the playhead and undo.
/// Panels mutate the model directly, then call <see cref="Touch"/> while dragging or <see cref="Commit"/>
/// when an edit is finished, which records an undo step.
/// </summary>
public sealed class EditorState
{
    private const int UndoLimit = 200;

    private readonly List<string> _undo = [];
    private readonly List<string> _redo = [];
    private readonly Stopwatch _clock = new();
    private string _snapshot;
    private double _playStart;
    private DmxFrame? _frame;
    private (double Time, int Version) _frameKey = (-1, -1);

    public EditorState(Project project, string? filePath = null)
    {
        Project = project;
        FilePath = filePath;
        Renderer = new ShowRenderer(project);
        _snapshot = ProjectSerializer.Serialize(project, indented: false);
    }

    public Project Project { get; private set; }
    public ShowRenderer Renderer { get; private set; }
    public string? FilePath { get; set; }
    public bool Dirty { get; private set; }

    /// <summary>Bumped on every model change; caches compare against it.</summary>
    public int Version { get; private set; }

    public double Playhead { get; private set; }
    public bool IsPlaying => _clock.IsRunning;
    public bool Loop { get; set; }

    public SelectionKind SelectionKind { get; private set; }
    public string? SelectedTrackId { get; private set; }
    public HashSet<string> SelectedClipIds { get; } = [];
    public HashSet<string> SelectedFixtureIds { get; } = [];
    public string? SelectedObjectId { get; private set; }

    /// <summary>The keyframe the timeline last picked, so Delete removes it rather than the clip.</summary>
    public (string LaneId, Keyframe Key)? SelectedKey { get; set; }

    public event Action? Changed;
    public event Action? SelectionChanged;
    public event Action? PlayheadChanged;

    /// <summary>Raised when undo/open replaces the whole project instance.</summary>
    public event Action? ProjectReplaced;

    public Dictionary<string, AudioPeaks?> Waveforms { get; } = [];

    // ---- Model changes ---------------------------------------------------------------------------------

    public void Touch()
    {
        Version++;
        Dirty = true;
        Changed?.Invoke();
    }

    public void Commit()
    {
        var current = ProjectSerializer.Serialize(Project, indented: false);
        if (current == _snapshot)
        {
            Touch();
            return;
        }
        _undo.Add(_snapshot);
        if (_undo.Count > UndoLimit)
            _undo.RemoveAt(0);
        _redo.Clear();
        _snapshot = current;
        Touch();
    }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Undo() => Step(_undo, _redo);

    public void Redo() => Step(_redo, _undo);

    private void Step(List<string> from, List<string> to)
    {
        if (from.Count == 0)
            return;
        to.Add(_snapshot);
        _snapshot = from[^1];
        from.RemoveAt(from.Count - 1);
        ReplaceProject(ProjectSerializer.Deserialize(_snapshot), keepSelection: true);
        Dirty = true;
    }

    public void Load(Project project, string? path)
    {
        _undo.Clear();
        _redo.Clear();
        _snapshot = ProjectSerializer.Serialize(project, indented: false);
        FilePath = path;
        ReplaceProject(project, keepSelection: false);
        Dirty = false;
        Seek(0);
    }

    public void MarkSaved(string path)
    {
        FilePath = path;
        Dirty = false;
        Changed?.Invoke();
    }

    private void ReplaceProject(Project project, bool keepSelection)
    {
        Project = project;
        Renderer = new ShowRenderer(project);
        if (!keepSelection)
            ClearSelection();
        else
            PruneSelection();
        Version++;
        ProjectReplaced?.Invoke();
        Changed?.Invoke();
        SelectionChanged?.Invoke();
    }

    // ---- Lookups ---------------------------------------------------------------------------------------

    public Track? TrackById(string? id) => id is null ? null : Project.Tracks.Find(t => t.Id == id);

    public (Track Track, Clip Clip)? FindClip(string id)
    {
        foreach (var track in Project.Tracks)
            if (track.Clips.Find(c => c.Id == id) is { } clip)
                return (track, clip);
        return null;
    }

    public Clip? PrimaryClip => SelectedClipIds.Count == 0 ? null : FindClip(SelectedClipIds.First())?.Clip;

    public Fixture? PrimaryFixture => SelectedFixtureIds.Count == 0 ? null : Project.FixtureById(SelectedFixtureIds.First());

    public StageObject? SelectedObject => SelectedObjectId is null ? null : Project.Stage.Objects.Find(o => o.Id == SelectedObjectId);

    public Track? SelectedTrack => TrackById(SelectedTrackId);

    // ---- Selection -------------------------------------------------------------------------------------

    public void ClearSelection()
    {
        SelectionKind = SelectionKind.None;
        SelectedClipIds.Clear();
        SelectedFixtureIds.Clear();
        SelectedObjectId = null;
        SelectedKey = null;
    }

    public void SelectClip(string clipId, bool additive = false)
    {
        if (!additive)
            ClearSelection();
        SelectedClipIds.Add(clipId);
        SelectionKind = SelectionKind.Clip;
        if (FindClip(clipId) is { } found)
            SelectedTrackId = found.Track.Id;
        SelectionChanged?.Invoke();
    }

    public void SelectTrack(string trackId)
    {
        ClearSelection();
        SelectedTrackId = trackId;
        SelectionKind = SelectionKind.Track;
        SelectionChanged?.Invoke();
    }

    public void SelectFixtures(IEnumerable<string> ids, bool additive = false)
    {
        if (!additive)
            ClearSelection();
        foreach (var id in ids)
            if (!SelectedFixtureIds.Add(id) && additive)
                SelectedFixtureIds.Remove(id);
        SelectionKind = SelectedFixtureIds.Count > 0 ? SelectionKind.Fixture : SelectionKind.None;
        SelectionChanged?.Invoke();
    }

    public void SelectObject(string? id)
    {
        ClearSelection();
        SelectedObjectId = id;
        SelectionKind = id is null ? SelectionKind.None : SelectionKind.StageObject;
        SelectionChanged?.Invoke();
    }

    public void Deselect()
    {
        ClearSelection();
        SelectionChanged?.Invoke();
    }

    public void NotifySelectionChanged() => SelectionChanged?.Invoke();

    private void PruneSelection()
    {
        SelectedClipIds.RemoveWhere(id => FindClip(id) is null);
        SelectedFixtureIds.RemoveWhere(id => Project.FixtureById(id) is null);
        if (SelectedObject is null)
            SelectedObjectId = null;
        if (TrackById(SelectedTrackId) is null)
            SelectedTrackId = null;
        SelectedKey = null;
        SelectionKind = SelectionKind switch
        {
            SelectionKind.Clip when SelectedClipIds.Count == 0 => SelectionKind.None,
            SelectionKind.Fixture when SelectedFixtureIds.Count == 0 => SelectionKind.None,
            SelectionKind.StageObject when SelectedObjectId is null => SelectionKind.None,
            SelectionKind.Track when SelectedTrackId is null => SelectionKind.None,
            var k => k,
        };
    }

    // ---- Transport -------------------------------------------------------------------------------------

    public void Seek(double time)
    {
        Playhead = Math.Max(0, time);
        if (IsPlaying)
        {
            _playStart = Playhead;
            _clock.Restart();
        }
        PlayheadChanged?.Invoke();
    }

    public void Play()
    {
        if (IsPlaying)
            return;
        if (Playhead >= Project.ContentEnd - 0.01)
            Playhead = 0;
        _playStart = Playhead;
        _clock.Restart();
        PlayheadChanged?.Invoke();
    }

    public void Pause()
    {
        if (!IsPlaying)
            return;
        _clock.Stop();
        PlayheadChanged?.Invoke();
    }

    public void TogglePlay()
    {
        if (IsPlaying)
            Pause();
        else
            Play();
    }

    /// <summary>Advances the playhead from the wall clock; called once per UI frame.</summary>
    public void Tick()
    {
        if (!IsPlaying)
            return;
        var t = _playStart + _clock.Elapsed.TotalSeconds;
        var end = Project.ContentEnd;
        if (t >= end)
        {
            if (Loop)
            {
                _playStart = 0;
                _clock.Restart();
                t = 0;
            }
            else
            {
                _clock.Stop();
                t = end;
            }
        }
        Playhead = t;
        PlayheadChanged?.Invoke();
    }

    /// <summary>
    /// Values the profile editor's tester holds on some patched fixtures, over whatever the show says.
    /// Replace the whole record to change it; null hands the fixtures back to the show.
    /// </summary>
    public FixtureTest? Test { get; set; }

    /// <summary>DMX output at the playhead (with any <see cref="Test"/> on top), rendered at most once per change.</summary>
    public DmxFrame CurrentFrame
    {
        get
        {
            if (_frame is null || _frameKey != (Playhead, Version))
            {
                _frame = Renderer.Render(Playhead);
                _frameKey = (Playhead, Version);
                _testedFrame = null;
            }
            if (Test is not { } test)
                return _frame;
            if (_testedFrame is null || _testedFor != test)
            {
                var fixtures = Project.Fixtures.Where(f => test.FixtureIds.Contains(f.Id));
                _testedFrame = _frame.With(fixtures, test.Values);
                _testedFor = test;
            }
            return _testedFrame;
        }
    }

    private DmxFrame? _testedFrame;
    private FixtureTest? _testedFor;

    // ---- Timeline helpers ------------------------------------------------------------------------------

    public double SecondsPerBeat => 60 / Math.Max(1, Project.Bpm);

    /// <summary>Snap candidates near <paramref name="time"/>: beats, markers, clip edges and the playhead.</summary>
    public double Snap(double time, double tolerance, IReadOnlySet<string>? ignoreClips = null, bool beats = true)
    {
        var best = time;
        var bestDistance = tolerance;
        void Consider(double candidate)
        {
            var d = Math.Abs(candidate - time);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = candidate;
            }
        }
        Consider(0);
        Consider(Playhead);
        foreach (var marker in Project.Markers)
            Consider(marker.Time);
        foreach (var clip in Project.AllClips)
        {
            if (ignoreClips?.Contains(clip.Id) == true)
                continue;
            Consider(clip.Start);
            Consider(clip.End);
        }
        if (beats)
        {
            var beat = SecondsPerBeat;
            Consider(Project.BeatOffset + Math.Round((time - Project.BeatOffset) / beat) * beat);
        }
        return best;
    }
}
