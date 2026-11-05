using System.Runtime.CompilerServices;

namespace Lampion.Core.Engine;

/// <summary>One channel index (plus one, so 0 means "absent") per channel role, stored inline in <see cref="ChannelLayout"/>.</summary>
[InlineArray(ChannelRoles.Count)]
internal struct RoleIndices
{
    private int _element;
}
