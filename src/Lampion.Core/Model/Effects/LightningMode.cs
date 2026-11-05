using System.Text.Json.Serialization;

namespace Lampion.Core.Model.Effects;

/// <summary>Whether a <see cref="LightningEffect"/>'s fixtures strike together. Saved by name.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LightningMode>))]
public enum LightningMode
{
    /// <summary>Every fixture flashes at the same instants.</summary>
    Together,

    /// <summary>Each fixture strikes on its own schedule.</summary>
    Scattered,
}
