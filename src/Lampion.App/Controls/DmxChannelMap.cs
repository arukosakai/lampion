using Lampion.Core.Engine;
using Lampion.Core.Model;

namespace Lampion.App.Controls;

/// <summary>
/// Who is patched on each channel of one universe (fixture and channel name) and whether that fixture is
/// selected. Where fixtures overlap, the later one in the patch wins. Reuses its buffers; UI thread only.
/// </summary>
internal sealed class DmxChannelMap
{
    private readonly string?[] _owners = new string?[DmxFrame.UniverseSize];
    private readonly string?[] _roles = new string?[DmxFrame.UniverseSize];
    private readonly bool[] _selected = new bool[DmxFrame.UniverseSize];

    /// <summary>Bumped on every <see cref="Rebuild"/>, so labels derived from the map know when to refresh.</summary>
    public int Version { get; private set; }

    /// <summary>Refills the map for <paramref name="universe"/> from the patch and the fixture selection. Allocation-free.</summary>
    public void Rebuild(Project project, int universe, IReadOnlySet<string> selectedFixtureIds)
    {
        Array.Clear(_owners);
        Array.Clear(_roles);
        Array.Clear(_selected);
        foreach (var fixture in project.Fixtures)
        {
            if (fixture.Universe != universe || project.ProfileOf(fixture) is not { } profile)
                continue;
            var selected = selectedFixtureIds.Contains(fixture.Id);
            for (var i = 0; i < profile.Channels.Count; i++)
            {
                var a = fixture.Address - 1 + i;
                if (a is < 0 or >= DmxFrame.UniverseSize)
                    continue;
                _owners[a] = fixture.Name;
                _roles[a] = profile.Channels[i].Name;
                _selected[a] = selected;
            }
        }
        Version++;
    }

    /// <summary>Name of the fixture on 0-based <paramref name="channel"/>, or null when it is unpatched.</summary>
    public string? Owner(int channel) => _owners[channel];

    /// <summary>Name of the fixture's channel on 0-based <paramref name="channel"/>, or null when it is unpatched.</summary>
    public string? Role(int channel) => _roles[channel];

    /// <summary>Whether 0-based <paramref name="channel"/> belongs to a selected fixture.</summary>
    public bool IsSelected(int channel) => _selected[channel];
}
