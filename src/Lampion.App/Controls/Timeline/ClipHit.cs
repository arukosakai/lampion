using Lampion.Core.Model.Timeline;

namespace Lampion.App.Controls.Timeline;

/// <summary>A clip under the pointer and the part of it that was hit.</summary>
/// <param name="Clip">The clip; a <see cref="LightClip"/> for the fade and transition zones.</param>
/// <param name="Zone">The part of the clip.</param>
internal readonly record struct ClipHit(Clip Clip, ClipZone Zone);
