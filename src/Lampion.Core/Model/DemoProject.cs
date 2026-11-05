using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Stage;
using Lampion.Core.Model.Timeline;

namespace Lampion.Core.Model;

/// <summary>The show new users start with: six RGBW pars and four moving heads running a short warm-up, rainbow and chase.</summary>
public static class DemoProject
{
    /// <summary>A fresh demo show (new random ids every call), 30 s long, with every built-in profile available.</summary>
    public static Project Create()
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

        var wash = new Track { Name = "Front wash", GroupIds = { front.Id }, Order = FixtureOrder.LeftToRight };
        wash.Clips.Add(new LightClip { Name = "Warm open", Start = 0, Duration = 8, FadeIn = 2, Effect = new SolidEffect { Color = "#FF8C3A" } });
        wash.Clips.Add(
            new LightClip
            {
                Name = "Rainbow",
                Start = 8,
                Duration = 12,
                Effect = new RainbowEffect { Speed = 0.2, Spread = 1 },
                TransitionIn = new Transition { Duration = 2 },
            }
        );
        var beams = new Track { Name = "Heads", GroupIds = { heads.Id }, Order = FixtureOrder.LeftToRight };
        beams.Clips.Add(
            new LightClip
            {
                Name = "Chase",
                Start = 4,
                Duration = 16,
                FadeIn = 1,
                FadeOut = 1,
                Effect = new ChaseEffect { Colors = ["#3AA0FF", "#FF3A9A"], StepTime = 0.5, Tail = 1.5, Background = 0.1 },
                Automation =
                {
                    new AutomationLane
                    {
                        Target = AutomationTarget.Role,
                        Role = ChannelRole.Pan,
                        Keys =
                        {
                            new Keyframe { Time = 0, Value = 0.4, Easing = Easing.EaseInOut },
                            new Keyframe { Time = 8, Value = 0.6, Easing = Easing.EaseInOut },
                            new Keyframe { Time = 16, Value = 0.4 },
                        },
                    },
                },
            }
        );
        project.Tracks.AddRange([beams, wash, new Track { Name = "Audio", Kind = TrackKind.Audio }, new Track { Name = "Video", Kind = TrackKind.Video }]);
        project.Duration = 30;
        return project;
    }
}
