namespace Lampion.Core.Model.Rig;

/// <summary>A named, ordered set of fixtures that tracks can target as one.</summary>
public sealed class FixtureGroup
{
    /// <summary>Stable id tracks refer to.</summary>
    public string Id { get; set; } = Ids.New();

    /// <summary>Name shown in the UI; also the QLC+ lane name.</summary>
    public string Name { get; set; } = "Group";

    /// <summary>Swatch colour as <c>#RRGGBB</c>.</summary>
    public string Color { get; set; } = "#3A8EE6";

    /// <summary>Member fixture ids in group order; ids without a fixture are skipped when resolving.</summary>
    public List<string> FixtureIds { get; } = [];
}
