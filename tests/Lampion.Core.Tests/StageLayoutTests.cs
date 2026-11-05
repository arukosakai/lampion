using Lampion.Core.Model;
using Lampion.Core.Persistence;

namespace Lampion.Core.Tests;

public class StageLayoutTests
{
    [Fact]
    public void LayoutCarriesSetAndRigIntoAFreshProject()
    {
        var source = Project.CreateDemo();
        source.Stage.Haze = 0.33;
        var layout = StageLayout.Deserialize(StageLayout.Serialize(StageLayout.From(source)));

        var target = new Project();
        layout.ApplyTo(target, includeRig: true);

        Assert.Equal(source.Stage.Objects.Select(o => o.Name), target.Stage.Objects.Select(o => o.Name));
        Assert.Equal(0.33, target.Stage.Haze);
        Assert.Equal(source.Fixtures.Select(f => (f.Name, f.Universe, f.Address, f.Position)), target.Fixtures.Select(f => (f.Name, f.Universe, f.Address, f.Position)));
        Assert.All(target.Fixtures, f => Assert.NotNull(target.ProfileOf(f)));
        Assert.Equal(source.Groups.Count, target.Groups.Count);
    }

    [Fact]
    public void LayoutOnlyIncludesProfilesTheRigUses()
    {
        var layout = StageLayout.From(Project.CreateDemo());
        Assert.Equal([BuiltInProfiles.RgbwParId, BuiltInProfiles.MovingHeadId], layout.Profiles.Select(p => p.Id).Order().Reverse().ToArray());
    }

    [Fact]
    public void SetOnlyImportKeepsTheProjectsFixtures()
    {
        var target = Project.CreateDemo();
        var fixtures = target.Fixtures.Select(f => f.Id).ToList();
        var layout = new StageLayout { Stage = new StageSettings { Objects = [new StageObject { Name = "Riser" }] } };

        layout.ApplyTo(target, includeRig: false);

        Assert.Equal("Riser", Assert.Single(target.Stage.Objects).Name);
        Assert.Equal(fixtures, target.Fixtures.Select(f => f.Id));
    }

    [Fact]
    public void ReplacingTheRigDropsDanglingTrackTargets()
    {
        var target = Project.CreateDemo();
        new StageLayout().ApplyTo(target, includeRig: true);
        Assert.All(target.Tracks, t => Assert.Empty(t.GroupIds));
        Assert.All(target.Tracks, t => Assert.Empty(t.FixtureIds));
    }

    [Fact]
    public void LayoutIsADeepCopy()
    {
        var source = Project.CreateDemo();
        var layout = StageLayout.From(source);
        source.Stage.Objects[0].Name = "changed";
        source.Fixtures[0].Address = 400;
        Assert.NotEqual("changed", layout.Stage.Objects[0].Name);
        Assert.NotEqual(400, layout.Fixtures[0].Address);
    }
}

public class StageTemplateTests
{
    [Fact]
    public void BuiltInTemplatesAreReadyToUse()
    {
        foreach (var (layout, _) in StageTemplates.BuiltIn())
        {
            var project = new Project();
            layout.ApplyTo(project, includeRig: true);
            Assert.NotEmpty(project.Fixtures);
            Assert.Empty(project.PatchProblems());
            Assert.All(project.Groups, g => Assert.NotEmpty(g.FixtureIds));
        }
    }
}
