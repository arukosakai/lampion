using System.Text.Json.Serialization;

namespace Lampion.Core.Model.Rig;

/// <summary>
/// What a DMX channel does. Lampion fills the colour roles itself; the rest are driven by automation.
/// Saved by name. <see cref="Generic"/> must stay the last value: per-role buffers are sized from it.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ChannelRole>))]
public enum ChannelRole
{
    /// <summary>Master intensity; Lampion puts the brightness here and full colour on R/G/B.</summary>
    Dimmer,

    /// <summary>Red emitter.</summary>
    Red,

    /// <summary>Green emitter.</summary>
    Green,

    /// <summary>Blue emitter.</summary>
    Blue,

    /// <summary>White emitter; filled from the common part of R/G/B when the fixture extracts white.</summary>
    White,

    /// <summary>Amber emitter (automation only).</summary>
    Amber,

    /// <summary>Ultraviolet emitter (automation only).</summary>
    UV,

    /// <summary>Shutter / strobe rate.</summary>
    Strobe,

    /// <summary>Pan, coarse byte (or the only byte).</summary>
    Pan,

    /// <summary>Pan, fine byte of a 16-bit pan.</summary>
    PanFine,

    /// <summary>Tilt, coarse byte (or the only byte).</summary>
    Tilt,

    /// <summary>Tilt, fine byte of a 16-bit tilt.</summary>
    TiltFine,

    /// <summary>Movement speed.</summary>
    PanTiltSpeed,

    /// <summary>Beam angle.</summary>
    Zoom,

    /// <summary>Colour wheel slot.</summary>
    ColorWheel,

    /// <summary>Gobo wheel slot.</summary>
    Gobo,

    /// <summary>Built-in effect / macro selection.</summary>
    Effect,

    /// <summary>Anything else; reachable through channel automation.</summary>
    Generic,
}
