using Lampion.Core.Model.Rig;

namespace Lampion.Core.Engine;

/// <summary>Sizing for the per-role tables in <see cref="FixtureState"/> and <see cref="ChannelLayout"/>.</summary>
internal static class ChannelRoles
{
    /// <summary>Number of <see cref="ChannelRole"/> values; relies on <see cref="ChannelRole.Generic"/> being the last one.</summary>
    public const int Count = (int)ChannelRole.Generic + 1;

    /// <summary>Whether <paramref name="role"/> is a defined value that fits the tables (JSON can carry arbitrary integers).</summary>
    public static bool IsValid(ChannelRole role) => (uint)role < Count;
}
