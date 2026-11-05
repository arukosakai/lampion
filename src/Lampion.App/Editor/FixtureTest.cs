namespace Lampion.App.Editor;

/// <summary>Channel values held on patched fixtures while testing a profile.</summary>
/// <param name="FixtureIds">Fixtures whose output is overridden.</param>
/// <param name="Values">DMX values (0–255) written from each fixture's first channel; extra values past channel 512 are dropped.</param>
public sealed record FixtureTest(IReadOnlySet<string> FixtureIds, byte[] Values);
