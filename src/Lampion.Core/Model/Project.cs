using System.Text.Json.Serialization;

namespace Lampion.Core.Model;

[JsonConverter(typeof(JsonStringEnumConverter<StageObjectKind>))]
public enum StageObjectKind
{
    Truss,
    Platform,
    Wall,

    /// <summary>A video surface: shows the timeline's video in the 3D preview.</summary>
    Screen,
}

/// <summary>A piece of set: an oriented box in metres, centred on <see cref="Position"/>.</summary>
public sealed class StageObject
{
    public string Id { get; set; } = Ids.New();
    public string Name { get; set; } = "Object";
    public StageObjectKind Kind { get; set; } = StageObjectKind.Platform;
    public Vec3 Position { get; set; }

    /// <summary>Width (local X), height (Y) and depth (Z) in metres. A truss runs along its width.</summary>
    public Vec3 Size { get; set; } = new(2, 0.3, 0.3);

    /// <summary>Degrees around Y.</summary>
    public double Yaw { get; set; }

    public string Color { get; set; } = "#3A3A40";

    public StageObject Clone() =>
        new()
        {
            Name = Name,
            Kind = Kind,
            Position = Position,
            Size = Size,
            Yaw = Yaw,
            Color = Color,
        };
}

public sealed class StageSettings
{
    public List<StageObject> Objects { get; set; } = [];

    /// <summary>How visible beams are in the air, 0..1.</summary>
    public double Haze { get; set; } = 0.6;

    /// <summary>House light level so the set is readable with every fixture off, 0..1.</summary>
    public double Ambient { get; set; } = 0.3;

    /// <summary>Lets the rig's combined output tint the room's ambient light, like real bounce off the deck.</summary>
    public bool LightBounce { get; set; }

    /// <summary>Snap step for placing things in the stage builder, in metres.</summary>
    public double GridStep { get; set; } = 0.25;

    public static StageSettings CreateDefault() =>
        new()
        {
            Objects =
            [
                new StageObject { Name = "Deck", Kind = StageObjectKind.Platform, Position = new Vec3(0, 0.5, -1), Size = new Vec3(12, 1, 8), Color = "#2B2622" },
                new StageObject { Name = "Back wall", Kind = StageObjectKind.Wall, Position = new Vec3(0, 4, -5.1), Size = new Vec3(14, 8, 0.2), Color = "#1E1E24" },
                new StageObject { Name = "LED screen", Kind = StageObjectKind.Screen, Position = new Vec3(0, 3.6, -4.95), Size = new Vec3(6.4, 3.6, 0.08), Color = "#050505" },
                new StageObject { Name = "Front truss", Kind = StageObjectKind.Truss, Position = new Vec3(0, 6, 2), Size = new Vec3(13, 0.3, 0.3), Color = "#B8BCC4" },
                new StageObject { Name = "Back truss", Kind = StageObjectKind.Truss, Position = new Vec3(0, 6, -3.5), Size = new Vec3(13, 0.3, 0.3), Color = "#B8BCC4" },
            ],
        };
}

public sealed class Marker
{
    public string Id { get; set; } = Ids.New();
    public double Time { get; set; }
    public string Label { get; set; } = "";
}

public sealed class Project
{
    public int FormatVersion { get; set; } = 1;
    public string Name { get; set; } = "Untitled show";

    /// <summary>Seconds. The show ends here even if clips run longer.</summary>
    public double Duration { get; set; } = 60;

    public double Bpm { get; set; } = 120;

    /// <summary>Seconds to the first downbeat, so the beat grid lines up with the music.</summary>
    public double BeatOffset { get; set; }

    public StageSettings Stage { get; set; } = StageSettings.CreateDefault();
    public List<FixtureProfile> Profiles { get; set; } = [];
    public List<Fixture> Fixtures { get; set; } = [];
    public List<FixtureGroup> Groups { get; set; } = [];
    public List<Track> Tracks { get; set; } = [];
    public List<Marker> Markers { get; set; } = [];

    public FixtureProfile? ProfileOf(Fixture fixture) => Profiles.Find(p => p.Id == fixture.ProfileId);

    public Fixture? FixtureById(string id) => Fixtures.Find(f => f.Id == id);

    public IEnumerable<Clip> AllClips => Tracks.SelectMany(t => t.Clips);

    /// <summary>The track's fixtures (explicit ones first, then group members), deduplicated and ordered.</summary>
    public List<Fixture> ResolveTargets(Track track)
    {
        var seen = new HashSet<string>();
        var result = new List<Fixture>();
        void Add(string id)
        {
            if (seen.Add(id) && FixtureById(id) is { } fixture)
                result.Add(fixture);
        }
        foreach (var id in track.FixtureIds)
            Add(id);
        foreach (var groupId in track.GroupIds)
            if (Groups.Find(g => g.Id == groupId) is { } group)
                foreach (var id in group.FixtureIds)
                    Add(id);
        return OrderFixtures(result, track.Order);
    }

    internal static List<Fixture> OrderFixtures(List<Fixture> fixtures, FixtureOrder order)
    {
        if (fixtures.Count < 2)
            return fixtures;
        var center = fixtures.Average(f => f.Position.X);
        return order switch
        {
            FixtureOrder.LeftToRight => fixtures.OrderBy(f => f.Position.X).ToList(),
            FixtureOrder.RightToLeft => fixtures.OrderByDescending(f => f.Position.X).ToList(),
            FixtureOrder.CenterOut => fixtures.OrderBy(f => Math.Abs(f.Position.X - center)).ToList(),
            FixtureOrder.OutsideIn => fixtures.OrderByDescending(f => Math.Abs(f.Position.X - center)).ToList(),
            _ => fixtures,
        };
    }

    /// <summary>The first address in <paramref name="universe"/> where <paramref name="channels"/> fit, or -1.</summary>
    public int NextFreeAddress(int universe, int channels, string? ignoreFixtureId = null)
    {
        var used = new bool[513];
        foreach (var fixture in Fixtures)
        {
            if (fixture.Universe != universe || fixture.Id == ignoreFixtureId)
                continue;
            var count = ProfileOf(fixture)?.Channels.Count ?? 1;
            for (var a = fixture.Address; a < fixture.Address + count && a <= 512; a++)
                if (a >= 1)
                    used[a] = true;
        }
        for (var start = 1; start + channels - 1 <= 512; start++)
        {
            var free = true;
            for (var a = start; a < start + channels; a++)
                if (used[a])
                {
                    free = false;
                    start = a;
                    break;
                }
            if (free)
                return start;
        }
        return -1;
    }

    /// <summary>Human-readable patch problems: overlaps and fixtures running past channel 512.</summary>
    public List<string> PatchProblems()
    {
        var problems = new List<string>();
        var placed = Fixtures
            .Select(f => (Fixture: f, Count: ProfileOf(f)?.Channels.Count ?? 0))
            .OrderBy(x => x.Fixture.Universe)
            .ThenBy(x => x.Fixture.Address)
            .ToList();
        foreach (var (fixture, count) in placed)
        {
            if (ProfileOf(fixture) is null)
                problems.Add($"\"{fixture.Name}\" has no fixture profile.");
            if (fixture.Address < 1 || fixture.Address + count - 1 > 512)
                problems.Add($"\"{fixture.Name}\" ({fixture.Universe}.{fixture.Address}, {count}ch) does not fit in a universe.");
        }
        for (var i = 0; i < placed.Count; i++)
        for (var j = i + 1; j < placed.Count; j++)
        {
            var (a, ac) = placed[i];
            var (b, bc) = placed[j];
            if (a.Universe != b.Universe)
                continue;
            if (b.Address <= a.Address + ac - 1 && a.Address <= b.Address + bc - 1)
                problems.Add($"\"{a.Name}\" and \"{b.Name}\" overlap in universe {a.Universe}.");
        }
        return problems;
    }

    public double ContentEnd =>
        Math.Max(Duration, AllClips.Select(c => c.End).DefaultIfEmpty(0).Max());

    public static Project CreateDemo()
    {
        var project = new Project { Name = "Untitled show" };
        project.Profiles.AddRange(BuiltInProfiles.All().Select(p => p.Clone(newId: false)));
        var rgbw = project.Profiles.First(p => p.Id == BuiltInProfiles.RgbwParId);
        var head = project.Profiles.First(p => p.Id == BuiltInProfiles.MovingHeadId);

        var front = new FixtureGroup { Name = "Front pars", Color = "#E6A23A" };
        var heads = new FixtureGroup { Name = "Moving heads", Color = "#3AA0E6" };
        for (var i = 0; i < 6; i++)
        {
            var fixture = new Fixture
            {
                Name = $"Par {i + 1}",
                ProfileId = rgbw.Id,
                Address = 1 + i * rgbw.Channels.Count,
                Position = new Vec3(-5 + i * 2, 5.65, 2),
                Pitch = -55,
                Yaw = 180,
            };
            project.Fixtures.Add(fixture);
            front.FixtureIds.Add(fixture.Id);
        }
        for (var i = 0; i < 4; i++)
        {
            var fixture = new Fixture
            {
                Name = $"Head {i + 1}",
                ProfileId = head.Id,
                Address = 101 + i * head.Channels.Count,
                Position = new Vec3(-4.5 + i * 3, 5.6, -3.5),
                Pitch = -70,
            };
            project.Fixtures.Add(fixture);
            heads.FixtureIds.Add(fixture.Id);
        }
        project.Groups.AddRange([front, heads]);

        var wash = new Track { Name = "Front wash", GroupIds = [front.Id], Order = FixtureOrder.LeftToRight };
        wash.Clips.Add(new LightClip { Name = "Warm open", Start = 0, Duration = 8, FadeIn = 2, Effect = new SolidEffect { Color = "#FF8C3A" } });
        wash.Clips.Add(new LightClip
        {
            Name = "Rainbow",
            Start = 8,
            Duration = 12,
            Effect = new RainbowEffect { Speed = 0.2, Spread = 1 },
            TransitionIn = new Transition { Duration = 2 },
        });
        var beams = new Track { Name = "Heads", GroupIds = [heads.Id], Order = FixtureOrder.LeftToRight };
        beams.Clips.Add(new LightClip
        {
            Name = "Chase",
            Start = 4,
            Duration = 16,
            FadeIn = 1,
            FadeOut = 1,
            Effect = new ChaseEffect { Colors = ["#3AA0FF", "#FF3A9A"], StepTime = 0.5, Tail = 1.5, Background = 0.1 },
            Automation =
            [
                new AutomationLane
                {
                    Target = AutomationTarget.Role,
                    Role = ChannelRole.Pan,
                    Keys = [new Keyframe { Time = 0, Value = 0.4, Easing = Easing.EaseInOut }, new Keyframe { Time = 8, Value = 0.6, Easing = Easing.EaseInOut }, new Keyframe { Time = 16, Value = 0.4 }],
                },
            ],
        });
        project.Tracks.AddRange([beams, wash, new Track { Name = "Audio", Kind = TrackKind.Audio }, new Track { Name = "Video", Kind = TrackKind.Video }]);
        project.Duration = 30;
        return project;
    }
}
