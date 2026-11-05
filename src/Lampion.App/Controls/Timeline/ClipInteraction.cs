using Avalonia;
using Avalonia.Input;
using Lampion.App.Editor;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// Base for dragging part of a clip. Remembers where the drag started; each move applies the change relative
/// to that, calls <see cref="EditorState.Touch"/>, and release re-sorts the track and commits an undo step.
/// </summary>
internal abstract class ClipInteraction : ITimelineInteraction
{
    private readonly double _startTime;
    private bool _moved;

    /// <summary>Starts dragging <paramref name="clip"/> on <paramref name="track"/> from pointer time <paramref name="startTime"/>.</summary>
    protected ClipInteraction(EditorState state, TimelineViewport viewport, TimelineSnapper snapper, Clip clip, Track track, double startTime)
    {
        State = state;
        Viewport = viewport;
        Snapper = snapper;
        Clip = clip;
        Track = track;
        _startTime = startTime;
        OriginalStart = clip.Start;
        OriginalDuration = clip.Duration;
    }

    /// <summary>The editor being changed.</summary>
    protected EditorState State { get; }

    /// <summary>The timeline's zoom and scroll.</summary>
    protected TimelineViewport Viewport { get; }

    /// <summary>Snapping for the edited edges.</summary>
    protected TimelineSnapper Snapper { get; }

    /// <summary>The clip being dragged.</summary>
    protected Clip Clip { get; }

    /// <summary>The track the clip is on; a move can hand it to another one.</summary>
    protected Track Track { get; set; }

    /// <summary>The clip's start when the drag began, in seconds.</summary>
    protected double OriginalStart { get; }

    /// <summary>The clip's duration when the drag began, in seconds.</summary>
    protected double OriginalDuration { get; }

    /// <inheritdoc />
    public void Move(Point p, KeyModifiers modifiers)
    {
        var tolerance = modifiers.HasFlag(KeyModifiers.Alt) ? 0 : Viewport.PixelsToSeconds(TimelineSnapper.DragTolerancePixels);
        Apply(p, Viewport.XToTime(p.X) - _startTime, tolerance);
        _moved = true;
        State.Touch();
    }

    /// <inheritdoc />
    public void Complete()
    {
        if (!_moved)
            return;
        Track.SortClips();
        State.Commit();
    }

    /// <summary>Changes the clip for a pointer at <paramref name="p"/>.</summary>
    /// <param name="p">Pointer position in control coordinates.</param>
    /// <param name="delta">Seconds the pointer has moved since the press.</param>
    /// <param name="tolerance">Snap tolerance in seconds; 0 while Alt is held.</param>
    protected abstract void Apply(Point p, double delta, double tolerance);

    /// <summary>Keeps a light clip's fades inside its (new) duration.</summary>
    protected static void ClampFades(LightClip clip)
    {
        clip.FadeIn = Math.Min(clip.FadeIn, clip.Duration);
        clip.FadeOut = Math.Min(clip.FadeOut, clip.Duration - clip.FadeIn);
    }
}
