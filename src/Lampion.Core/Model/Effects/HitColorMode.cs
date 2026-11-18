using System.Text.Json.Serialization;

namespace Lampion.Core.Model.Effects;

/// <summary>Which colour each flash of a <see cref="HitEffect"/> takes. Saved by name.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<HitColorMode>))]
public enum HitColorMode
{
    /// <summary>Every hit is <see cref="HitEffect.Color"/>.</summary>
    Single,

    /// <summary>Each hit takes the next colour of <see cref="HitEffect.Colors"/>, wrapping round.</summary>
    Cycle,
}
