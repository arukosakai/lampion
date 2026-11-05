namespace Lampion.Core.Model.Stage;

/// <summary>The pieces of a built truss structure, not yet added to any project.</summary>
/// <param name="Pieces">Every truss segment, legs first.</param>
/// <param name="Spans">The horizontal top spans, front first, then back, left and right; also in <paramref name="Pieces"/>.</param>
public sealed record TrussStructure(IReadOnlyList<StageObject> Pieces, IReadOnlyList<StageObject> Spans);
