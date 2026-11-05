namespace Lampion.Core.Export;

/// <summary>One track of the exported show: a fixture group, or a lone fixture, played by one Sequence.</summary>
/// <param name="Name">Track and function name.</param>
/// <param name="Color">Show Manager colour, <c>#RRGGBB</c>.</param>
/// <param name="Fixtures">The lane's fixtures, in group order; never empty.</param>
internal sealed record QlcLane(string Name, string Color, List<QlcPatchedFixture> Fixtures)
{
    /// <summary>Channels in one step of the lane: every fixture's footprint, back to back.</summary>
    public int Width => Fixtures.Sum(f => f.Profile.Channels.Count);
}
