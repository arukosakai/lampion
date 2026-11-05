using Lampion.Core.Model;
using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Stage;
using Lampion.Core.Model.Timeline;

namespace Lampion.Core.Tests.Golden;

/// <summary>
/// A show with fixed ids that touches every effect, transition kind, blend mode, fixture order, automation
/// target and the patch edge cases (overlaps, past-512, missing profile, universe 0), for the golden tests.
/// </summary>
internal static class EverythingProject
{
    public static Project Create(bool solo = false)
    {
        var project = new Project
        {
            Name = "Everything",
            Duration = 36,
            Bpm = 128,
            BeatOffset = 0.25,
            Stage = new StageSettings
            {
                Haze = 0.4,
                Ambient = 0.2,
                LightBounce = true,
                GridStep = 0.5,
                Objects =
                {
                    new StageObject { Id = "so-deck", Name = "Deck", Kind = StageObjectKind.Platform, Position = new Vec3(0, 0.5, -1), Size = new Vec3(10, 1, 6), Color = "#2B2622" },
                    new StageObject { Id = "so-truss", Name = "Truss", Kind = StageObjectKind.Truss, Position = new Vec3(0, 6, 1), Size = new Vec3(12, 0.3, 0.3), Yaw = 15, Color = "#B8BCC4" },
                    new StageObject { Id = "so-screen", Name = "Screen", Kind = StageObjectKind.Screen, Position = new Vec3(0, 3, -4), Size = new Vec3(6, 3.4, 0.1), Color = "#050505" },
                    new StageObject { Id = "so-wall", Name = "Wall", Kind = StageObjectKind.Wall, Position = new Vec3(0, 4, -5), Size = new Vec3(12, 8, 0.2), Color = "#1E1E24" },
                },
            },
        };
        project.Profiles.AddRange(BuiltInProfiles.All().Select(p => p.Clone(newId: false)));
        project.Profiles.Add(
            new FixtureProfile
            {
                Id = "custom-odd",
                Manufacturer = "Acme",
                Model = "Odd Wash",
                Mode = "12ch",
                Beam = BeamKind.Wash,
                BeamAngle = 40,
                PanRange = 360,
                TiltRange = 180,
                Channels =
                {
                    new FixtureChannel { Name = "Zoom", Role = ChannelRole.Zoom, Default = 128 },
                    new FixtureChannel { Name = "Red", Role = ChannelRole.Red },
                    new FixtureChannel { Name = "Green", Role = ChannelRole.Green },
                    new FixtureChannel { Name = "Blue", Role = ChannelRole.Blue },
                    new FixtureChannel { Name = "Amber", Role = ChannelRole.Amber },
                    new FixtureChannel { Name = "UV", Role = ChannelRole.UV },
                    new FixtureChannel { Name = "Macro", Role = ChannelRole.Generic, Default = 10 },
                    new FixtureChannel { Name = "Wheel", Role = ChannelRole.ColorWheel },
                    new FixtureChannel { Name = "Gobo", Role = ChannelRole.Gobo, Default = 5 },
                    new FixtureChannel { Name = "FX", Role = ChannelRole.Effect },
                    new FixtureChannel { Name = "Tilt", Role = ChannelRole.Tilt, Default = 128 },
                    new FixtureChannel { Name = "Speed", Role = ChannelRole.PanTiltSpeed },
                },
            }
        );
        project.Profiles.Add(
            new FixtureProfile
            {
                Id = "custom-white",
                Model = "White Blinder",
                Beam = BeamKind.Blinder,
                Channels = { new FixtureChannel { Name = "White", Role = ChannelRole.White }, new FixtureChannel { Name = "Strobe", Role = ChannelRole.Strobe, Default = 0 } },
            }
        );

        Fixture Add(string id, string profile, int universe, int address, double x, double y = 5.5, double z = 1, bool extractWhite = true)
        {
            var fixture = new Fixture
            {
                Id = id,
                Name = id.ToUpperInvariant(),
                ProfileId = profile,
                Universe = universe,
                Address = address,
                Position = new Vec3(x, y, z),
                Yaw = x * 3,
                Pitch = -50 - x,
                ExtractWhite = extractWhite,
                GelColor = "#FFD080",
            };
            project.Fixtures.Add(fixture);
            return fixture;
        }

        Add("par1", BuiltInProfiles.RgbwParId, 1, 1, -3);
        Add("par2", BuiltInProfiles.RgbwParId, 1, 5, -1);
        Add("par3", BuiltInProfiles.RgbwParId, 1, 9, 1, extractWhite: false);
        Add("par4", BuiltInProfiles.RgbwParId, 1, 13, 3);
        Add("head1", BuiltInProfiles.MovingHeadId, 1, 20, -4, 6, -2);
        Add("head2", BuiltInProfiles.MovingHeadId, 1, 32, 0, 6, -2);
        Add("head3", BuiltInProfiles.MovingHeadId, 1, 44, 4, 6, -2);
        Add("hex1", BuiltInProfiles.HexParId, 1, 60, -2, 0.5, 2);
        Add("hex2", BuiltInProfiles.HexParId, 1, 68, 2, 0.5, 2);
        Add("overlap", BuiltInProfiles.RgbParId, 1, 61, 0.5, 0.5, 2);
        Add("dimrgb1", BuiltInProfiles.DimRgbParId, 1, 80, 5);
        Add("strobe1", BuiltInProfiles.StrobeId, 1, 90, 0, 7);
        Add("dimmer1", BuiltInProfiles.DimmerId, 1, 95, -6);
        Add("dimmer2", BuiltInProfiles.DimmerId, 1, 96, -6);
        Add("custom1", "custom-odd", 1, 100, 6, 4);
        Add("bar1", BuiltInProfiles.LedBarId, 2, 1, -1.5, 0.2, 3);
        Add("bar2", BuiltInProfiles.LedBarId, 2, 4, 1.5, 0.2, 3);
        Add("rgb1", BuiltInProfiles.RgbParId, 2, 7, 7);
        Add("edge", BuiltInProfiles.MovingHeadId, 2, 505, -7);
        Add("noprofile", "nope", 3, 1, 0);
        Add("custom2", "custom-odd", 3, 10, -5);
        Add("white1", "custom-white", 3, 30, 2.5);
        Add("zero", BuiltInProfiles.RgbParId, 0, 1, 8);

        project.Groups.AddRange(
        [
            new FixtureGroup { Id = "g-pars", Name = "Pars", Color = "#E6A23A", FixtureIds = { "par1", "par2", "par3", "par4", "ghost" } },
            new FixtureGroup { Id = "g-heads", Name = "Heads", Color = "#3AA0E6", FixtureIds = { "head1", "head2", "head3" } },
            new FixtureGroup { Id = "g-mixed", Name = "Mixed", Color = "#A03AE6", FixtureIds = { "hex1", "hex2", "dimrgb1", "custom1", "bar1", "bar2", "rgb1", "edge", "zero", "overlap" } },
            new FixtureGroup { Id = "g-dim", Name = "Dimmers", Color = "#E63A5A", FixtureIds = { "dimmer1", "dimmer2", "strobe1", "white1", "custom2", "noprofile" } },
            new FixtureGroup { Id = "g-front", Name = "Front two", Color = "#3AE6A0", FixtureIds = { "par2", "par1" } },
        ]);

        static Keyframe K(double time, double value, Easing easing = Easing.Linear) => new() { Time = time, Value = value, Easing = easing };

        var top = new Track
        {
            Id = "t-top",
            Name = "Top HTP",
            Blend = BlendMode.Htp,
            Order = FixtureOrder.CenterOut,
            FixtureIds = { "par3" },
            GroupIds = { "g-front", "g-pars" },
        };
        top.Clips.Add(new LightClip { Id = "c-strobe", Name = "Strobe", Start = 0, Duration = 6, Effect = new StrobeEffect { Color = "#FFEEDD", Rate = 5, Duty = 0.4, Intensity = 0.8 } });
        top.Clips.Add(
            new LightClip
            {
                Id = "c-sparkle",
                Name = "Sparkle",
                Start = 6,
                Duration = 8,
                FadeIn = 1,
                FadeOut = 2,
                FadeEasing = Easing.EaseInOut,
                Effect = new SparkleEffect { Color = "#AACCFF", Rate = 3, Decay = 0.25, Seed = 42 },
            }
        );
        top.Clips.Add(
            new LightClip
            {
                Id = "c-lightning-s",
                Name = "Lightning scattered",
                Start = 14,
                Duration = 8,
                TransitionIn = new Transition { Kind = TransitionKind.Wipe, Duration = 2, Easing = Easing.Linear },
                Effect = new LightningEffect { Color = "#E8F0FF", Rate = 1.5, Decay = 0.2, Mode = LightningMode.Scattered, Seed = 5 },
            }
        );
        top.Clips.Add(
            new LightClip
            {
                Id = "c-lightning-t",
                Name = "Lightning together",
                Start = 22,
                Duration = 8,
                TransitionIn = new Transition { Kind = TransitionKind.DipToBlack, Duration = 3, Easing = Easing.EaseIn },
                Effect = new LightningEffect { Color = "#FFFFFF", Rate = 1, Mode = LightningMode.Together },
            }
        );

        var adder = new Track
        {
            Id = "t-add",
            Name = "Adder",
            Blend = BlendMode.Add,
            Order = FixtureOrder.RightToLeft,
            GroupIds = { "g-mixed" },
            Solo = solo,
        };
        adder.Clips.Add(new LightClip { Id = "c-fire", Name = "Fire", Start = 0, Duration = 10, Effect = new FireEffect { Rate = 6, Minimum = 0.2, Seed = 9 } });
        adder.Clips.Add(
            new LightClip
            {
                Id = "c-hit",
                Name = "Hit",
                Start = 5,
                Duration = 4,
                FadeIn = 0.5,
                FadeEasing = Easing.EaseOut,
                Effect = new HitEffect { Color = "#FF00FF", Decay = 0.3, Interval = 0.5, Offset = 0.1 },
            }
        );
        adder.Clips.Add(
            new LightClip
            {
                Id = "c-gradient",
                Name = "Gradient",
                Start = 10,
                Duration = 10,
                TransitionIn = new Transition { Kind = TransitionKind.Crossfade, Duration = 2, Easing = Easing.Linear },
                Effect = new GradientEffect { Colors = ["#FF2A6D", "#05D9E8", "#F9F871"], Repeat = 2, Speed = 0.3 },
            }
        );
        adder.Clips.Add(
            new LightClip
            {
                Id = "c-wave",
                Name = "Wave",
                Start = 20,
                Duration = 10,
                TransitionIn = new Transition { Kind = TransitionKind.Crossfade, Duration = 1.5, Easing = Easing.EaseOut },
                Effect = new ColorWaveEffect { Color = "#00E5FF", Base = "#3A00FF", Rate = 0.7, Wavelength = 3, Wave = Waveform.Triangle },
                Automation =
                {
                    new AutomationLane { Id = "a-hue", Target = AutomationTarget.HueShift, Keys = { K(0, 0, Easing.EaseInOut), K(10, 2.5) } },
                    new AutomationLane { Id = "a-sat", Target = AutomationTarget.Saturation, Keys = { K(0, 1, Easing.EaseIn), K(5, 0.2, Easing.EaseOut), K(10, 1) } },
                    new AutomationLane { Id = "a-gobo", Target = AutomationTarget.Role, Role = ChannelRole.Gobo, Keys = { K(0, 0.1), K(10, 0.9) } },
                    new AutomationLane { Id = "a-wheel", Target = AutomationTarget.Role, Role = ChannelRole.ColorWheel, Animated = false, Keys = { K(0, 0.33), K(5, 0.9) } },
                },
            }
        );
        adder.Clips.Add(new LightClip { Id = "c-heart", Name = "Heartbeat", Start = 30, Duration = 8, Effect = new HeartbeatEffect { Rate = 1.4, Phase = 0.15, Minimum = 0.1 } });
        adder.Clips.Add(new LightClip { Id = "c-empty-cycle", Name = "Empty cycle", Start = 31, Duration = 3, Effect = new ColorCycleEffect { Colors = [] } });

        var heads = new Track
        {
            Id = "t-heads",
            Name = "Heads",
            Blend = BlendMode.Normal,
            Order = FixtureOrder.LeftToRight,
            GroupIds = { "g-heads" },
        };
        heads.Clips.Add(
            new LightClip
            {
                Id = "c-chase-bounce",
                Name = "Chase bounce",
                Start = 0,
                Duration = 12,
                FadeOut = 1,
                Effect = new ChaseEffect { Colors = ["#FF0000", "#00FF00", "#0000FF"], StepTime = 0.3, Tail = 2, Direction = ChaseDirection.Bounce, Background = 0.05 },
                Automation =
                {
                    new AutomationLane
                    {
                        Id = "a-pan",
                        Target = AutomationTarget.Role,
                        Role = ChannelRole.Pan,
                        Keys = { K(0, 0.1, Easing.Linear), K(2, 0.9, Easing.Hold), K(4, 0.3, Easing.EaseIn), K(6, 0.7, Easing.EaseOut), K(8, 0.2, Easing.EaseInOut), K(12, 0.6) },
                    },
                    new AutomationLane { Id = "a-tilt", Target = AutomationTarget.Role, Role = ChannelRole.Tilt, Keys = { K(0, 0.2), K(12, 0.8) } },
                    new AutomationLane { Id = "a-panfine", Target = AutomationTarget.Role, Role = ChannelRole.PanFine, Animated = false, Keys = { K(0, 0.4) } },
                    new AutomationLane { Id = "a-zoom", Target = AutomationTarget.Role, Role = ChannelRole.Zoom, Keys = { K(0, 0), K(12, 1) } },
                    new AutomationLane { Id = "a-ch5", Target = AutomationTarget.Channel, Channel = 5, Keys = { K(0, 1), K(12, 0) } },
                    new AutomationLane { Id = "a-ch99", Target = AutomationTarget.Channel, Channel = 99, Keys = { K(0, 1) } },
                    new AutomationLane { Id = "a-ch0", Target = AutomationTarget.Channel, Channel = 0, Keys = { K(0, 0.5) } },
                    new AutomationLane { Id = "a-strobe-off", Target = AutomationTarget.Role, Role = ChannelRole.Strobe, Enabled = false, Keys = { K(0, 1) } },
                },
            }
        );
        heads.Clips.Add(
            new LightClip
            {
                Id = "c-chase-back",
                Name = "Chase back",
                Start = 12,
                Duration = 12,
                TransitionIn = new Transition { Kind = TransitionKind.Crossfade, Duration = 2, Easing = Easing.EaseInOut },
                Effect = new ChaseEffect { Colors = ["#FFFFFF", "#FF8800"], StepTime = 0.4, Tail = 0, Direction = ChaseDirection.Backward },
                Automation =
                {
                    new AutomationLane { Id = "a-pan2", Target = AutomationTarget.Role, Role = ChannelRole.Pan, Keys = { K(0, 0.5), K(12, 0.1) } },
                    new AutomationLane { Id = "a-strobe", Target = AutomationTarget.Role, Role = ChannelRole.Strobe, Keys = { K(0, 0.2), K(6, 0.6, Easing.Hold), K(12, 0) } },
                },
            }
        );
        heads.Clips.Add(
            new LightClip
            {
                Id = "c-cycle",
                Name = "Cycle",
                Start = 24,
                Duration = 10,
                TransitionIn = new Transition { Kind = TransitionKind.Crossfade, Duration = 1, Easing = Easing.Hold },
                Effect = new ColorCycleEffect { Colors = ["#FF0000", "#FFFF00", "#00FFFF", "#FF00FF"], StepTime = 0.75, FadeRatio = 0.3, FixtureOffset = 0.5 },
            }
        );

        var washes = new Track
        {
            Id = "t-washes",
            Name = "Washes",
            Blend = BlendMode.Normal,
            Order = FixtureOrder.OutsideIn,
            FixtureIds = { "hex1" },
            GroupIds = { "g-pars", "g-dim" },
            Solo = solo,
        };
        washes.Clips.Add(
            new LightClip
            {
                Id = "c-solid",
                Name = "Solid",
                Start = 0,
                Duration = 5,
                FadeIn = 1.5,
                Effect = new SolidEffect { Color = "#FF8C3A", Intensity = 0.9 },
                Automation =
                {
                    new AutomationLane { Id = "a-white", Target = AutomationTarget.White, Keys = { K(0, 0), K(5, 0.8) } },
                    new AutomationLane { Id = "a-red", Target = AutomationTarget.Red, Keys = { K(0, 0.2), K(5, 1) } },
                    new AutomationLane { Id = "a-green", Target = AutomationTarget.Green, Keys = { K(1, 0.5) } },
                    new AutomationLane { Id = "a-blue", Target = AutomationTarget.Blue, Animated = false, Keys = { K(0, 0.3), K(5, 1) } },
                    new AutomationLane { Id = "a-none", Target = AutomationTarget.Intensity },
                    new AutomationLane { Id = "a-dimmer", Target = AutomationTarget.Role, Role = ChannelRole.Dimmer, Keys = { K(2, 0.25), K(4, 0.75) } },
                },
            }
        );
        washes.Clips.Add(
            new LightClip
            {
                Id = "c-fade",
                Name = "Fade",
                Start = 5,
                Duration = 10,
                EffectOffset = 1,
                EffectSpan = 12,
                Effect = new ColorFadeEffect { From = "#00FF00", To = "#FF00FF", Offset = 0.3, Easing = Easing.EaseIn },
            }
        );
        washes.Clips.Add(
            new LightClip
            {
                Id = "c-rainbow",
                Name = "Rainbow",
                Start = 15,
                Duration = 10,
                FadeOut = 3,
                FadeEasing = Easing.EaseIn,
                Effect = new RainbowEffect { Speed = 0.3, Spread = 0.8, Saturation = 0.7, StartHue = 0.1 },
                Automation =
                {
                    new AutomationLane { Id = "a-int", Target = AutomationTarget.Intensity, Keys = { K(0, 1), K(5, 0.3, Easing.EaseInOut), K(10, 1.4) } },
                    new AutomationLane { Id = "a-op", Target = AutomationTarget.Opacity, Keys = { K(0, 1), K(10, 0.4) } },
                },
            }
        );
        washes.Clips.Add(
            new LightClip
            {
                Id = "c-pulse",
                Name = "Pulse",
                Start = 25,
                Duration = 10,
                Effect = new PulseEffect { Color = "#33FF99", Rate = 1.5, Minimum = 0.2, Wave = Waveform.Square, Phase = 0.2 },
            }
        );
        washes.Clips.Add(
            new LightClip
            {
                Id = "c-pulse-saw",
                Name = "Pulse saw",
                Start = 35,
                Duration = 5,
                TransitionIn = new Transition { Kind = TransitionKind.Wipe, Duration = 1, Easing = Easing.EaseOut },
                Effect = new PulseEffect { Color = "#9933FF", Rate = 2, Wave = Waveform.Saw },
            }
        );

        var muted = new Track { Id = "t-muted", Name = "Muted", Muted = true, FixtureIds = { "par1" } };
        muted.Clips.Add(new LightClip { Id = "c-muted", Name = "Muted red", Start = 0, Duration = 40, Effect = new SolidEffect { Color = "#FF0000" } });

        var bottom = new Track
        {
            Id = "t-bottom",
            Name = "Bottom",
            Order = FixtureOrder.AsListed,
            Locked = true,
            Expanded = true,
            FixtureIds = { "dimmer2", "dimmer1", "strobe1", "white1", "custom2", "noprofile", "ghost", "dimmer2", "custom1", "rgb1" },
        };
        bottom.Clips.Add(
            new LightClip
            {
                Id = "c-base",
                Name = "Base",
                Start = 0,
                Duration = 40,
                FadeIn = 3,
                FadeEasing = Easing.EaseOut,
                Effect = new SolidEffect { Color = "#405060", Intensity = 0.6 },
            }
        );
        bottom.Clips.Add(new LightClip { Id = "c-empty-chase", Name = "Empty chase", Start = 8, Duration = 6, FadeIn = 1, FadeOut = 1, Effect = new ChaseEffect { Colors = [], StepTime = 0.5 } });
        bottom.Clips.Add(new LightClip { Id = "c-forward", Name = "Forward chase", Start = 20, Duration = 8, Effect = new ChaseEffect { Colors = ["#FFAA00"], StepTime = 0.2, Tail = 3 } });

        var empty = new Track { Id = "t-empty", Name = "No targets" };
        empty.Clips.Add(new LightClip { Id = "c-orphan", Name = "Orphan", Start = 0, Duration = 10 });

        var audio = new Track { Id = "t-audio", Name = "Audio", Kind = TrackKind.Audio };
        audio.Clips.Add(new MediaClip { Id = "m-song", Name = "Song", Start = 0, Duration = 38, Path = "media/song.mp3", MediaOffset = 0.5, SourceDuration = 200, Volume = 0.8 });
        var video = new Track { Id = "t-video", Name = "Video", Kind = TrackKind.Video, Muted = true };
        video.Clips.Add(new MediaClip { Id = "m-clip", Name = "Clip", Start = 4, Duration = 20, Path = "media/loop.mp4" });

        project.Tracks.AddRange([top, adder, heads, washes, muted, bottom, empty, audio, video]);
        project.Markers.Add(new Marker { Id = "mk-drop", Time = 14, Label = "Drop" });
        project.Markers.Add(new Marker { Id = "mk-end", Time = 30, Label = "Outro" });
        return project;
    }

    /// <summary>Sample times across the show, including the cuts where transitions are centred.</summary>
    public static double[] SampleTimes(double end)
    {
        var times = new List<double>();
        for (var k = 0; k <= 48; k++)
            times.Add(Math.Round(end * k / 48, 6));
        times.AddRange([0.333, 5.5, 9.99, 11.0, 12.0, 13.999, 14.0, 14.0005, 15.2, 20.0, 20.6, 22.0, 23.1, 24.0, 24.4, 31.5, 35.0, 35.4, -1, end + 5]);
        return [.. times];
    }
}
