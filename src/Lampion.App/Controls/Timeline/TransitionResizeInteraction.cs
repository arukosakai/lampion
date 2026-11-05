using Avalonia;
using Lampion.App.Editor;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Controls.Timeline;

/// <summary>Dragging the transition box on a light clip's left edge to change how long the transition lasts.</summary>
internal sealed class TransitionResizeInteraction(
    EditorState state,
    TimelineViewport viewport,
    TimelineSnapper snapper,
    LightClip clip,
    Track track,
    double startTime) : ClipInteraction(state, viewport, snapper, clip, track, startTime)
{
    /// <summary>The shortest transition a drag can make, in seconds.</summary>
    public const double MinDuration = 0.05;

    /// <summary>The longest transition a drag can make, in seconds.</summary>
    public const double MaxDuration = 60;

    /// <inheritdoc />
    protected override void Apply(Point p, double delta, double tolerance)
    {
        if (Clip is not LightClip { TransitionIn: { } transition })
            return;
        // The box is centred on the cut, so its width follows the pointer's distance from it on either side.
        transition.Duration = Math.Clamp(Math.Abs(Viewport.XToTime(p.X) - Clip.Start) * 2, MinDuration, Math.Min(MaxDuration, Clip.Duration * 2));
    }
}
