using System.Text.Json;
using System.Text.Json.Serialization;
using Lampion.Core.Model;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Stage;

namespace Lampion.Core.Persistence;

/// <summary>
/// A reusable venue (<c>.lampstage</c>): the set pieces and stage look, optionally with the rig (fixtures,
/// their profiles and groups). Saved apart from any show so one layout can seed many projects.
/// </summary>
public sealed class StageLayout
{
    /// <summary>File extension, including the dot.</summary>
    public const string Extension = ".lampstage";

    /// <summary>File format version.</summary>
    public int FormatVersion { get; set; } = 1;

    /// <summary>Name shown on the start screen.</summary>
    public string Name { get; set; } = "Stage layout";

    /// <summary>The set and room.</summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Replace)]
    public StageSettings Stage { get; set; } = new();

    /// <summary>Profiles the rig uses.</summary>
    public List<FixtureProfile> Profiles { get; } = [];

    /// <summary>The rig; empty for a set-only layout.</summary>
    public List<Fixture> Fixtures { get; } = [];

    /// <summary>The rig's groups.</summary>
    public List<FixtureGroup> Groups { get; } = [];

    /// <summary>Whether the layout carries fixtures. Also written to layout files.</summary>
    public bool HasRig => Fixtures.Count > 0;

    /// <summary>Copies the project's stage and rig; only profiles some fixture uses are included. Nothing is shared with the project.</summary>
    /// <param name="project">The source project.</param>
    /// <param name="name">Layout name; null uses the project's name.</param>
    public static StageLayout From(Project project, string? name = null)
    {
        // A JSON round trip gives a deep copy, so later edits to the project never leak into the layout.
        var copy = ProjectSerializer.Deserialize(ProjectSerializer.Serialize(project, indented: false));
        var used = copy.Fixtures.Select(f => f.ProfileId).ToHashSet();
        var layout = new StageLayout { Name = name ?? project.Name, Stage = copy.Stage };
        layout.Profiles.AddRange(copy.Profiles.Where(p => used.Contains(p.Id)));
        layout.Fixtures.AddRange(copy.Fixtures);
        layout.Groups.AddRange(copy.Groups);
        return layout;
    }

    /// <summary>
    /// Replaces the project's stage with this layout. With <paramref name="includeRig"/> the fixtures and
    /// groups are replaced too, profiles are added (same id = updated), and tracks drop references to
    /// fixtures and groups that no longer exist. Nothing is shared with the layout afterwards.
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
        project.Fixtures.Clear();
        project.Fixtures.AddRange(copy.Fixtures);
        project.Groups.Clear();
        project.Groups.AddRange(copy.Groups);
        var fixtureIds = project.Fixtures.Select(f => f.Id).ToHashSet();
        var groupIds = project.Groups.Select(g => g.Id).ToHashSet();
        foreach (var track in project.Tracks)
        {
            track.FixtureIds.RemoveAll(id => !fixtureIds.Contains(id));
            track.GroupIds.RemoveAll(id => !groupIds.Contains(id));
        }
    }

    /// <summary>The layout as indented JSON.</summary>
    public static string Serialize(StageLayout layout) => JsonSerializer.Serialize(layout, LampionJson.Indented);

    /// <summary>A layout from JSON.</summary>
    /// <exception cref="JsonException">The text is not a layout.</exception>
    /// <exception cref="InvalidDataException">The text is empty or damaged.</exception>
    public static StageLayout Deserialize(string json) => LampionJson.Read<StageLayout>(json, "The stage layout file is empty.");

    /// <summary>Writes the layout to <paramref name="path"/> via a temp file, replacing any existing file.</summary>
    public void Save(string path)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, Serialize(this));
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>Reads a layout file.</summary>
    /// <exception cref="IOException">The file can't be read.</exception>
    /// <exception cref="JsonException">The file is not a layout.</exception>
    /// <exception cref="InvalidDataException">The file is empty or damaged.</exception>
    public static StageLayout Load(string path) => Deserialize(File.ReadAllText(path));
}
