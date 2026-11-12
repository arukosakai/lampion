using System.Text.Json.Serialization;

namespace Lampion.Core.Model.Live;

/// <summary>Which MIDI message a binding listens for. Saved by name.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MidiBindingKind>))]
public enum MidiBindingKind
{
    /// <summary>A control change: sliders, knobs, and the buttons of controllers that send CCs.</summary>
    Control,

    /// <summary>A note on/off: pads and keys.</summary>
    Note,
}
