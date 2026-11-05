namespace Lampion.App.Rendering;

/// <summary>
/// Latest decoded video picture, shared between the media player thread and the renderers.
/// Writers and readers must <c>lock</c> the instance while touching <see cref="Pixels"/>.
/// </summary>
public sealed class VideoFrame
{
    /// <summary>BGRA, 4 bytes per pixel, rows top to bottom, <see cref="Width"/> × <see cref="Height"/> pixels.</summary>
    public byte[] Pixels { get; set; } = [];

    /// <summary>Picture width in pixels; 0 until the first frame arrives.</summary>
    public int Width { get; set; }

    /// <summary>Picture height in pixels.</summary>
    public int Height { get; set; }

    /// <summary>Bumped by the writer on every new picture, so readers upload only what changed.</summary>
    public int Version { get; set; }
}
