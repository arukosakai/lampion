namespace Lampion.Core.Persistence;

/// <summary>A group a saved console refers to; matched by id, then by name (ignoring case), in another project.</summary>
/// <param name="Id">The group's id in the project the console was saved from.</param>
/// <param name="Name">Its name there.</param>
public sealed record LiveGroupRef(string Id, string Name);
