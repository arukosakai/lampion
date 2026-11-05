namespace Lampion.Core.Export;

/// <summary>What <see cref="QlcExporter.Export"/> produced.</summary>
/// <param name="Xml">The complete .qxw document, UTF-8 without BOM once written.</param>
/// <param name="Warnings">Plain-language problems for the user (left-out fixtures, renamed show, trimmed media), in the order they were found.</param>
/// <param name="FixtureCount">Lampion fixtures that made it into the workspace.</param>
/// <param name="StepCount">Sequence steps written across all lanes.</param>
public sealed record QlcExportResult(string Xml, IReadOnlyList<string> Warnings, int FixtureCount, int StepCount);
