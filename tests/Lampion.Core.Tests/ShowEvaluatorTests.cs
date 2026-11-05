using Lampion.Core.Color;
using Lampion.Core.Engine;
using Lampion.Core.Model;

namespace Lampion.Core.Tests;

public class ShowEvaluatorTests
{
    private static (Project Project, Track Track, Fixture[] Fixtures) Setup(int fixtureCount = 1)
    {
        var project = new Project();
        project.Profiles.AddRange(BuiltInProfiles.All().Select(p => p.Clone(newId: false)));
        var fixtures = Enumerable
            .Range(0, fixtureCount)
            .Select(i => new Fixture { Name = $"F{i}", ProfileId = BuiltInProfiles.RgbParId, Address = 1 + i * 3, Position = new Vec3(i, 4, 0) })
            .ToArray();
        project.Fixtures.AddRange(fixtures);
        var track = new Track { FixtureIds = fixtures.Select(f => f.Id).ToList() };
        project.Tracks.Add(track);
        return (project, track, fixtures);
    }

    private static Rgb LightAt(Project project, Fixture fixture, double time) => new ShowEvaluator(project).Evaluate(time)[fixture.Id].Light;

    private static void AssertColor(Rgb expected, Rgb actual, int precision = 3)
    {
        Assert.Equal(expected.R, actual.R, precision);
        Assert.Equal(expected.G, actual.G, precision);
        Assert.Equal(expected.B, actual.B, precision);
    }

    [Fact]
    public void SolidClipLightsItsFixturesOnlyWhileItRuns()
    {
        var (project, track, f) = Setup();
        track.Clips.Add(new LightClip { Start = 1, Duration = 2, Effect = new SolidEffect { Color = "#FF0000" } });
        AssertColor(Rgb.Black, LightAt(project, f[0], 0.5));
        AssertColor(new Rgb(1, 0, 0), LightAt(project, f[0], 2));
        AssertColor(Rgb.Black, LightAt(project, f[0], 3.01));
    }

    [Fact]
    public void FadeInRampsFromBlack()
    {
        var (project, track, f) = Setup();
        track.Clips.Add(new LightClip { Start = 0, Duration = 4, FadeIn = 2, Effect = new SolidEffect { Color = "#FFFFFF" } });
        AssertColor(new Rgb(0.5, 0.5, 0.5), LightAt(project, f[0], 1));
    }

    [Fact]
    public void HueAutomationTurnsRedIntoGreen()
    {
        var (project, track, f) = Setup();
        track.Clips.Add(
            new LightClip
            {
                Duration = 10,
                Effect = new SolidEffect { Color = "#FF0000" },
                Automation = [new AutomationLane { Target = AutomationTarget.HueShift, Keys = [new Keyframe { Time = 0, Value = 0 }, new Keyframe { Time = 3, Value = 1 }] }],
            }
        );
        AssertColor(new Rgb(0, 1, 0), LightAt(project, f[0], 1));
        AssertColor(new Rgb(0, 0, 1), LightAt(project, f[0], 2));
    }

    [Fact]
    public void CrossfadeIsHalfwayAtTheCut()
    {
        var (project, track, f) = Setup();
        track.Clips.Add(new LightClip { Start = 0, Duration = 4, Effect = new SolidEffect { Color = "#FF0000" } });
        track.Clips.Add(
            new LightClip
            {
                Start = 4,
                Duration = 4,
                Effect = new SolidEffect { Color = "#0000FF" },
                TransitionIn = new Transition { Duration = 2, Easing = Easing.Linear },
            }
        );
        AssertColor(new Rgb(1, 0, 0), LightAt(project, f[0], 2.9));
        AssertColor(new Rgb(0.5, 0, 0.5), LightAt(project, f[0], 4));
        AssertColor(new Rgb(0, 0, 1), LightAt(project, f[0], 5.1));
    }

    [Fact]
    public void UpperTrackCoversLowerOneAndMutingRevealsIt()
    {
        var (project, lower, f) = Setup();
        lower.Clips.Add(new LightClip { Duration = 10, Effect = new SolidEffect { Color = "#0000FF" } });
        var upper = new Track { FixtureIds = [f[0].Id] };
        upper.Clips.Add(new LightClip { Duration = 10, Effect = new SolidEffect { Color = "#FF0000" } });
        project.Tracks.Insert(0, upper);

        AssertColor(new Rgb(1, 0, 0), LightAt(project, f[0], 1));
        upper.Muted = true;
        AssertColor(new Rgb(0, 0, 1), LightAt(project, f[0], 1));
        upper.Muted = false;
        upper.Blend = BlendMode.Htp;
        AssertColor(new Rgb(1, 0, 1), LightAt(project, f[0], 1));
    }

    [Fact]
    public void ChaseLightsOneFixtureAtATime()
    {
        var (project, track, f) = Setup(4);
        track.Clips.Add(new LightClip { Duration = 10, Effect = new ChaseEffect { StepTime = 1, Tail = 0 } });
        var states = new ShowEvaluator(project).Evaluate(2.5);
        Assert.Equal([0, 0, 1, 0], f.Select(x => states[x.Id].Light.R).ToArray());
    }

    [Fact]
    public void RainbowSpreadsHueAcrossFixtures()
    {
        var (project, track, f) = Setup(3);
        track.Clips.Add(new LightClip { Duration = 10, Effect = new RainbowEffect { Speed = 0, Spread = 1 } });
        var states = new ShowEvaluator(project).Evaluate(0);
        AssertColor(new Rgb(1, 0, 0), states[f[0].Id].Light);
        AssertColor(new Rgb(0, 1, 0), states[f[1].Id].Light);
        AssertColor(new Rgb(0, 0, 1), states[f[2].Id].Light);
    }

    [Fact]
    public void RendererWritesFixturesAtTheirAddress()
    {
        var (project, track, f) = Setup(2);
        track.FixtureIds = [f[1].Id];
        track.Clips.Add(new LightClip { Duration = 10, Effect = new SolidEffect { Color = "#00FF00" } });
        var universe = new ShowRenderer(project).Render(1).Universe(1);
        Assert.Equal([0, 0, 0, 0, 255, 0], universe[..6]);
    }
}
