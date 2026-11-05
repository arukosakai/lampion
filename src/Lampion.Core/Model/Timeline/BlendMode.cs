using System.Text.Json.Serialization;

namespace Lampion.Core.Model.Timeline;

/// <summary>How a light track combines with the tracks below it. Saved by name.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BlendMode>))]
public enum BlendMode
{
    /// <summary>Replaces what is below, weighted by clip opacity (fades reveal the lower tracks).</summary>
    Normal,

    /// <summary>Highest takes precedence, channel by channel — the classic lighting-desk merge.</summary>
    Htp,

    /// <summary>Adds light on top.</summary>
    Add,
}
