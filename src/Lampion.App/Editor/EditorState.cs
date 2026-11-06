using Lampion.Core.Engine;
using Lampion.Core.Media;
using Lampion.Core.Model;
using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Stage;
using Lampion.Core.Model.Timeline;
using Lampion.Core.Persistence;

namespace Lampion.App.Editor;

/// <summary>
/// The single source of truth the panels share: the project, what is selected, the playhead and undo.
/// Panels mutate the model directly, then call <see cref="Touch"/> while dragging or <see cref="Commit"/>
/// when an edit is finished, which records an undo step. UI thread only.
/// </summary>
/// <remarks>
/// A facade over <see cref="ProjectDocument"/>, <see cref="UndoHistory"/>, <see cref="SelectionModel"/>,
/// <see cref="Transport"/> and <see cref="FixtureTestHold"/>; the collaborators hold state and this class
/// raises the events.
/// </remarks>
public sealed class EditorState
{
    private readonly ProjectDocument _document;
    private readonly UndoHistory _history;
    private readonly SelectionModel _selection = new();
    private readonly Transport _transport = new();
    private readonly FixtureTestHold _testHold = new();
    private readonly DmxFrame _frame = new();
    private double _frameTime = -1;
    private int _frameVersion = -1;

    /// <summary>Starts editing <paramref name="project"/>, saved at <paramref name="filePath"/> (null while unsaved).</summary>
    public EditorState(Project project, string? filePath = null)
    {
        _document = new ProjectDocument(project, filePath);
        _history = new UndoHistory(Snapshot(project));
    }

    /// <summary>The project being edited. Undo, redo and open replace the instance; see <see cref="ProjectReplaced"/>.</summary>
    public Project Project => _document.Project;

    /// <summary>The renderer for <see cref="Project"/>, recreated with it. Prefer <see cref="CurrentFrame"/>, which is cached.</summary>
    public ShowRenderer Renderer => _document.Renderer;

    /// <summary>The file the project was opened from or last saved to; null while unsaved.</summary>
    public string? FilePath
    {
        get => _document.FilePath;
        set => _document.FilePath = value;
    }

    /// <summary>True when the project has edits that are not on disk.</summary>
    public bool Dirty => _document.Dirty;

    /// <summary>Bumped on every model change; caches compare against it.</summary>
    public int Version => _document.Version;

    /// <summary>The playhead in seconds; never negative.</summary>
    public double Playhead => _transport.Playhead;

    /// <summary>True while playback is running.</summary>
    public bool IsPlaying => _transport.IsPlaying;

    /// <summary>True to wrap to the start at the end of the content instead of stopping.</summary>
    public bool Loop
    {
        get => _transport.Loop;
        set => _transport.Loop = value;
    }

    /// <summary>What the selection holds.</summary>
    public SelectionKind SelectionKind => _selection.Kind;

    /// <summary>The selected track, or the track of the last selected clip; null when none.</summary>
    public string? SelectedTrackId => _selection.TrackId;

    /// <summary>Selected clip ids. Call <see cref="NotifySelectionChanged"/> after editing it directly.</summary>
    public HashSet<string> SelectedClipIds => _selection.ClipIds;

    /// <summary>Selected fixture ids. Call <see cref="NotifySelectionChanged"/> after editing it directly.</summary>
    public HashSet<string> SelectedFixtureIds => _selection.FixtureIds;

    /// <summary>Selected stage set piece ids. Call <see cref="NotifySelectionChanged"/> after editing it directly.</summary>
    public HashSet<string> SelectedObjectIds => _selection.ObjectIds;

    /// <summary>One of the selected set pieces' ids (the set is unordered), or null.</summary>
    public string? SelectedObjectId => _selection.AnyObjectId;

    /// <summary>The keyframe the timeline last picked, so Delete removes it rather than the clip.</summary>
    public (string LaneId, Keyframe Key)? SelectedKey
    {
        get => _selection.Key;
        set => _selection.Key = value;
    }

    /// <summary>Raised after every model change (<see cref="Touch"/>, <see cref="Commit"/>, undo, open, save).</summary>
    public event Action? Changed;

    /// <summary>Raised after the selection changes.</summary>
    public event Action? SelectionChanged;

    /// <summary>Raised when the playhead moves or playback starts or stops; every tick while playing.</summary>
    public event Action? PlayheadChanged;

    /// <summary>Raised when undo/open replaces the whole project instance.</summary>
    public event Action? ProjectReplaced;

    /// <summary>Waveform peaks by media path; a null value means loading or unreadable.</summary>
    public Dictionary<string, AudioPeaks?> Waveforms { get; } = [];

    // ---- Model changes ---------------------------------------------------------------------------------

    /// <summary>Records a live edit (e.g. mid-drag) without an undo step: bumps <see cref="Version"/>, marks dirty, raises <see cref="Changed"/>.</summary>
    public void Touch()
    {
        _document.Touch();
        Changed?.Invoke();
    }

    /// <summary>Finishes an edit: records an undo step if the project differs from the last one, then <see cref="Touch"/>es.</summary>
    public void Commit()
    {
        _history.Record(Snapshot(Project));
        Touch();
    }

    /// <summary>True when there is a step to undo.</summary>
    public bool CanUndo => _history.CanUndo;

    /// <summary>True when there is a step to redo.</summary>
    public bool CanRedo => _history.CanRedo;

    /// <summary>Restores the project as it was before the last committed edit, keeping what still exists of the selection.</summary>
    public void Undo()
    {
        if (_history.TryUndo(out var snapshot))
            Restore(snapshot);
    }

    /// <summary>Re-applies the last undone edit.</summary>
    public void Redo()
    {
        if (_history.TryRedo(out var snapshot))
            Restore(snapshot);
    }

    /// <summary>Replaces the project (open, new), clearing undo and selection and rewinding the playhead.</summary>
    /// <param name="project">The project to edit.</param>
    /// <param name="path">Where it is saved; null while unsaved.</param>
    public void Load(Project project, string? path)
    {
        _history.Reset(Snapshot(project));
        _document.FilePath = path;
        ReplaceProject(project, keepSelection: false);
        _document.Dirty = false;
        Seek(0);
    }

    /// <summary>Notes that the project was written to <paramref name="path"/>: no longer dirty. Raises <see cref="Changed"/>.</summary>
    public void MarkSaved(string path)
    {
        _document.FilePath = path;
        _document.Dirty = false;
        Changed?.Invoke();
    }

    private void Restore(string snapshot)
    {
        ReplaceProject(ProjectSerializer.Deserialize(snapshot), keepSelection: true);
        _document.Dirty = true;
    }

    private void ReplaceProject(Project project, bool keepSelection)
    {
        _document.Replace(project);
        if (keepSelection)
            _selection.Prune(_document);
        else
            _selection.Clear();
        ProjectReplaced?.Invoke();
        Changed?.Invoke();
        SelectionChanged?.Invoke();
    }

    private static string Snapshot(Project project) => ProjectSerializer.Serialize(project, indented: false);

    // ---- Lookups ---------------------------------------------------------------------------------------

    /// <summary>The track with this id, or null (also for a null id).</summary>
    public Track? TrackById(string? id) => _document.TrackById(id);

    /// <summary>The clip with this id and the track holding it, or null.</summary>
    public (Track Track, Clip Clip)? FindClip(string id) => _document.FindClip(id);

    /// <summary>One of the selected clips (the set is unordered), or null.</summary>
    public Clip? PrimaryClip => _selection.AnyClipId is { } id ? FindClip(id)?.Clip : null;

    /// <summary>One of the selected fixtures (the set is unordered), or null.</summary>
    public Fixture? PrimaryFixture => _selection.AnyFixtureId is { } id ? Project.FixtureById(id) : null;

    /// <summary>One of the selected stage set pieces (the set is unordered), or null.</summary>
    public StageObject? SelectedObject => _document.StageObjectById(SelectedObjectId);

    /// <summary>The selected track (see <see cref="SelectedTrackId"/>), or null.</summary>
    public Track? SelectedTrack => TrackById(SelectedTrackId);

    // ---- Selection -------------------------------------------------------------------------------------

    /// <summary>Clears the selection <em>without</em> raising <see cref="SelectionChanged"/>; <see cref="SelectedTrackId"/> is kept.</summary>
    public void ClearSelection() => _selection.Clear();

    /// <summary>Selects a clip (and its track), added to the selected clips when <paramref name="additive"/>.</summary>
    public void SelectClip(string clipId, bool additive = false)
    {
        _selection.SelectClip(clipId, FindClip(clipId)?.Track.Id, additive);
        SelectionChanged?.Invoke();
    }

    /// <summary>Selects one track and nothing else.</summary>
    public void SelectTrack(string trackId)
    {
        _selection.SelectTrack(trackId);
        SelectionChanged?.Invoke();
    }

    /// <summary>Selects fixtures; when <paramref name="additive"/>, ids already selected are toggled off instead.</summary>
    public void SelectFixtures(IEnumerable<string> ids, bool additive = false)
    {
        _selection.SelectFixtures(ids, additive);
        SelectionChanged?.Invoke();
    }

    /// <summary>Selects one stage set piece, or nothing for null.</summary>
    public void SelectObject(string? id)
    {
        _selection.SelectObject(id);
        SelectionChanged?.Invoke();
    }

    /// <summary>Selects set pieces; when <paramref name="additive"/>, ids already selected are toggled off instead.</summary>
    public void SelectObjects(IEnumerable<string> ids, bool additive = false)
    {
        _selection.SelectObjects(ids, additive);
        SelectionChanged?.Invoke();
    }

    /// <summary>Selects nothing and raises <see cref="SelectionChanged"/>.</summary>
    public void Deselect()
    {
        _selection.Clear();
        SelectionChanged?.Invoke();
    }

    /// <summary>Raises <see cref="SelectionChanged"/> after the selection sets were edited directly.</summary>
    public void NotifySelectionChanged() => SelectionChanged?.Invoke();

    // ---- Transport -------------------------------------------------------------------------------------

    /// <summary>Moves the playhead to <paramref name="time"/> seconds (clamped at 0); playback carries on from there.</summary>
    public void Seek(double time)
    {
        _transport.Seek(time);
        PlayheadChanged?.Invoke();
    }

    /// <summary>Starts playback, from the start if the playhead is at the end.</summary>
    public void Play()
    {
        if (_transport.Play(Project.ContentEnd))
            PlayheadChanged?.Invoke();
    }

    /// <summary>Stops playback where it is.</summary>
    public void Pause()
    {
        if (_transport.Pause())
            PlayheadChanged?.Invoke();
    }

    /// <summary>Plays when paused, pauses when playing.</summary>
    public void TogglePlay()
    {
        if (IsPlaying)
            Pause();
        else
            Play();
    }

    /// <summary>Advances the playhead from the wall clock; called once per UI frame. Allocation-free.</summary>
    public void Tick()
    {
        if (_transport.Advance(Project.ContentEnd))
            PlayheadChanged?.Invoke();
    }

    /// <summary>
    /// Values the profile editor's tester holds on some patched fixtures, over whatever the show says.
    /// Replace the whole record to change it; null hands the fixtures back to the show.
    /// </summary>
    public FixtureTest? Test
    {
        get => _testHold.Test;
        set => _testHold.Test = value;
    }

    /// <summary>
    /// DMX output at the playhead (with any <see cref="Test"/> on top), rendered at most once per playhead
    /// move or edit and allocation-free in steady state. The frame is owned by the editor and overwritten
    /// on the next change: read it right away, or <see cref="DmxFrame.CopyTo"/> it to keep it.
    /// </summary>
    public DmxFrame CurrentFrame
    {
        get
        {
            var playhead = Playhead;
            var version = Version;
            var rendered = _frameTime != playhead || _frameVersion != version;
            if (rendered)
            {
                Renderer.Render(playhead, _frame);
                _frameTime = playhead;
                _frameVersion = version;
            }
            return _testHold.Apply(_frame, rendered, Project, version);
        }
    }

    // ---- Timeline helpers ------------------------------------------------------------------------------

    /// <summary>Length of one beat at the project tempo, in seconds.</summary>
    public double SecondsPerBeat => 60 / Math.Max(1, Project.Bpm);
}
