using Lampion.Core.Model;

namespace Lampion.Core.Persistence;

/// <summary>Venues that ship with Lampion, offered on the start screen next to the user's own templates.</summary>
public static class StageTemplates
{
    public static IReadOnlyList<(StageLayout Layout, string Description)> BuiltIn() =>
    [
        (Club(), "Small stage, one front truss with four RGB pars, two LED bars on the floor"),
        (StageLayout.From(Project.CreateDemo(), "Concert stage"), "12 m deck, two trusses, six RGBW pars, four moving heads, LED wall"),
    ];

    private static StageLayout Club()
    {
        var project = new Project { Name = "Club stage" };
        project.Profiles.AddRange(BuiltInProfiles.All().Select(p => p.Clone(newId: false)));
        var par = project.Profiles.First(p => p.Id == BuiltInProfiles.DimRgbParId);
        var bar = project.Profiles.First(p => p.Id == BuiltInProfiles.LedBarId);
        var truss = new StageObject { Name = "Front truss", Kind = StageObjectKind.Truss, Position = new Vec3(0, 4.2, 1.2), Size = new Vec3(7, 0.3, 0.3), Color = "#B8BCC4" };
        project.Stage = new StageSettings
        {
            Haze = 0.45,
            Objects =
            [
                new StageObject { Name = "Stage", Kind = StageObjectKind.Platform, Position = new Vec3(0, 0.3, -0.5), Size = new Vec3(6, 0.6, 4), Color = "#2B2622" },
                new StageObject { Name = "Back wall", Kind = StageObjectKind.Wall, Position = new Vec3(0, 2.5, -2.6), Size = new Vec3(8, 5, 0.2), Color = "#1E1E24" },
                truss,
            ],
        };

        var pars = new FixtureGroup { Name = "Front pars", Color = "#E6A23A" };
        var bars = new FixtureGroup { Name = "Floor bars", Color = "#3AA0E6" };
        var front = Enumerable.Range(0, 4).Select(i => new Fixture { Name = $"Par {i + 1}", ProfileId = par.Id, Address = 1 + i * par.Channels.Count, Yaw = 180 }).ToList();
        StageMath.DistributeAlong(truss, front);
        foreach (var fixture in front)
            StageMath.AimAt(fixture, fixture.Position with { Y = 0.6, Z = -0.8 });
        for (var i = 0; i < 2; i++)
        {
            var x = i == 0 ? -2.2 : 2.2;
            front.Add(new Fixture { Name = $"Bar {i + 1}", ProfileId = bar.Id, Address = 21 + i * bar.Channels.Count, Position = new Vec3(x, 0.66, -2.2), Pitch = 70 });
        }
        project.Fixtures.AddRange(front);
        pars.FixtureIds.AddRange(front.Where(f => f.ProfileId == par.Id).Select(f => f.Id));
        bars.FixtureIds.AddRange(front.Where(f => f.ProfileId == bar.Id).Select(f => f.Id));
        project.Groups.AddRange([pars, bars]);
        return StageLayout.From(project);
    }
}
