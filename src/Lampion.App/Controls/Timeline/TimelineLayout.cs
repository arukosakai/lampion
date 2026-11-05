using System.Runtime.InteropServices;
using Lampion.App.Editor;
using Lampion.Core.Model;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// The timeline's rows: one per track plus one per animated lane under expanded light tracks, with their
/// heights and header text. Rebuilt only when <see cref="EditorState.Version"/> or the project instance
/// changes, reusing its buffers, so repainting and scrolling don't touch it. UI thread only.
/// </summary>
internal sealed class TimelineLayout
{
    /// <summary>Height of a keyframe row, in pixels.</summary>
    public const double LaneHeight = 38;

    private readonly EditorState _state;
    private readonly List<TimelineRow> _rows = [];
    private readonly List<LaneTarget> _lanes = [];
    private readonly TrackSummaryCache _summaries = new();
    private readonly TrackTargetCounter _targets = new();
    private Project? _builtFor;
    private int _builtVersion = -1;

    /// <summary>Creates a layout that follows <paramref name="state"/>'s project.</summary>
    public TimelineLayout(EditorState state) => _state = state;

    /// <summary>
    /// The rows, top to bottom, brought up to date first. The span points into a reused buffer: it is only
    /// valid until the project next changes, so copy a row out rather than holding the span across edits.
    /// </summary>
    public ReadOnlySpan<TimelineRow> Rows
    {
        get
        {
            Update();
            return CollectionsMarshal.AsSpan(_rows);
        }
    }

    /// <summary>Content-space bottom of the last row in pixels, or 0 when there are no tracks.</summary>
    public double ContentBottom
    {
        get
        {
            var rows = Rows;
            return rows.Length == 0 ? 0 : rows[^1].Bottom;
        }
    }

    /// <summary>Row height of a track (not its lanes), in pixels.</summary>
    public static double TrackHeight(Track track) =>
        track.Kind switch
        {
            TrackKind.Audio => 60,
            TrackKind.Video => 46,
            _ => 54,
        };

    /// <summary>Content-space bottom of the last row belonging to a light track (lanes included), or null when there is none.</summary>
    public double? LightTracksBottom()
    {
        var rows = Rows;
        for (var i = rows.Length - 1; i >= 0; i--)
            if (rows[i].Track.Kind == TrackKind.Light)
                return rows[i].Bottom;
        return null;
    }

    private void Update()
    {
        var project = _state.Project;
        if (ReferenceEquals(project, _builtFor) && _builtVersion == _state.Version)
            return;
        _builtFor = project;
        _builtVersion = _state.Version;
        _rows.Clear();
        _summaries.Prune(project.Tracks.Count);
        _targets.Begin(project);

        var top = 0.0;
        foreach (var track in project.Tracks)
        {
            var height = TrackHeight(track);
            var isLight = track.Kind == TrackKind.Light;
            var caption = track.Kind switch
            {
                TrackKind.Light => _summaries.Get(project, track),
                TrackKind.Audio => "Audio",
                _ => "Video",
            };
            _rows.Add(new TimelineRow(track, false, default, caption, isLight ? _targets.Count(project, track) : 0, top, height));
            top += height;
            if (isLight && track.Expanded)
                top = AddLaneRows(track, top);
        }
    }

    private double AddLaneRows(Track track, double top)
    {
        _lanes.Clear();
        foreach (var clip in track.Clips)
        {
            if (clip is not LightClip light)
                continue;
            foreach (var lane in light.Automation)
            {
                if (!lane.Animated)
                    continue;
                var target = LaneTarget.Of(lane);
                if (_lanes.Contains(target))
                    continue;
                _lanes.Add(target);
                _rows.Add(new TimelineRow(track, true, target, LaneLabels.Caption(lane, target), 0, top, LaneHeight));
                top += LaneHeight;
            }
        }
        return top;
    }
}
