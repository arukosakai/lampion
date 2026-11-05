namespace Lampion.Core.Model.Rig;

/// <summary>One DMX channel of a fixture profile.</summary>
public sealed class FixtureChannel
{
    /// <summary>Label shown in the profile editor and DMX monitor.</summary>
    public string Name { get; set; } = "Channel";

    /// <summary>What the channel does.</summary>
    public ChannelRole Role { get; set; } = ChannelRole.Generic;

    /// <summary>DMX value (0–255) written when nothing drives the channel (e.g. a shutter that must sit "open").</summary>
    public byte Default { get; set; }

    /// <summary>An independent copy.</summary>
    public FixtureChannel Clone() => new() { Name = Name, Role = Role, Default = Default };
}
