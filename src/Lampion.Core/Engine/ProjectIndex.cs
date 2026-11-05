using Lampion.Core.Model;
using Lampion.Core.Model.Rig;

namespace Lampion.Core.Engine;

/// <summary>
/// String-id lookups for one project, resolved to list positions. <see cref="Rebuild"/> refills the reused
/// tables from the model as it is now; the model has no change notifications, so call it once per frame
/// rather than trusting an older build. Allocates only when the project grows. Not thread-safe.
/// </summary>
internal sealed class ProjectIndex(Project project)
{
    private readonly Dictionary<string, int> _fixtureById = [];
    private readonly Dictionary<string, int> _profileById = [];
    private readonly Dictionary<string, FixtureGroup> _groupById = [];
    private int[] _canonical = [];
    private int[] _profileOf = [];
    private ChannelLayout[] _layouts = [];

    /// <summary>The indexed project.</summary>
    public Project Project { get; } = project;

    /// <summary>Number of fixtures at the last <see cref="Rebuild"/>; fixture indices run 0..FixtureCount-1.</summary>
    public int FixtureCount { get; private set; }

    /// <summary>Refreshes every table from the project. First match wins for duplicate ids, like the model's own lookups.</summary>
    public void Rebuild()
    {
        var profiles = Project.Profiles;
        _profileById.Clear();
        EnsureLength(ref _layouts, profiles.Count);
        for (var p = 0; p < profiles.Count; p++)
        {
            var profile = profiles[p];
            if (profile.Id is { } id)
                _profileById.TryAdd(id, p);
            _layouts[p] = ChannelLayout.Of(profile);
        }

        var fixtures = Project.Fixtures;
        _fixtureById.Clear();
        EnsureLength(ref _canonical, fixtures.Count);
        EnsureLength(ref _profileOf, fixtures.Count);
        for (var i = 0; i < fixtures.Count; i++)
        {
            var fixture = fixtures[i];
            _canonical[i] = fixture.Id is { } id && !_fixtureById.TryAdd(id, i) ? _fixtureById[id] : i;
            _profileOf[i] = fixture.ProfileId is { } profileId && _profileById.TryGetValue(profileId, out var p) ? p : -1;
        }

        _groupById.Clear();
        foreach (var group in Project.Groups)
            if (group.Id is { } id)
                _groupById.TryAdd(id, group);
        FixtureCount = fixtures.Count;
    }

    /// <summary>Index of the first fixture with this id, or -1.</summary>
    public int FixtureIndexOf(string? id) => id is not null && _fixtureById.TryGetValue(id, out var index) ? index : -1;

    /// <summary>
    /// The index whose state a fixture shows: itself, or the first fixture sharing its id. Fixtures with the
    /// same id were one entry in the old id-keyed state map, so they show the same light.
    /// </summary>
    public int CanonicalIndex(int fixture) => _canonical[fixture];

    /// <summary>The first group with this id, or null.</summary>
    public FixtureGroup? GroupById(string? id) => id is not null && _groupById.TryGetValue(id, out var group) ? group : null;

    /// <summary>The fixture's profile, or null when its profile id matches none.</summary>
    public FixtureProfile? ProfileOf(int fixture) => _profileOf[fixture] is var p and >= 0 ? Project.Profiles[p] : null;

    /// <summary>The layout of the fixture's profile; only meaningful when <see cref="ProfileOf"/> is not null.</summary>
    public ref readonly ChannelLayout LayoutOf(int fixture) => ref _layouts[_profileOf[fixture]];

    private static void EnsureLength<T>(ref T[] array, int length)
    {
        if (array.Length < length)
            array = new T[Math.Max(length, array.Length * 2)];
    }
}
