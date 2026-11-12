using System.Text.Json.Serialization;

namespace Lampion.Core.Model.Live;

/// <summary>How a pad reacts to presses. Saved by name.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PadMode>))]
public enum PadMode
{
    /// <summary>Press to start, press again to stop.</summary>
    Toggle,

    /// <summary>Runs only while held.</summary>
    Flash,
}
