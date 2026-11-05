using System.Runtime.CompilerServices;

namespace Lampion.Core.Engine;

/// <summary>Up to <see cref="FixtureState.MaxRawOverrides"/> channel overrides, stored inline in <see cref="FixtureState"/>.</summary>
[InlineArray(FixtureState.MaxRawOverrides)]
internal struct RawOverrides
{
    private RawOverride _element;
}
