using Avalonia;
using Lampion.App.Editor;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Controls.Timeline;

/// <summary>Dragging a light clip's fade-in or fade-out handle; the two fades never overlap. Fades don't snap.</summary>
internal sealed class ClipFadeInteraction : ClipInteraction
{
    private readonly LightClip _light;
    private readonly bool _fadeIn;
    private readonly double _originalFade;

    /// <summary>Starts dragging <paramref name="clip"/>'s fade-in (<paramref name="fadeIn"/>) or fade-out handle from pointer time <paramref name="startTime"/>.</summary>
    public ClipFadeInteraction(EditorState state, TimelineViewport viewport, TimelineSnapper snapper, LightClip clip, Track track, double startTime, bool fadeIn)
        : base(state, viewport, snapper, clip, track, startTime)
    {
        _light = clip;
        _fadeIn = fadeIn;
        _originalFade = fadeIn ? clip.FadeIn : clip.FadeOut;
    }

    /// <inheritdoc />
    protected override void Apply(Point p, double delta, double tolerance)
    {
        if (_fadeIn)
            _light.FadeIn = Math.Clamp(_originalFade + delta, 0, _light.Duration - _light.FadeOut);
        else
            _light.FadeOut = Math.Clamp(_originalFade - delta, 0, _light.Duration - _light.FadeIn);
    }
}
