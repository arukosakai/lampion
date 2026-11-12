using Lampion.Core.Model.Live;

namespace Lampion.Core.Persistence;

/// <summary>A saved console, remapped onto a project and ready to replace its <see cref="Model.Project.Live"/>.</summary>
/// <param name="Console">The console, with fixture and group ids and paths rewritten for the target project.</param>
/// <param name="UnmatchedFixtures">Names of fixtures with no counterpart (same id, or same universe and address); faders and pads lose them.</param>
/// <param name="UnmatchedGroups">Names of groups with no counterpart (same id, or same name).</param>
public sealed record LiveConsoleImport(LiveSet Console, IReadOnlyList<string> UnmatchedFixtures, IReadOnlyList<string> UnmatchedGroups);
