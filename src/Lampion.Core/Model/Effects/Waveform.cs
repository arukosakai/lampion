using System.Text.Json.Serialization;

namespace Lampion.Core.Model.Effects;

/// <summary>The shape of one cycle of a periodic effect, peaking at 1. Saved by name.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Waveform>))]
public enum Waveform
{
    /// <summary>Raised cosine: 0 at phase 0, 1 at phase 0.5.</summary>
    Sine,

    /// <summary>Linear up to 1 at phase 0.5 and back down.</summary>
    Triangle,

    /// <summary>1 at phase 0, falling linearly to 0.</summary>
    Saw,

    /// <summary>1 for the first half of the cycle, 0 for the second.</summary>
    Square,
}
