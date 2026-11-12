namespace Lampion.Core.Persistence;

/// <summary>A fixture a saved console refers to, with what's needed to find its counterpart in another project.</summary>
/// <param name="Id">The fixture's id in the project the console was saved from.</param>
/// <param name="Name">Its name there, for messages.</param>
/// <param name="Universe">1-based universe; with <paramref name="Address"/>, how it's matched when the id isn't found.</param>
/// <param name="Address">1-based start address.</param>
public sealed record LiveFixtureRef(string Id, string Name, int Universe, int Address);
