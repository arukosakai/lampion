using System.Text.Json.Serialization;

namespace Lampion.App.Output;

/// <summary>Where DMX output goes. Saved by name in the user settings, so renaming a value forgets the choice.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DmxOutputKind>))]
internal enum DmxOutputKind
{
    /// <summary>Art-Net over the network, to nodes or a running QLC+.</summary>
    ArtNet,

    /// <summary>An Enttec DMX USB Pro or a compatible interface on a serial port.</summary>
    EnttecPro,

    /// <summary>An Enttec Open DMX USB or another plain FTDI cable on a serial port.</summary>
    OpenDmx,
}
