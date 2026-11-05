using Lampion.Core.Color;
using Lampion.Core.Engine;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Timeline;

namespace Lampion.Core.Tests;

public class FixtureStateTests
{
    [Fact]
    public void GenericIsTheLastChannelRole() =>
        // The per-role tables are sized from it.
        Assert.Equal(ChannelRole.Generic, Enum.GetValues<ChannelRole>().Max());

    [Fact]
    public void RolesMapInTheOrderTheyWereFirstSet()
    {
        // A coarse pan also writes the fine byte, so whichever role is written last decides the fine channel.
        var profile = BuiltInProfiles.All().First(p => p.Id == BuiltInProfiles.MovingHeadId);
        var fineFirst = new FixtureState();
        fineFirst.SetRole(ChannelRole.PanFine, 1);
        fineFirst.SetRole(ChannelRole.Pan, 0.5);
        var panFirst = new FixtureState();
        panFirst.SetRole(ChannelRole.Pan, 0.5);
        panFirst.SetRole(ChannelRole.PanFine, 1);

        var values = new byte[profile.Channels.Count];
        DmxMapper.Map(new Fixture(), profile, in fineFirst, values);
        Assert.Equal(0x00, values[1]);
        DmxMapper.Map(new Fixture(), profile, in panFirst, values);
        Assert.Equal(0xFF, values[1]);
    }

    [Fact]
    public void BlendLerpsSharedRolesAndTakesNewOnesWhole()
    {
        var below = new FixtureState { Light = Rgb.Black };
        below.SetRole(ChannelRole.Pan, 0.2);
        var top = new FixtureState { Light = Rgb.White };
        top.SetRole(ChannelRole.Pan, 0.6);
        top.SetRole(ChannelRole.Tilt, 0.9);
        top.SetRaw(3, 0.4);

        below.Blend(in top, 0.5, BlendMode.Normal);

        Assert.Equal(new Rgb(0.5, 0.5, 0.5), below.Light);
        Assert.True(below.TryGetRole(ChannelRole.Pan, out var pan));
        Assert.Equal(0.4, pan, 9);
        Assert.True(below.TryGetRole(ChannelRole.Tilt, out var tilt));
        Assert.Equal(0.9, tilt);
        Assert.True(below.TryGetRaw(3, out var raw));
        Assert.Equal(0.4, raw);
    }

    [Fact]
    public void RawOverridesAreCappedButExistingOnesStillUpdate()
    {
        var state = new FixtureState();
        for (var i = 0; i < FixtureState.MaxRawOverrides; i++)
            Assert.True(state.SetRaw(i, 0.1));
        Assert.False(state.SetRaw(99, 1));
        Assert.False(state.TryGetRaw(99, out _));
        Assert.True(state.SetRaw(0, 0.9));
        Assert.True(state.TryGetRaw(0, out var value));
        Assert.Equal(0.9, value);
    }

    [Fact]
    public void UndefinedRolesAreIgnored()
    {
        var state = new FixtureState();
        state.SetRole((ChannelRole)99, 1);
        Assert.False(state.TryGetRole((ChannelRole)99, out _));
    }
}
