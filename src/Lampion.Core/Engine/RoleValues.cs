using System.Runtime.CompilerServices;

namespace Lampion.Core.Engine;

/// <summary>One 0..1 value per channel role, stored inline in <see cref="FixtureState"/>.</summary>
[InlineArray(ChannelRoles.Count)]
internal struct RoleValues
{
    private double _element;
}
