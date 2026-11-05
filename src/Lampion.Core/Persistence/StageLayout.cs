using System.Text.Json;
using Lampion.Core.Model;

namespace Lampion.Core.Persistence;

/// <summary>
/// A reusable venue: the set pieces and stage look, optionally with the rig (fixtures, their profiles and
/// groups). Saved apart from any show so one layout can seed many projects.
/// </summary>
public sealed class StageLayout
{
    public const string Extension = ".lampstage";

    public int FormatVersion { get; set; } = 1;
    public string Name { get; set; } = "Stage layout";
    public StageSettings Stage { get; set; } = new();
    public List<FixtureProfile> Profiles { get; set; } = [];
    public List<Fixture> Fixtures { get; set; } = [];
    public List<FixtureGroup> Groups { get; set; } = [];

    public bool HasRig => Fixtures.Count > 0;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowOutOfOrderMetadataProperties = true,
    };

    /// <summary>Copies the project's stage and rig; only profiles some fixture uses are included.</summary>
    public static StageLayout From(Project project, string? name = null)
    {
        // A JSON round trip gives a deep copy, so later edits to the project never leak into the layout.
        var copy = ProjectSerializer.Deserialize(ProjectSerializer.Serialize(project, indented: false));
        var used = copy.Fixtures.Select(f => f.ProfileId).ToHashSet();
        return new StageLayout
        {
            Name = name ?? project.Name,
            Stage = copy.Stage,
            Profiles = copy.Profiles.Where(p => used.Contains(p.Id)).ToList(),
            Fixtures = copy.Fixtures,
            Groups = copy.Groups,
        };
    }

    /// <summary>
    /// Replaces the project's stage with this layout. With <paramref name="includeRig"/> the fixtures and
    /// groups are replaced too, profiles are added (same id = updated), and tracks drop references to
    /// fixtures and groups that no longer exist.
    /// </summary>
    public void ApplyTo(Project project, bool includeRig)
    {
        var copy = Deserialize(Serialize(this));
        project.Stage = copy.Stage;
        if (!includeRig)
            return;
        foreach (var profile in copy.Profiles)
        {
            var index = project.Profiles.FindIndex(p => p.Id == profile.Id);
            if (index >= 0)
                project.Profiles[index] = profile;
            else
                project.Profiles.Add(profile);
        }
        project.Fixtures = copy.Fixtures;
        project.Groups = copy.Groups;
        var fixtureIds = project.Fixtures.Select(f => f.Id).ToHashSet();
        var groupIds = project.Groups.Select(g => g.Id).ToHashSet();
        foreach (var track in project.Tracks)
        {
            track.FixtureIds.RemoveAll(id => !fixtureIds.Contains(id));
            track.GroupIds.RemoveAll(id => !groupIds.Contains(id));
        }
    }

    public static string Serialize(StageLayout layout) => JsonSerializer.Serialize(layout, Options);

    public static StageLayout Deserialize(string json) =>
        JsonSerializer.Deserialize<StageLayout>(json, Options) ?? throw new InvalidDataException("The stage layout file is empty.");

    public void Save(string path)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, Serialize(this));
        File.Move(temp, path, overwrite: true);
    }

    public static StageLayout Load(string path) => Deserialize(File.ReadAllText(path));
}
