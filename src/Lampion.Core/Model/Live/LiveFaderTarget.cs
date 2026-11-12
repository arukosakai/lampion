using System.Text.Json.Serialization;

namespace Lampion.Core.Model.Live;

/// <summary>What a console fader controls. Saved by name.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LiveFaderTarget>))]
public enum LiveFaderTarget
{
    /// <summary>Brightness of its fixtures (white light unless a hue fader colours them). Highest fader wins.</summary>
    Intensity,

    /// <summary>Colour of its fixtures, around the hue wheel.</summary>
    Hue,

    /// <summary>Colour saturation of its fixtures (only matters with a hue fader on them).</summary>
    Saturation,

    /// <summary>One channel role of its fixtures (pan, tilt, strobe, zoom, gobo…), straight through.</summary>
    Role,

    /// <summary>The brightness of one pad's effect.</summary>
    PadIntensity,

    /// <summary>The speed of one pad's effect: ¼× at the bottom, 1× in the middle, 4× at the top.</summary>
    PadSpeed,

    /// <summary>Rotates the hue of one pad's effect by up to a full turn.</summary>
    PadHueShift,
}
