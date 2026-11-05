using Lampion.Core.Model;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Stage;
using Lampion.Core.Model.Timeline;

namespace Lampion.Core.Tests;

public class StructureEditsTests
{
    private static (Project Project, Structure Arch, List<Fixture> Lights) ArchWithLights()
    {
        var project = new Project();
        project.Stage.Objects.Clear();
        var built = TrussStructures.Build(TrussShape.Arch, "Arch", new Vec3(0, 0, 0), 6, 0, 4, 0);
        project.Stage.Objects.AddRange(built.Pieces);
        project.Stage.Objects.Add(new StageObject { Name = "Riser" });
        var lights = Enumerable.Range(0, 3).Select(i => new Fixture { Id = $"l{i}" }).ToList();
        StageMath.DistributeAlong(built.Spans[0], lights);
        project.Fixtures.AddRange(lights);
        project.Fixtures.Add(new Fixture { Id = "free", Position = new Vec3(9, 5, 9) });
        var arch = StructureEdits.Create(project, "Arch", built.Pieces, lights);
        return (project, arch, lights);
    }

    [Fact]
    public void MovingAStructureTakesItsPiecesAndLightsButNothingElse()
    {
        var (project, arch, lights) = ArchWithLights();
        StructureEdits.MoveBy(project, arch.Id, new Vec3(2, 0, -1));

        Assert.Equal(new Vec3(2, 0, -1), StructureEdits.Centre(project, arch.Id));
        Assert.Equal([0, 2, 4], lights.Select(f => f.Position.X).ToArray());
        Assert.All(lights, f => Assert.Equal(-1, f.Position.Z, 6));
        Assert.Equal(new Vec3(0, 0, 0), project.Stage.Objects.Single(o => o.Name == "Riser").Position);
        Assert.Equal(new Vec3(9, 5, 9), project.FixtureById("free")!.Position);
    }

    [Fact]
    public void TurningSwingsLightsAroundTheCentreAndTurnsTheirAim()
    {
        var (project, arch, lights) = ArchWithLights();
        StructureEdits.Turn(project, arch.Id, 90);

        // Yaw 90 sends +X to -Z: the arch now runs front to back.
        Assert.Equal([2, 0, -2], lights.Select(f => f.Position.Z).ToArray());
        Assert.All(lights, f => Assert.Equal(0, f.Position.X, 6));
        Assert.All(lights, f => Assert.Equal(90, f.Yaw, 6));
        Assert.All(StructureEdits.Pieces(project, arch.Id), p => Assert.Equal(90, p.Yaw, 6));
        Assert.Equal(90, arch.Yaw);
        Assert.Equal(new Vec3(0, 0, 0), StructureEdits.Centre(project, arch.Id));
    }

    [Fact]
    public void UngroupLeavesEverythingInPlace()
    {
        var (project, arch, lights) = ArchWithLights();
        var before = project.Stage.Objects.Select(o => o.Position).ToList();
        StructureEdits.Ungroup(project, arch.Id);

        Assert.Empty(project.Stage.Structures);
        Assert.All(project.Stage.Objects, o => Assert.Null(o.StructureId));
        Assert.All(lights, f => Assert.Null(f.StructureId));
        Assert.Equal(before, project.Stage.Objects.Select(o => o.Position).ToList());
    }

    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 1)]
    public void DeleteRemovesThePiecesAndOptionallyTheLights(bool withFixtures, int fixturesLeft)
    {
        var (project, arch, _) = ArchWithLights();
        project.Groups.Add(new FixtureGroup { FixtureIds = { "l0", "free" } });
        project.Tracks.Add(new Track { FixtureIds = { "l1" } });
        StructureEdits.Delete(project, arch.Id, withFixtures);

        Assert.Equal("Riser", Assert.Single(project.Stage.Objects).Name);
        Assert.Empty(project.Stage.Structures);
        Assert.Equal(fixturesLeft, project.Fixtures.Count);
        Assert.All(project.Fixtures, f => Assert.Null(f.StructureId));
        if (withFixtures)
        {
            Assert.Equal(["free"], project.Groups[0].FixtureIds);
            Assert.Empty(project.Tracks[0].FixtureIds);
        }
    }
}
