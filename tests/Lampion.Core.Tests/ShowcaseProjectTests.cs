using Lampion.Core.Engine;
using Lampion.Core.Model;
using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Showcase;
using Lampion.Core.Model.Stage;
using Lampion.Core.Model.Timeline;
using Lampion.Core.Persistence;

namespace Lampion.Core.Tests;

public class ShowcaseProjectTests
{
    private static readonly Project Showcase = ShowcaseProject.Create();

    private static IEnumerable<LightClip> LightClips => Showcase.AllClips.OfType<LightClip>();

    [Fact]
    public void PatchIsCleanAndFitsTheRenderersLightBudget()
    {
        Assert.Empty(PatchPlanner.Problems(Showcase));
        Assert.Equal(48, Showcase.Fixtures.Count);
        Assert.All(Showcase.Groups, g => Assert.NotEmpty(g.FixtureIds));
        Assert.All(Showcase.Fixtures, f => Assert.Contains(Showcase.Groups, g => g.FixtureIds.Contains(f.Id)));
    }

    [Fact]
    public void ShowsOffEveryEffectTransitionAndBlendMode()
    {
        Assert.Equal(EffectCatalog.Entries.Select(e => e.Kind).Order(), LightClips.Select(c => c.Effect.Kind).Distinct().Order());
        Assert.Equal(Enum.GetValues<TransitionKind>(), LightClips.Where(c => c.TransitionIn is not null).Select(c => c.TransitionIn!.Kind).Distinct().Order());
        Assert.Equal(Enum.GetValues<BlendMode>(), Showcase.Tracks.Where(t => t.Kind == TrackKind.Light).Select(t => t.Blend).Distinct().Order());
        var targets = LightClips.SelectMany(c => c.Automation).Select(l => l.Target).ToHashSet();
        Assert.Subset(targets, new HashSet<AutomationTarget> { AutomationTarget.HueShift, AutomationTarget.Saturation, AutomationTarget.Opacity, AutomationTarget.White, AutomationTarget.Role });
    }

    [Fact]
    public void ClipsNeverOverlapOnATrackAndEndWithTheShow()
    {
        foreach (var track in Showcase.Tracks)
            for (var i = 0; i < track.Clips.Count; i++)
            {
                Assert.True(track.Clips[i].End <= Showcase.Duration + 1e-9, $"{track.Name}: {track.Clips[i].Name} runs past the end");
                if (i > 0)
                    Assert.True(track.Clips[i - 1].End <= track.Clips[i].Start + 1e-9, $"{track.Name}: {track.Clips[i - 1].Name} overlaps {track.Clips[i].Name}");
            }
        Assert.Equal(Showcase.Duration, Showcase.ContentEnd, 9);
    }

    [Fact]
    public void AutomationKeysStayInsideTheirClips()
    {
        foreach (var clip in LightClips)
            foreach (var lane in clip.Automation)
                Assert.All(lane.Keys, k => Assert.InRange(k.Time, 0, clip.Duration + 1e-9));
    }

    [Theory]
    [InlineData(6, 30)] // intro: wall, front wash, towers, searchlights
    [InlineData(11, 30)] // build
    [InlineData(18.1, 40)] // drop, right after a hit
    [InlineData(27, 25)] // breakdown
    [InlineData(34.1, 40)] // drop 2
    [InlineData(46.05, 12)] // the final hit
    public void EverySectionLightsTheRig(double bar, int atLeastLit)
    {
        var states = new ShowEvaluator(Showcase).Evaluate(Cue.Bars(bar));
        var lit = 0;
        foreach (var state in states)
            if (state.Light.R + state.Light.G + state.Light.B > 0.02)
                lit++;
        Assert.True(lit >= atLeastLit, $"Only {lit} fixtures lit at bar {bar}");
    }

    [Fact]
    public void GroundSupportMovesWithItsLights()
    {
        var project = ShowcaseProject.Create();
        var structure = project.Stage.Structures.Single(s => s.Name == "Ground support");
        var hung = StructureEdits.Fixtures(project, structure.Id).ToList();
        var before = hung.Select(f => f.Position).ToList();
        StructureEdits.MoveBy(project, structure.Id, new Vec3(0, 0, 1));
        Assert.Equal(30, hung.Count);
        Assert.All(hung.Zip(before), p => Assert.Equal(p.Second.Z + 1, p.First.Position.Z, 6));
    }

    [Fact]
    public void RoundTripsThroughAProjectFile()
    {
        var json = ProjectSerializer.Serialize(Showcase);
        Assert.Equal(json, ProjectSerializer.Serialize(ProjectSerializer.Deserialize(json)));
    }
}
