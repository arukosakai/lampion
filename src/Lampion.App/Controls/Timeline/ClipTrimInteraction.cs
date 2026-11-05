using Avalonia;
using Lampion.App.Editor;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// Dragging a clip's left or right edge. Clips stay at least <see cref="MinDuration"/> long, media clips
/// can't be trimmed past either end of their source file, and light-clip fades shrink to fit.
/// </summary>
internal sealed class ClipTrimInteraction : ClipInteraction
{
    /// <summary>The shortest a trim can make a clip, in seconds.</summary>
    public const double MinDuration = 0.05;

    private readonly bool _start;
    private readonly double _originalOffset;

    /// <summary>Starts trimming <paramref name="clip"/>'s start (<paramref name="trimStart"/>) or end, from pointer time <paramref name="startTime"/>.</summary>
    public ClipTrimInteraction(EditorState state, TimelineViewport viewport, TimelineSnapper snapper, Clip clip, Track track, double startTime, bool trimStart)
        : base(state, viewport, snapper, clip, track, startTime)
    {
        _start = trimStart;
        _originalOffset = (clip as MediaClip)?.MediaOffset ?? 0;
    }

    /// <inheritdoc />
    protected override void Apply(Point p, double delta, double tolerance)
    {
        if (_start)
            TrimStart(delta, tolerance);
        else
            TrimEnd(delta, tolerance);
        if (Clip is LightClip light)
            ClampFades(light);
    }

    private void TrimStart(double delta, double tolerance)
    {
        var end = OriginalStart + OriginalDuration;
        var minStart = Clip is MediaClip ? OriginalStart - _originalOffset : 0;
        var start = Math.Clamp(Snapper.Snap(OriginalStart + delta, tolerance, Clip), Math.Max(0, minStart), end - MinDuration);
        Clip.Start = start;
        Clip.Duration = end - start;
        if (Clip is MediaClip media)
            media.MediaOffset = _originalOffset + (start - OriginalStart);
    }

    private void TrimEnd(double delta, double tolerance)
    {
        var end = Snapper.Snap(OriginalStart + OriginalDuration + delta, tolerance, Clip);
        var maxDuration = Clip is MediaClip { SourceDuration: { } source } media ? source - media.MediaOffset : double.MaxValue;
        Clip.Duration = Math.Clamp(end - Clip.Start, MinDuration, maxDuration);
    }
}
