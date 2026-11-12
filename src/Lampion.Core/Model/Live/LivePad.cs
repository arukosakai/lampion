using System.Text.Json.Serialization;
using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Timeline;

namespace Lampion.Core.Model.Live;

/// <summary>
/// A launchpad-style button that plays an effect on its fixtures while it's on, over the faders. Like the
/// faders, it never touches fixtures the playing show drives.
/// </summary>
public sealed class LivePad
{
    /// <summary>Stable id faders refer to.</summary>
    public string Id { get; set; } = Ids.New();

    /// <summary>Label on the pad.</summary>
    public string Name { get; set; } = "Pad";

    /// <summary>The look it plays, timed from the moment it's switched on.</summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Replace)]
    public Effect Effect { get; set; } = new RainbowEffect();

    /// <summary>Toggle or flash.</summary>
    public PadMode Mode { get; set; } = PadMode.Toggle;

    /// <summary>How it combines with the faders and pads before it.</summary>
    public BlendMode Blend { get; set; } = BlendMode.Normal;

    /// <summary>The order its effect walks the fixtures in.</summary>
    public FixtureOrder Order { get; set; } = FixtureOrder.LeftToRight;

    /// <summary>Fixtures it drives.</summary>
    public List<string> FixtureIds { get; } = [];

    /// <summary>Groups it drives, after <see cref="FixtureIds"/>.</summary>
    public List<string> GroupIds { get; } = [];

    /// <summary>The MIDI note or control that presses it, or null.</summary>
    public MidiBinding? Midi { get; set; }

    /// <summary>Velocity sent back to the controller's pad while on, which picks the LED colour on most grids (1–127).</summary>
    public int LedVelocity { get; set; } = 21;
}
