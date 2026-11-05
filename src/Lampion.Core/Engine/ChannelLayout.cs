using Lampion.Core.Model.Rig;

namespace Lampion.Core.Engine;

/// <summary>
/// Where each role sits in a profile's footprint: a snapshot of <see cref="FixtureProfile.IndexOf"/> for
/// every role, so mapping a fixture costs no searches. Allocation-free; <c>default</c> is a profile with no
/// channels. It does not track later edits to the profile, so build it from the profile you map with.
/// </summary>
public readonly struct ChannelLayout
{
    private readonly RoleIndices _indexPlusOne;

    private ChannelLayout(RoleIndices indexPlusOne) => _indexPlusOne = indexPlusOne;

    /// <summary>The layout of <paramref name="profile"/>'s channels as they are now. Linear in the channel count.</summary>
    public static ChannelLayout Of(FixtureProfile profile)
    {
        var indices = new RoleIndices();
        var channels = profile.Channels;
        for (var i = 0; i < channels.Count; i++)
        {
            var role = channels[i].Role;
            if (ChannelRoles.IsValid(role) && indices[(int)role] == 0)
                indices[(int)role] = i + 1;
        }
        return new ChannelLayout(indices);
    }

    /// <summary>0-based offset of the first channel with this role, or -1.</summary>
    public int IndexOf(ChannelRole role) => ChannelRoles.IsValid(role) ? _indexPlusOne[(int)role] - 1 : -1;

    /// <summary>Whether any channel has this role.</summary>
    public bool Has(ChannelRole role) => IndexOf(role) >= 0;

    /// <summary>Whether the profile mixes colour (has any of red, green or blue).</summary>
    public bool HasColor => Has(ChannelRole.Red) || Has(ChannelRole.Green) || Has(ChannelRole.Blue);
}
