using System.Text.Json.Serialization;

namespace Lampion.Core.Model.Animation;

/// <summary>
/// What an automation lane drives. The colour targets are semantic: Lampion maps them onto whichever
/// R/G/B/W/dimmer channels each fixture has. Role and Channel write DMX channels directly. Saved by name.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AutomationTarget>))]
public enum AutomationTarget
{
    /// <summary>Hue rotation; 1.0 is one full turn, so 0 → 3 is three trips around the wheel.</summary>
    HueShift,

    /// <summary>Multiplies the effect's saturation; 0 is grey, values above 1 are clamped by HSV.</summary>
    Saturation,

    /// <summary>Multiplies the effect's intensity; negative values count as 0, values above 1 brighten.</summary>
    Intensity,

    /// <summary>Multiplies the clip's opacity against the tracks below, 0..1.</summary>
    Opacity,

    /// <summary>An explicit white level, 0..1, instead of white extracted from R/G/B.</summary>
    White,

    /// <summary>Replaces the effect's red, 0..1.</summary>
    Red,

    /// <summary>Replaces the effect's green, 0..1.</summary>
    Green,

    /// <summary>Replaces the effect's blue, 0..1.</summary>
    Blue,

    /// <summary>The first channel with <see cref="AutomationLane.Role"/> on each fixture.</summary>
    Role,

    /// <summary>A fixed 1-based channel within each fixture's footprint.</summary>
    Channel,
}
