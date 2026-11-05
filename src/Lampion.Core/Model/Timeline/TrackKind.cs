using System.Text.Json.Serialization;

namespace Lampion.Core.Model.Timeline;

/// <summary>What a track holds. Saved by name.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TrackKind>))]
public enum TrackKind
{
    /// <summary><see cref="LightClip"/>s driving fixtures.</summary>
    Light,

    /// <summary><see cref="MediaClip"/>s that play sound.</summary>
    Audio,

    /// <summary><see cref="MediaClip"/>s shown in the video monitor and on stage screens.</summary>
    Video,
}
