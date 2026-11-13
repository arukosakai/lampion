using System.Diagnostics;
using Lampion.Core.Color;
using Lampion.Core.Engine;
using Lampion.Core.Live;
using Lampion.Core.Midi;
using Lampion.Core.Model;
using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Live;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Showcase;
using Lampion.Core.Model.Stage;
using Lampion.Core.Model.Timeline;
using Lampion.Core.Persistence;

namespace Lampion.Core.Tests;

/// <summary>Live mode must keep putting out light whatever goes wrong: broken effects, garbage MIDI, nonsense values, odd timing.</summary>
public class LiveRobustnessTests
{
    /// <summary>Red until <see cref="Broken"/>, then throws, like a bug in an effect would.</summary>
    private sealed class BreakableEffect : Effect
    {
        public bool Broken { get; set; }

        public override string DisplayName => "Breakable";

        public override string SwatchColor => "#FF0000";

        public override EffectSample Sample(in EffectContext ctx) => Broken ? throw new InvalidOperationException("boom") : new(new Rgb(1, 0, 0), 1);
    }

    private long _now = Stopwatch.Frequency;

    private static Project Rig()
    {
        var project = new Project();
        project.Profiles.AddRange(BuiltInProfiles.All().Select(p => p.Clone(newId: false)));
        project.Fixtures.Add(new Fixture { Id = "par1", ProfileId = BuiltInProfiles.RgbParId, Address = 1, Position = new Vec3(-1, 4, 0) });
        project.Fixtures.Add(new Fixture { Id = "par2", ProfileId = BuiltInProfiles.RgbParId, Address = 4, Position = new Vec3(1, 4, 0) });
        project.Fixtures.Add(new Fixture { Id = "refl1", ProfileId = BuiltInProfiles.DimmerId, Address = 10 });
        project.Fixtures.Add(new Fixture { Id = "refl2", ProfileId = BuiltInProfiles.DimmerId, Address = 11 });
        return project;
    }

    private byte[] Render(LiveEngine engine)
    {
        var frame = new DmxFrame();
        engine.Render(_now, frame);
        _now += Stopwatch.Frequency / 100;
        return frame.TryGetUniverse(1, out var channels) ? channels.ToArray() : new byte[DmxFrame.UniverseSize];
    }

    [Fact]
    public void ABrokenShowHoldsItsLastLookInsteadOfGoingDark()
    {
        var effect = new BreakableEffect();
        var show = Rig();
        var track = new Track();
        track.FixtureIds.AddRange(["par1", "par2"]);
        track.Clips.Add(new LightClip { Start = 0, Duration = 60, Effect = effect });
        show.Tracks.Add(track);
        var patch = LivePatch.Build(Rig());
        var cue = ShowCue.Load(show, patch, loop: false);
        cue.Clock.Start(_now, 0);
        var engine = new LiveEngine(new LiveControls()) { Scene = new LiveScene(patch, cue) };

        Render(engine);
        effect.Broken = true;
        var held = Render(engine);

        Assert.Equal([255, 0, 0, 255, 0, 0], held[..6]);
        Assert.Equal(1, engine.FaultCount);
        Assert.Contains("boom", engine.LastFault);
    }

    [Fact]
    public void ABrokenPadIsSwitchedOffAndTheRestCarriesOn()
    {
        var rig = Rig();
        var broken = new LivePad { Effect = new BreakableEffect { Broken = true } };
        broken.FixtureIds.Add("refl1");
        var fine = new LivePad { Effect = new SolidEffect { Color = "#FFFFFF" } };
        fine.FixtureIds.Add("refl2");
        rig.Live.Pads.AddRange([broken, fine]);
        var patch = LivePatch.BuildFrom(rig);
        var engine = new LiveEngine(new LiveControls()) { Scene = new LiveScene(patch, null) };
        engine.Controls.PressPad(0, PadMode.Toggle);
        engine.Controls.PressPad(1, PadMode.Toggle);

        var dmx = Render(engine);
        Render(engine);

        Assert.Equal(255, dmx[10]);
        Assert.False(engine.Controls.IsPadOn(0));
        Assert.True(engine.Controls.IsPadOn(1));
        Assert.Equal(1, engine.FaultCount);
    }

    [Fact]
    public void GarbageMidiNeverThrowsOrLeavesTheFaderRange()
    {
        var project = new Project();
        for (var channel = 1; channel <= 16; channel++)
        {
            project.Live.Faders.Add(new LiveFader { Midi = new MidiBinding(MidiBindingKind.Control, channel, channel) });
            project.Live.Pads.Add(new LivePad { Mode = channel % 2 == 0 ? PadMode.Flash : PadMode.Toggle, Midi = new MidiBinding(MidiBindingKind.Note, channel, channel) });
        }
        var router = new MidiRouter(new LiveControls()) { Patch = LivePatch.Build(project) };
        var random = new Random(1);

        for (var i = 0; i < 200_000; i++)
            router.Handle(new MidiMessage((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)));

        Assert.Null(router.Error);
        for (var f = 0; f < 16; f++)
            Assert.InRange(router.Controls.Fader(f), 0, 1);
    }

    [Theory]
    [InlineData(1e9)]
    [InlineData(double.PositiveInfinity)]
    public async Task HugeRatesDoNotHangSlotEffects(double rate)
    {
        // These loop over the slots around "now"; a slot index that saturated at int.MaxValue used to loop forever.
        Effect[] effects = [new SparkleEffect { Rate = rate }, new LightningEffect { Rate = rate }];
        var sampled = Task.Run(() =>
        {
            foreach (var effect in effects)
                foreach (var time in (double[])[0, 3, 1e9, 1e15])
                    effect.Sample(new EffectContext(time, 4, 1, 4));
        });

        Assert.True(await Task.WhenAny(sampled, Task.Delay(TimeSpan.FromSeconds(5))) == sampled, "a slot effect hung");
    }

    [Fact]
    public void TheClockIgnoresGarbageReports()
    {
        var clock = new ShowClock();
        clock.Start(0, 5);

        clock.Sync(Stopwatch.Frequency, double.NaN);
        clock.Sync(Stopwatch.Frequency, double.PositiveInfinity);
        clock.Start(0, double.NaN);
        clock.Hold(double.NegativeInfinity);

        Assert.Equal(6, clock.TimeAt(Stopwatch.Frequency), 9);
    }

    [Fact]
    public void SensibleSettingsNeverFault()
    {
        var (engine, fault) = Fuzz(seed: 3, insane: false);

        Assert.Null(fault);
        Assert.Equal(0, engine.FaultCount);
    }

    [Fact]
    public void NonsenseSettingsAndTimingNeverStopTheLights()
    {
        var (_, fault) = Fuzz(seed: 4, insane: true);

        Assert.Null(fault);
    }

    // Runs the engine for thousands of frames with every fader target, every effect on a pad (its parameters randomized),
    // random fader moves and presses, timestamps that jump and go backwards, show swaps, re-patches and clock reports.
    private (LiveEngine Engine, Exception? Escaped) Fuzz(int seed, bool insane)
    {
        var random = new Random(seed);
        double Value() => insane
            ? random.Next(6) switch { 0 => double.NaN, 1 => double.PositiveInfinity, 2 => double.NegativeInfinity, 3 => -1e9, 4 => 1e9, _ => random.NextDouble() * 20 - 10 }
            : random.NextDouble() * 4;

        var rig = ShowcaseProject.Create();
        var free = rig.Fixtures.Where((_, i) => i % 3 == 0).Select(f => f.Id).ToArray();
        foreach (var target in Enum.GetValues<LiveFaderTarget>())
        {
            var fader = new LiveFader { Target = target, Role = (ChannelRole)random.Next((int)ChannelRole.Generic + 1) };
            fader.FixtureIds.AddRange(free);
            rig.Live.Faders.Add(fader);
        }
        foreach (var entry in EffectCatalog.Entries)
        {
            var effect = EffectCatalog.Create(entry.Kind);
            foreach (var property in effect.GetType().GetProperties().Where(p => p.CanWrite))
                if (property.PropertyType == typeof(double))
                    property.SetValue(effect, Value());
                else if (property.PropertyType == typeof(int))
                    property.SetValue(effect, insane ? random.Next(int.MinValue, int.MaxValue) : random.Next(1, 8));
            var pad = new LivePad { Effect = effect, Blend = (BlendMode)random.Next(3), Mode = (PadMode)random.Next(2) };
            pad.FixtureIds.AddRange(free);
            rig.Live.Pads.Add(pad);
        }
        for (var f = 0; f < rig.Live.Faders.Count; f++)
            if (LivePatch.IsPadTarget(rig.Live.Faders[f].Target))
                rig.Live.Faders[f].PadId = rig.Live.Pads[random.Next(rig.Live.Pads.Count)].Id;

        var patch = LivePatch.BuildFrom(rig);
        var engine = new LiveEngine(new LiveControls());
        var cue = ShowCue.Load(ShowcaseProject.Create(), patch, loop: random.Next(2) == 0);
        cue.Clock.Start(_now, 0);
        engine.Scene = new LiveScene(patch, cue);
        var frame = new DmxFrame();
        try
        {
            for (var i = 0; i < 5000; i++)
            {
                _now += random.Next(20) switch
                {
                    0 => -Stopwatch.Frequency / 20,
                    1 => Stopwatch.Frequency * 3600,
                    _ => (long)(Stopwatch.Frequency * random.NextDouble() * 0.05),
                };
                engine.Controls.SetFader(random.Next(rig.Live.Faders.Count), insane ? Value() : random.NextDouble());
                if (random.Next(5) == 0)
                    engine.Controls.PressPad(random.Next(rig.Live.Pads.Count), (PadMode)random.Next(2));
                if (random.Next(7) == 0)
                    engine.Controls.ReleasePad(random.Next(rig.Live.Pads.Count), PadMode.Flash);
                if (random.Next(50) == 0)
                    cue.Clock.Sync(_now, insane ? Value() : random.NextDouble() * cue.Duration);
                switch (random.Next(400))
                {
                    case 0:
                        engine.Scene = new LiveScene(patch, null);
                        break;
                    case 1:
                        cue = ShowCue.Load(ProjectSerializer.Deserialize(ProjectSerializer.Serialize(ShowcaseProject.Create())), patch, loop: true);
                        cue.Clock.Start(_now, random.NextDouble() * 100);
                        engine.Scene = new LiveScene(patch, cue);
                        break;
                    case 2:
                        patch = LivePatch.BuildFrom(rig);
                        cue = cue.Retarget(patch);
                        engine.Scene = new LiveScene(patch, cue);
                        break;
                }
                engine.Render(_now, frame);
            }
            return (engine, null);
        }
        catch (Exception ex)
        {
            return (engine, ex);
        }
    }
}
