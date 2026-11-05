namespace Lampion.Core.Export;

internal static partial class QlcPatchPlanner
{
    /// <summary>A fixture already taking up channels in the workspace (from the base project, or placed earlier in this export).</summary>
    private sealed record Occupant(string Name, int Universe, int Address, int Channels, int QlcId);
}
