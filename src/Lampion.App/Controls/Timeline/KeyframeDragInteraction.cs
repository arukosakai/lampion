using Avalonia;
using Avalonia.Input;
using Lampion.App.Editor;
using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// Dragging a keyframe: time follows the pointer within the clip (snapping to beats unless Alt is held),
/// value follows its height. Hue and effect-parameter keys keep 3 decimals and may leave 0..1 (parameters stop at
/// their minimum); other values are clamped to it.
/// </summary>
internal sealed class KeyframeDragInteraction(
    EditorState state,
    TimelineViewport viewport,
    TimelineSnapper snapper,
    LightClip clip,
    AutomationLane lane,
    Keyframe key,
    Rect laneBounds) : ITimelineInteraction
{
    private bool _moved;

    /// <inheritdoc />
    public void Move(Point p, KeyModifiers modifiers)
    {
        var scale = LaneValueScale.For(lane, laneBounds);
        var time = Math.Clamp(viewport.XToTime(p.X) - clip.Start, 0, clip.Duration);
        var beats = !modifiers.HasFlag(KeyModifiers.Alt);
        key.Time = snapper.SnapDrag(clip.Start + time, beats: beats) - clip.Start;
        var value = scale.YToValue(p.Y);
        key.Value = lane.Target switch
        {
            AutomationTarget.HueShift => Math.Round(value, 3),
            AutomationTarget.EffectParameter => Math.Round(Math.Max(clip.Effect.FindParameter(lane.Parameter)?.Minimum ?? 0, value), 3),
            _ => Math.Clamp(value, 0, 1),
        };
        lane.SortKeys();
        _moved = true;
        state.Touch();
    }

    /// <inheritdoc />
    public void Complete()
    {
        if (_moved)
            state.Commit();
    }
}
