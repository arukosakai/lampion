using Avalonia.Controls;
using Avalonia.Media;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// A transparent child layer over the timeline that only draws the playhead. Playback invalidates just this
/// layer, so the clips under it aren't repainted every frame. Not hit-testable; input goes to the timeline.
/// </summary>
internal sealed class PlayheadLayer : Control
{
    private readonly PlayheadRenderer _renderer;

    /// <summary>A layer that paints with <paramref name="renderer"/>.</summary>
    public PlayheadLayer(PlayheadRenderer renderer)
    {
        _renderer = renderer;
        IsHitTestVisible = false;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context) => _renderer.Render(context);
}
