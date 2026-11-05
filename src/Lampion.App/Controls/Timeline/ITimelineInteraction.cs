using Avalonia;
using Avalonia.Input;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// A pointer drag in progress on the timeline, from press to release. One instance per gesture; the
/// timeline forwards pointer moves to it while it holds the pointer capture.
/// </summary>
internal interface ITimelineInteraction
{
    /// <summary>Applies the drag with the pointer at <paramref name="p"/> (control coordinates).</summary>
    /// <param name="p">Pointer position.</param>
    /// <param name="modifiers">Held keys; Alt turns snapping off.</param>
    void Move(Point p, KeyModifiers modifiers);

    /// <summary>The pointer was released: record the edit (an undo step) if anything changed.</summary>
    void Complete();
}
