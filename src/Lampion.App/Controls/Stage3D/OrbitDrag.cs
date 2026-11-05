using Avalonia;

namespace Lampion.App.Controls.Stage3D;

/// <summary>Right/middle-button (or Hand tool) drag: orbits the camera, or pans it when started with middle or Shift.</summary>
internal sealed class OrbitDrag(StageView view) : IStageDrag
{
    private bool _pan;

    /// <summary>Starts a drag; <paramref name="pan"/> slides the view instead of turning it.</summary>
    public void Begin(bool pan) => _pan = pan;

    /// <inheritdoc/>
    public void Move(Point point, Vector delta)
    {
        if (_pan)
            view.Camera.Pan((float)delta.X, (float)delta.Y, (float)view.Bounds.Height);
        else
            view.Camera.Orbit((float)delta.X, (float)delta.Y);
    }

    /// <inheritdoc/>
    public void Release(Point point) { }
}
