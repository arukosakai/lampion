namespace Lampion.Core.Model.Stage;

/// <summary>
/// Set pieces that move as one, with the fixtures hung on them: an arch, a truss cube. Pieces and fixtures
/// point here through their <c>StructureId</c>; see <see cref="StructureEdits"/>.
/// </summary>
public sealed class Structure
{
    /// <summary>Stable id members refer to.</summary>
    public string Id { get; set; } = Ids.New();

    /// <summary>Name shown in the inspector and the stage readout.</summary>
    public string Name { get; set; } = "Structure";

    /// <summary>Degrees the structure has been turned in total, so the inspector can show and scrub it.</summary>
    public double Yaw { get; set; }
}
