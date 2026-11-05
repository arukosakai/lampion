using System.Text.Json.Serialization;

namespace Lampion.Core.Model.Rig;

/// <summary>
/// A fixture type: its channel layout and how it looks in the preview. Shared by every fixture whose
/// <see cref="Fixture.ProfileId"/> matches <see cref="Id"/>.
/// </summary>
public sealed class FixtureProfile
{
    /// <summary>Stable id fixtures refer to; built-in profiles use the constants in <see cref="BuiltInProfiles"/>.</summary>
    public string Id { get; set; } = Ids.New();

    /// <summary>Maker, for display.</summary>
    public string Manufacturer { get; set; } = "Generic";

    /// <summary>Model name, for display.</summary>
    public string Model { get; set; } = "Fixture";

    /// <summary>DMX mode name, for display.</summary>
    public string Mode { get; set; } = "Default";

    /// <summary>How the preview draws it.</summary>
    public BeamKind Beam { get; set; } = BeamKind.Par;

    /// <summary>Beam angle in degrees.</summary>
    public double BeamAngle { get; set; } = 25;

    /// <summary>Degrees the head turns across the full pan range (0..1).</summary>
    public double PanRange { get; set; } = 540;

    /// <summary>Degrees the head turns across the full tilt range (0..1).</summary>
    public double TiltRange { get; set; } = 270;

    /// <summary>The footprint, in DMX order; index 0 sits at the fixture's start address.</summary>
    public List<FixtureChannel> Channels { get; } = [];

    /// <summary>"Manufacturer Model (Nch)", for lists. Allocates.</summary>
    [JsonIgnore]
    public string DisplayName => $"{Manufacturer} {Model} ({Channels.Count}ch)";

    /// <summary>0-based offset of the first channel with this role, or -1. Linear in the channel count.</summary>
    public int IndexOf(ChannelRole role)
    {
        for (var i = 0; i < Channels.Count; i++)
            if (Channels[i].Role == role)
                return i;
        return -1;
    }

    /// <summary>Whether any channel has this role.</summary>
    public bool Has(ChannelRole role) => IndexOf(role) >= 0;

    /// <summary>Whether the fixture mixes colour (has any of red, green or blue). Also written to project files.</summary>
    public bool HasColor => Has(ChannelRole.Red) || Has(ChannelRole.Green) || Has(ChannelRole.Blue);

    /// <summary>Whether the fixture moves (has pan or tilt). Also written to project files.</summary>
    public bool HasPanTilt => Has(ChannelRole.Pan) || Has(ChannelRole.Tilt);

    /// <summary>A deep copy.</summary>
    /// <param name="newId">True to give the copy a fresh id (a new profile); false to keep it (the same profile, e.g. built-ins).</param>
    public FixtureProfile Clone(bool newId = true)
    {
        var copy = new FixtureProfile
        {
            Id = newId ? Ids.New() : Id,
            Manufacturer = Manufacturer,
            Model = Model,
            Mode = Mode,
            Beam = Beam,
            BeamAngle = BeamAngle,
            PanRange = PanRange,
            TiltRange = TiltRange,
        };
        foreach (var channel in Channels)
            copy.Channels.Add(channel.Clone());
        return copy;
    }
}
