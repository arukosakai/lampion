using Lampion.Core.Model.Rig;

namespace Lampion.Core.Model.Live;

/// <summary>
/// A console fader. Fixture targets only reach fixtures the playing show doesn't drive; pad targets steer
/// one pad's effect. Its position is performance state and isn't saved.
/// </summary>
public sealed class LiveFader
{
    /// <summary>Stable id.</summary>
    public string Id { get; set; } = Ids.New();

    /// <summary>Label on the strip.</summary>
    public string Name { get; set; } = "Fader";

    /// <summary>What moving it does.</summary>
    public LiveFaderTarget Target { get; set; } = LiveFaderTarget.Intensity;

    /// <summary>The channel role, for <see cref="LiveFaderTarget.Role"/>.</summary>
    public ChannelRole Role { get; set; } = ChannelRole.Dimmer;

    /// <summary>The pad, for the pad targets; null otherwise.</summary>
    public string? PadId { get; set; }

    /// <summary>Fixtures it drives, for fixture targets.</summary>
    public List<string> FixtureIds { get; } = [];

    /// <summary>Groups it drives, after <see cref="FixtureIds"/>.</summary>
    public List<string> GroupIds { get; } = [];

    /// <summary>The MIDI control that moves it, or null.</summary>
    public MidiBinding? Midi { get; set; }
}
