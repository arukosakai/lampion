using System.Text.Json.Serialization;

namespace Lampion.Core.Model.Timeline;

/// <summary>How a clip takes over from the one before it. Saved by name.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TransitionKind>))]
public enum TransitionKind
{
    /// <summary>Blends from the outgoing clip to the incoming one.</summary>
    Crossfade,

    /// <summary>Fades the outgoing clip to black, then the incoming one up.</summary>
    DipToBlack,

    /// <summary>A crossfade that sweeps across the fixtures in track order.</summary>
    Wipe,
}
