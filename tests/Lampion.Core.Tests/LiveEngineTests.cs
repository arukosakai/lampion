using System.Diagnostics;
using Lampion.Core.Engine;
using Lampion.Core.Live;
using Lampion.Core.Model;
using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Live;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Stage;
using Lampion.Core.Model.Timeline;
using Lampion.Core.Persistence;

namespace Lampion.Core.Tests;

public class LiveEngineTests
{
    // Two RGB pars at 1 and 4 that the show drives, two 1-channel reflectors at 10 and 11 that it doesn't.
    private static Project Rig()
    {
        var project = new Project();
        project.Profiles.AddRange(BuiltInProfiles.All().Select(p => p.Clone(newId: false)));
        project.Fixtures.Add(new Fixture { Id = "par1", ProfileId = BuiltInProfiles.RgbParId, Address = 1, Position = new Vec3(-1, 4, 0) });
        project.Fixtures.Add(new Fixture { Id = "par2", ProfileId = BuiltInProfiles.RgbParId, Address = 4, Position = new Vec3(1, 4, 0) });
        project.Fixtures.Add(new Fixture { Id = "refl1", ProfileId = BuiltInProfiles.DimmerId, Address = 10, Position = new Vec3(-2, 2, 0) });
        project.Fixtures.Add(new Fixture { Id = "refl2", ProfileId = BuiltInProfiles.DimmerId, Address = 11, Position = new Vec3(2, 2, 0) });
        return project;
    }

    private static Project RedShowOnPars(double duration = 10)
    {
        var show = ProjectSerializer.Deserialize(ProjectSerializer.Serialize(Rig()));
        show.Duration = duration;
        var track = new Track();
        track.FixtureIds.AddRange(["par1", "par2"]);
        track.Clips.Add(new LightClip { Start = 0, Duration = duration, Effect = new SolidEffect { Color = "#FF0000" } });
        show.Tracks.Add(track);
        return show;
    }

    private static LiveFader Fader(LiveFaderTarget target, params string[] fixtures)
    {
        var fader = new LiveFader { Target = target };
        fader.FixtureIds.AddRange(fixtures);
        return fader;
    }

    private static LivePad Pad(Effect effect, PadMode mode = PadMode.Toggle, params string[] fixtures)
    {
        var pad = new LivePad { Effect = effect, Mode = mode };
        pad.FixtureIds.AddRange(fixtures);
        return pad;
    }

    // Engine time in Stopwatch ticks; each Render draws the frame at _now, then moves _now on by the given seconds.
    private long _now = Stopwatch.Frequency;

    private byte[] Render(LiveEngine engine, double seconds = 0.01)
    {
        var frame = new DmxFrame();
        engine.Render(_now, frame);
        _now += (long)(seconds * Stopwatch.Frequency);
        return frame.TryGetUniverse(1, out var channels) ? channels.ToArray() : new byte[DmxFrame.UniverseSize];
    }

    private (LiveEngine Engine, LivePatch Patch) Start(Project rig, Project? show = null, bool loop = false)
    {
        var patch = LivePatch.Build(rig);
        var engine = new LiveEngine(new LiveControls());
        var cue = show is null ? null : ShowCue.Load(show, patch, loop);
        cue?.Clock.Start(_now, 0);
        engine.Scene = new LiveScene(patch, cue);
        return (engine, patch);
    }

    [Fact]
    public void FaderDimsTheFixturesTheShowLeavesFree()
    {
        var rig = Rig();
        rig.Live.Faders.Add(Fader(LiveFaderTarget.Intensity, "par1", "par2", "refl1", "refl2"));
        var (engine, _) = Start(rig, RedShowOnPars());

        engine.Controls.SetFader(0, 0.5);
        var dmx = Render(engine);

        Assert.Equal(128, dmx[9]);
        Assert.Equal(128, dmx[10]);
        Assert.Equal([255, 0, 0, 255, 0, 0], dmx[..6]);
    }

    [Fact]
    public void ShowStillOwnsItsFixturesWithTheFaderDown()
    {
        var rig = Rig();
        rig.Live.Faders.Add(Fader(LiveFaderTarget.Intensity, "par1", "par2"));
        var (engine, _) = Start(rig, RedShowOnPars());

        engine.Controls.SetFader(0, 0);

        Assert.Equal([255, 0, 0, 255, 0, 0], Render(engine)[..6]);
    }

    [Fact]
    public void CueOwnsOnlyTheFixturesItsTracksTarget()
    {
        var patch = LivePatch.Build(Rig());
        var cue = ShowCue.Load(RedShowOnPars(), patch, loop: false);

        Assert.Equal(2, cue.OwnedCount);
        Assert.True(cue.Owns(0));
        Assert.True(cue.Owns(1));
        Assert.False(cue.Owns(2));
        Assert.False(cue.Owns(3));
    }

    [Fact]
    public void ShowFixturesWithAnotherIdAreMatchedByAddress()
    {
        var show = RedShowOnPars();
        foreach (var fixture in show.Fixtures)
            fixture.Id = "other-" + fixture.Id;
        show.Tracks[0].FixtureIds.Clear();
        show.Tracks[0].FixtureIds.AddRange(["other-par1", "other-par2"]);
        var (engine, _) = Start(Rig(), show);

        Assert.Equal([255, 0, 0, 255, 0, 0], Render(engine)[..6]);
    }

    [Fact]
    public void FreedFixturesGoBackToTheConsoleWhenTheShowEnds()
    {
        var rig = Rig();
        rig.Live.Faders.Add(Fader(LiveFaderTarget.Intensity, "par1"));
        var (engine, _) = Start(rig, RedShowOnPars(duration: 1));
        engine.Controls.SetFader(0, 1);

        Render(engine, 2);
        var dmx = Render(engine);

        Assert.True(engine.ShowEnded);
        Assert.Equal([255, 255, 255], dmx[..3]);
    }

    [Fact]
    public void LoopingShowStartsOver()
    {
        var (engine, _) = Start(Rig(), RedShowOnPars(duration: 1), loop: true);

        Render(engine, 1.5);
        Render(engine);

        Assert.False(engine.ShowEnded);
        Assert.Equal(0.5, engine.ShowTime, 3);
    }

    [Fact]
    public void ShowWaitsAtTheStartUntilItsMusicPlays()
    {
        var patch = LivePatch.Build(Rig());
        var cue = ShowCue.Load(RedShowOnPars(), patch, loop: false);
        cue.Clock.FollowsMedia = true;
        var engine = new LiveEngine(new LiveControls()) { Scene = new LiveScene(patch, cue) };

        Render(engine, 2);
        Render(engine);

        Assert.Equal(0, engine.ShowTime);
    }

    [Fact]
    public void LoopWithMusicHoldsTheEndUntilTheMusicRestarts()
    {
        var (engine, _) = Start(Rig(), RedShowOnPars(duration: 1), loop: true);
        engine.Scene!.Cue!.Clock.FollowsMedia = true;

        Render(engine, 1.5);
        Render(engine);

        Assert.False(engine.ShowEnded);
        Assert.True(engine.ShowTime is > 0.99 and < 1);
    }

    [Fact]
    public void RetargetedCueKeepsItsPlace()
    {
        var rig = Rig();
        var (engine, patch) = Start(rig, RedShowOnPars());
        Render(engine, 3);
        Render(engine);

        var rebuilt = LivePatch.Build(rig);
        engine.Scene = new LiveScene(rebuilt, engine.Scene!.Cue!.Retarget(rebuilt));
        Render(engine);

        Assert.Equal(3.01, engine.ShowTime, 3);
    }

    [Fact]
    public void PadTogglesItsEffectOnFreeFixturesOnly()
    {
        var rig = Rig();
        rig.Live.Pads.Add(Pad(new SolidEffect { Color = "#0000FF" }, PadMode.Toggle, "par1", "par2", "refl1", "refl2"));
        var (engine, _) = Start(rig, RedShowOnPars());

        engine.Controls.PressPad(0, PadMode.Toggle);
        var on = Render(engine);
        engine.Controls.PressPad(0, PadMode.Toggle);
        var off = Render(engine);

        Assert.Equal([255, 0, 0, 255, 0, 0], on[..6]);
        Assert.Equal(255, on[9]);
        Assert.Equal(0, off[9]);
    }

    [Fact]
    public void FlashPadRunsOnlyWhileHeld()
    {
        var rig = Rig();
        rig.Live.Pads.Add(Pad(new SolidEffect { Color = "#FFFFFF" }, PadMode.Flash, "refl1"));
        var (engine, _) = Start(rig);

        engine.Controls.PressPad(0, PadMode.Flash);
        engine.Controls.PressPad(0, PadMode.Flash);
        var held = Render(engine);
        engine.Controls.ReleasePad(0, PadMode.Flash);

        Assert.Equal(255, held[9]);
        Assert.Equal(0, Render(engine)[9]);
    }

    [Fact]
    public void PadIntensityFaderScalesThePad()
    {
        var rig = Rig();
        var pad = Pad(new SolidEffect { Color = "#FFFFFF" }, PadMode.Toggle, "refl1");
        rig.Live.Pads.Add(pad);
        rig.Live.Faders.Add(new LiveFader { Target = LiveFaderTarget.PadIntensity, PadId = pad.Id });
        var (engine, _) = Start(rig);

        engine.Controls.PressPad(0, PadMode.Toggle);
        engine.Controls.SetFader(0, 0.25);

        Assert.Equal(64, Render(engine)[9]);
    }

    [Fact]
    public void PadSpeedFaderAtTheTopRunsTheEffectFourTimesAsFast()
    {
        Project RigWithRainbow(out LivePad pad)
        {
            var rig = Rig();
            pad = Pad(new RainbowEffect { Speed = 0.1 }, PadMode.Toggle, "par1");
            rig.Live.Pads.Add(pad);
            rig.Live.Faders.Add(new LiveFader { Target = LiveFaderTarget.PadSpeed, PadId = pad.Id });
            return rig;
        }

        var (fast, _) = Start(RigWithRainbow(out _));
        fast.Controls.PressPad(0, PadMode.Toggle);
        fast.Controls.SetFader(0, 1);
        Render(fast, 0.5);
        var fastDmx = Render(fast, 0.5);

        var (normal, _) = Start(RigWithRainbow(out _));
        normal.Controls.PressPad(0, PadMode.Toggle);
        normal.Controls.SetFader(0, 0.5);
        Render(normal, 2);
        var normalDmx = Render(normal, 2);

        Assert.Equal(normalDmx[..3], fastDmx[..3]);
    }

    [Fact]
    public void HueFaderColoursAnIntensityFader()
    {
        var rig = Rig();
        rig.Live.Faders.Add(Fader(LiveFaderTarget.Intensity, "par1"));
        rig.Live.Faders.Add(Fader(LiveFaderTarget.Hue, "par1"));
        var (engine, _) = Start(rig);

        engine.Controls.SetFader(0, 1);
        engine.Controls.SetFader(1, 1 / 3.0);

        Assert.Equal([0, 255, 0], Render(engine)[..3]);
    }

    [Fact]
    public void LiveSetSurvivesSaveAndLoad()
    {
        var rig = Rig();
        rig.Live.Shows.Add(new LiveShow { Name = "Opener", Path = "opener.lampion", Loop = true });
        var strobe = Fader(LiveFaderTarget.Role, "refl1");
        strobe.Role = ChannelRole.Strobe;
        strobe.Midi = new MidiBinding(MidiBindingKind.Control, 1, 7);
        rig.Live.Faders.Add(strobe);
        rig.Live.Pads.Add(Pad(new RainbowEffect { Speed = 2 }, PadMode.Flash, "par1"));

        var loaded = ProjectSerializer.Deserialize(ProjectSerializer.Serialize(rig));

        Assert.Equal("opener.lampion", loaded.Live.Shows[0].Path);
        Assert.True(loaded.Live.Shows[0].Loop);
        Assert.Equal(ChannelRole.Strobe, loaded.Live.Faders[0].Role);
        Assert.Equal(new MidiBinding(MidiBindingKind.Control, 1, 7), loaded.Live.Faders[0].Midi);
        Assert.Equal(2, Assert.IsType<RainbowEffect>(loaded.Live.Pads[0].Effect).Speed);
        Assert.Equal(PadMode.Flash, loaded.Live.Pads[0].Mode);
    }

    [Fact]
    public void ProjectsFromBeforeLiveModeLoadWithAnEmptySet()
    {
        var loaded = ProjectSerializer.Deserialize("""{"formatVersion":1,"name":"Old show"}""");

        Assert.Empty(loaded.Live.Shows);
        Assert.Empty(loaded.Live.Faders);
        Assert.Empty(loaded.Live.Pads);
    }
}
