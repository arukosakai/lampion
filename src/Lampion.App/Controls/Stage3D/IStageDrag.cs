using Avalonia;

namespace Lampion.App.Controls.Stage3D;

/// <summary>One kind of pointer drag on the stage view, from press to release. Instances are reused across drags.</summary>
internal interface IStageDrag
{
    /// <summary>The pointer moved while the button is held.</summary>
    /// <param name="point">Pointer position in the view, pixels.</param>
    /// <param name="delta">Movement since the previous pointer event, pixels.</param>
    void Move(Point point, Vector delta);

    /// <summary>The button came up at <paramref name="point"/>; the drag is over.</summary>
    void Release(Point point);
}
