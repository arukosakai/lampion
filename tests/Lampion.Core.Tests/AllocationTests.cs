using System.Diagnostics;
using Lampion.Core.Color;
using Lampion.Core.Engine;
using Lampion.Core.Live;
using Lampion.Core.Midi;
using Lampion.Core.Model;
using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Live;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Showcase;
using Lampion.Core.Model.Stage;
using Lampion.Core.Output;
using Lampion.Core.Tests.Golden;

namespace Lampion.Core.Tests;

/// <summary>The per-frame path must not allocate once warmed up (see CLAUDE.md, "The hot path").</summary>
public class AllocationTests
{
    private const int Frames = 240;

    public static TheoryData<string> Projects => ["demo", "showcase", "everything", "everything-solo"];

    private static Project Create(string name) =>
        name switch
        {
            "demo" => DemoProject.Create(),
            "showcase" => ShowcaseProject.Create(),
            "everything-solo" => EverythingProject.Create(solo: true),
            _ => EverythingProject.Create(),
        };

    /// <summary>Bytes allocated on this thread by <paramref name="frame"/> over <see cref="Frames"/> calls, after the same calls as a warm-up.</summary>
    private static long Allocated(Action<int> frame)
    {
        for (var i = 0; i < Frames; i++)
            frame(i);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Frames; i++)
            frame(i);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    /// <summary>Times spread over the whole show and a little beyond, so every clip, fade and transition is hit.</summary>
    private static double TimeAt(Project project, int frame) => -0.5 + (project.ContentEnd + 1) * frame / (Frames - 1.0);

    [Theory]
    [MemberData(nameof(Projects))]
    public void EvaluatorDoesNotAllocate(string name)
    {
        var project = Create(name);
        var evaluator = new ShowEvaluator(project);
        Assert.Equal(0, Allocated(i => evaluator.Evaluate(TimeAt(project, i))));
    }

    [Theory]
    [MemberData(nameof(Projects))]
    public void EvaluatorIntoCallerBufferDoesNotAllocate(string name)
    {
        var project = Create(name);
        var evaluator = new ShowEvaluator(project);
        var states = new FixtureState[project.Fixtures.Count];
        Assert.Equal(0, Allocated(i => evaluator.Evaluate(TimeAt(project, i), states)));
    }

    [Theory]
    [MemberData(nameof(Projects))]
    public void RendererDoesNotAllocate(string name)
    {
        var project = Create(name);
        var renderer = new ShowRenderer(project);
        Assert.Equal(0, Allocated(i => renderer.Render(TimeAt(project, i))));
    }

    [Theory]
    [MemberData(nameof(Projects))]
    public void RenderingFixturesDoesNotAllocate(string name)
    {
        var project = Create(name);
        var renderer = new ShowRenderer(project);
        Assert.Equal(0, Allocated(i => renderer.RenderFixtures(TimeAt(project, i))));
    }

    [Theory]
    [MemberData(nameof(Projects))]
    public void ReadingAndForwardingAFrameDoesNotAllocate(string name)
    {
        var project = Create(name);
        var renderer = new ShowRenderer(project);
        var copy = new DmxFrame();
        var packet = new byte[ArtDmxPacket.Length];
        Assert.Equal(
            0,
            Allocated(i =>
            {
                var frame = renderer.Render(TimeAt(project, i));
                frame.CopyTo(copy);
                foreach (var (universe, channels) in copy)
                    ArtDmxPacket.Write(packet, universe - 1, channels, (byte)(i % 255 + 1));
                foreach (var fixture in project.Fixtures)
                    if (project.ProfileOf(fixture) is { } profile)
                        DmxMapper.Decode(fixture, profile, copy.Slice(fixture, profile.Channels.Count));
            })
        );
    }

    [Fact]
    public void MapperDoesNotAllocate()
    {
        var profiles = BuiltInProfiles.All();
        var fixture = new Fixture();
        var values = new byte[64];
        var state = new FixtureState { Light = new Rgb(0.8, 0.4, 0.2), White = 0.3 };
        state.SetRole(ChannelRole.Pan, 0.25);
        state.SetRole(ChannelRole.Strobe, 0.5);
        state.SetRaw(2, 0.75);
        Assert.Equal(
            0,
            Allocated(i =>
            {
                var profile = profiles[i % profiles.Count];
                DmxMapper.Map(fixture, profile, in state, values);
                DmxMapper.Decode(fixture, profile, values.AsSpan(0, profile.Channels.Count));
            })
        );
    }

    [Fact]
    public void EffectsDoNotAllocate()
    {
        var effects = EffectCatalog.Entries.Select(e => EffectCatalog.Create(e.Kind)).ToList();
        effects.Add(new ChaseEffect { Colors = [], Direction = ChaseDirection.Bounce });
        effects.Add(new ColorCycleEffect { Colors = [] });
        effects.Add(new GradientEffect { Colors = [] });
        effects.Add(new LightningEffect { Mode = LightningMode.Scattered });
        effects.Add(new PulseEffect { Wave = Waveform.Triangle });
        effects.Add(new SolidEffect { Color = " #abc " });
        Assert.Equal(
            0,
            Allocated(i =>
            {
                foreach (var effect in effects)
                    for (var fixture = 0; fixture < 6; fixture++)
                        effect.Sample(new EffectContext(i * 0.093, 12, fixture, 6));
            })
        );
    }

    [Fact]
    public void AutomationAndColourMathDoNotAllocate()
    {
        var lane = new AutomationLane
        {
            Keys = { new Keyframe { Time = 0, Value = 0, Easing = Easing.EaseIn }, new Keyframe { Time = 2, Value = 1, Easing = Easing.Hold }, new Keyframe { Time = 5, Value = 0.3 } },
        };
        Assert.Equal(
            0,
            Allocated(i =>
            {
                lane.Evaluate(i * 0.03);
                var (h, s, v) = Rgb.FromHsv(i * 0.01, 0.8, 0.9).ToHsv();
                Rgb.FromHsv(h + 0.5, s, v);
                Rgb.FromHex("#FF8800");
                Rgb.TryParseHex("#f80", out _);
                Rgb.TryParseHex("not a colour", out _);
                EasingCurve.Apply(Easing.EaseInOut, i / (double)Frames);
            })
        );
    }

    [Fact]
    public void ProjectLookupsAndStageRaysDoNotAllocate()
    {
        var project = EverythingProject.Create();
        var objects = project.Stage.Objects;
        Assert.Equal(
            0,
            Allocated(i =>
            {
                var fixture = project.Fixtures[i % project.Fixtures.Count];
                project.ProfileOf(fixture);
                project.FixtureById(fixture.Id);
                _ = project.ContentEnd;
                StageMath.Raycast(objects, fixture.Position, StageMath.Direction(fixture.Yaw, fixture.Pitch));
            })
        );
    }

    [Theory]
    [MemberData(nameof(Projects))]
    public void LiveEngineDoesNotAllocate(string name)
    {
        // The project is both the rig and the show, with half the rig on the console: every layer runs every frame.
        var rig = Create(name);
        var free = rig.Fixtures.Where((_, i) => i % 2 == 0).Select(f => f.Id).ToArray();
        foreach (var track in rig.Tracks)
            track.FixtureIds.RemoveAll(free.Contains);
        foreach (var target in Enum.GetValues<LiveFaderTarget>())
        {
            var fader = new LiveFader { Target = target, Role = ChannelRole.Pan, Midi = new MidiBinding(MidiBindingKind.Control, 1, (int)target) };
            fader.FixtureIds.AddRange(free);
            rig.Live.Faders.Add(fader);
        }
        foreach (var entry in EffectCatalog.Entries)
        {
            var pad = new LivePad { Effect = EffectCatalog.Create(entry.Kind), Midi = new MidiBinding(MidiBindingKind.Note, 1, rig.Live.Pads.Count) };
            pad.FixtureIds.AddRange(free);
            rig.Live.Pads.Add(pad);
        }
        for (var f = 0; f < rig.Live.Faders.Count; f++)
            if (LivePatch.IsPadTarget(rig.Live.Faders[f].Target))
                rig.Live.Faders[f].PadId = rig.Live.Pads[f % rig.Live.Pads.Count].Id;

        var patch = LivePatch.Build(rig);
        var controls = new LiveControls();
        var cue = ShowCue.Load(Create(name), patch, loop: true);
        var now = Stopwatch.GetTimestamp();
        cue.Clock.Start(now, 0);
        var engine = new LiveEngine(controls) { Scene = new LiveScene(patch, cue) };
        var router = new MidiRouter(controls) { Patch = patch };
        var mailbox = new DmxFrameMailbox();
        var frame = new DmxFrame();
        var copy = new DmxFrame();
        long seen = 0;
        Assert.Equal(
            0,
            Allocated(i =>
            {
                router.Handle(MidiMessage.ControlChange(1, i % 8, i % 128));
                router.Handle(MidiMessage.NoteOn(1, i % EffectCatalog.Entries.Count, 100));
                router.TryGetLatest(ref seen, out _);
                now += Stopwatch.Frequency / 100 + i % 3 * Stopwatch.Frequency / 1000;
                engine.Render(now, frame);
                cue.Clock.TimeAt(now);
                mailbox.Publish(frame);
                mailbox.CopyLatestTo(copy);
            })
        );
    }
}
