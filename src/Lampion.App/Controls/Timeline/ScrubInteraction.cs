using Avalonia;
using Avalonia.Input;
using Lampion.App.Editor;

namespace Lampion.App.Controls.Timeline;

/// <summary>Dragging in the ruler: the playhead follows the pointer. Doesn't edit the project.</summary>
internal sealed class ScrubInteraction(EditorState state, TimelineViewport viewport) : ITimelineInteraction
{
    /// <summary>Moves the playhead under <paramref name="p"/> (never before 0).</summary>
    public void Seek(Point p) => state.Seek(Math.Max(0, viewport.XToTime(p.X)));

    /// <inheritdoc />
    public void Move(Point p, KeyModifiers modifiers) => Seek(p);

    /// <inheritdoc />
    public void Complete() { }
}
