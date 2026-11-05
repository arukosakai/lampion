using System.Globalization;
using System.Text;
using Lampion.Core.Engine;
using Lampion.Core.Model;

namespace Lampion.Core.Tests.Golden;

/// <summary>The DMX the show produces is the contract; these pin it byte for byte.</summary>
public class DmxGoldenTests
{
    [Fact]
    public void DemoProjectRendersTheGoldenDmx() => GoldenFile.AssertMatches("demo.dmx.txt", Dump(DemoProject.Create()));

    [Fact]
    public void EverythingProjectRendersTheGoldenDmx() => GoldenFile.AssertMatches("everything.dmx.txt", Dump(EverythingProject.Create()));

    [Fact]
    public void SoloedEverythingProjectRendersTheGoldenDmx() => GoldenFile.AssertMatches("everything-solo.dmx.txt", Dump(EverythingProject.Create(solo: true)));

    [Fact]
    public void RenderingIsRepeatable()
    {
        // Random-looking effects (sparkle, fire, lightning) hash time and fixture, so two runs must agree.
        var project = EverythingProject.Create();
        Assert.Equal(Dump(project), Dump(project));
    }

    private static string Dump(Project project)
    {
        var text = new StringBuilder();
        var renderer = new ShowRenderer(project);
        foreach (var time in EverythingProject.SampleTimes(project.ContentEnd))
        {
            text.Append("t ").Append(time.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            AppendFrame(text, renderer, time);
            AppendFixtures(text, renderer, project, time);
        }
        return text.ToString();
    }

    private static void AppendFrame(StringBuilder text, ShowRenderer renderer, double time)
    {
        foreach (var (universe, data) in renderer.Render(time))
            text.Append("u").Append(universe.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(Convert.ToHexString(data)).Append('\n');
    }

    private static void AppendFixtures(StringBuilder text, ShowRenderer renderer, Project project, double time)
    {
        var fixtures = renderer.RenderFixtures(time);
        for (var i = 0; i < project.Fixtures.Count; i++)
            if (fixtures.HasChannels(i))
                text.Append("f ").Append(project.Fixtures[i].Name).Append(' ').Append(Convert.ToHexString(fixtures[i])).Append('\n');
    }
}
