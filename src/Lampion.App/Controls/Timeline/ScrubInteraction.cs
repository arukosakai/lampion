using Avalonia;
using Avalonia.Input;
using Lampion.App.Editor;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// Dragging in the ruler: the playhead follows the pointer and snaps to markers in reach; Alt scrubs freely.
/// Doesn't edit the project.
/// </summary>
internal sealed class ScrubInteraction(EditorState state, TimelineViewport viewport, TimelineSnapper snapper) : ITimelineInteraction
{
    /// <summary>Moves the playhead under <paramref name="p"/> (never before 0), snapped to markers unless Alt is held.</summary>
    public void Seek(Point p, KeyModifiers modifiers)
    {
        var time = Math.Max(0, viewport.XToTime(p.X));
        state.Seek(modifiers.HasFlag(KeyModifiers.Alt) ? time : snapper.SnapPlayhead(time));
    }

    /// <inheritdoc />
    public void Move(Point p, KeyModifiers modifiers) => Seek(p, modifiers);

    /// <inheritdoc />
    public void Complete() { }
}
