using System.Text.Json.Serialization;

namespace Lampion.Core.Model.Effects;

/// <summary>Which way a <see cref="ChaseEffect"/> runs along the track's fixture order. Saved by name.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ChaseDirection>))]
public enum ChaseDirection
{
    /// <summary>First fixture to last, then wraps.</summary>
    Forward,

    /// <summary>Last fixture to first, then wraps.</summary>
    Backward,

    /// <summary>There and back.</summary>
    Bounce,
}
