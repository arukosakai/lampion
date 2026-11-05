using Lampion.Core.Color;
using Lampion.Core.Engine;
using Lampion.Core.Model;

namespace Lampion.Core.Tests;

public class DmxMapperTests
{
    private static FixtureProfile Profile(string id) => BuiltInProfiles.All().First(p => p.Id == id);

    private static byte[] Map(string profileId, Rgb light, Action<FixtureState>? configure = null, bool extractWhite = true)
    {
        var state = new FixtureState { Light = light };
        configure?.Invoke(state);
        return DmxMapper.Map(new Fixture { ExtractWhite = extractWhite }, Profile(profileId), state);
    }

    [Fact]
    public void RgbParGetsColourDirectly()
    {
        Assert.Equal([255, 0, 128], Map(BuiltInProfiles.RgbParId, new Rgb(1, 0, 0.5)));
    }

    [Fact]
    public void RgbwParMovesTheSharedPartIntoWhite()
    {
        Assert.Equal([0, 0, 0, 255], Map(BuiltInProfiles.RgbwParId, Rgb.White));
        Assert.Equal([128, 0, 0, 128], Map(BuiltInProfiles.RgbwParId, new Rgb(1, 0.5, 0.5)));
    }

    [Fact]
    public void WhiteExtractionCanBeTurnedOff()
    {
        Assert.Equal([255, 255, 255, 0], Map(BuiltInProfiles.RgbwParId, Rgb.White, extractWhite: false));
    }

    [Fact]
    public void FixtureWithDimmerKeepsFullColourAndDimsWithTheDimmer()
    {
        // Dimmer, R, G, B
        Assert.Equal([128, 255, 0, 0], Map(BuiltInProfiles.DimRgbParId, new Rgb(0.5, 0, 0)));
        Assert.Equal([0, 0, 0, 0], Map(BuiltInProfiles.DimRgbParId, Rgb.Black));
    }

    [Fact]
    public void DimmerOnlyFixtureFollowsBrightness()
    {
        Assert.Equal([191], Map(BuiltInProfiles.DimmerId, new Rgb(0.2, 0.75, 0.1)));
    }

    [Fact]
    public void PanUsesTheFineChannelForSixteenBits()
    {
        var values = Map(BuiltInProfiles.MovingHeadId, Rgb.Black, s => s.SetRole(ChannelRole.Pan, 0.5));
        Assert.Equal(0x80, values[0]);
        Assert.Equal(0x00, values[1]);
        // Tilt untouched keeps the profile default.
        Assert.Equal(128, values[2]);
    }

    [Fact]
    public void RawChannelOverridesWin()
    {
        var values = Map(BuiltInProfiles.RgbParId, Rgb.White, s => s.SetRaw(1, 0.2));
        Assert.Equal([255, 51, 255], values);
    }

    [Fact]
    public void DecodeRoundTripsWhatMapWrote()
    {
        var profile = Profile(BuiltInProfiles.DimRgbParId);
        var fixture = new Fixture();
        var values = DmxMapper.Map(fixture, profile, new FixtureState { Light = new Rgb(0, 0.4, 0.4) });
        var emitted = DmxMapper.Decode(fixture, profile, values);
        Assert.Equal(0, emitted.Color.R, 2);
        Assert.Equal(0.4, emitted.Color.G, 2);
        Assert.Equal(0.4, emitted.Color.B, 2);
    }
}
