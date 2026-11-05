using Lampion.Core.Model;
using Lampion.Core.Model.Rig;
using Lampion.Core.Persistence;

namespace Lampion.Core.Tests;

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
            Assert.Empty(PatchPlanner.Problems(project));
            Assert.All(project.Groups, g => Assert.NotEmpty(g.FixtureIds));
        }
    }
}
