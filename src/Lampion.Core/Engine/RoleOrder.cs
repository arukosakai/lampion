using System.Runtime.CompilerServices;

namespace Lampion.Core.Engine;

/// <summary>Roles (as bytes) in the order they were first set, stored inline in <see cref="FixtureState"/>.</summary>
[InlineArray(ChannelRoles.Count)]
internal struct RoleOrder
{
    private byte _element;
}
