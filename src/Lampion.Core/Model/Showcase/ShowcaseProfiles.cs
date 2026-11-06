using Lampion.Core.Model.Rig;

namespace Lampion.Core.Model.Showcase;

/// <summary>The showcase's own fixture types, next to the built-ins: a wash mover and an audience blinder.</summary>
internal static class ShowcaseProfiles
{
    /// <summary>12ch LED wash moving head with 16-bit pan/tilt and zoom.</summary>
    public const string WashMoverId = "showcase-wash-mover";

    /// <summary>1ch two-lamp tungsten blinder; its colour comes from the fixture's gel.</summary>
    public const string BlinderId = "showcase-blinder";

    /// <summary>Fresh instances of both profiles.</summary>
    public static IEnumerable<FixtureProfile> All()
    {
        yield return Make(
            WashMoverId,
            "LED Wash Mover",
            BeamKind.Wash,
            22,
            ("Pan", ChannelRole.Pan, 128),
            ("Pan fine", ChannelRole.PanFine, 0),
            ("Tilt", ChannelRole.Tilt, 128),
            ("Tilt fine", ChannelRole.TiltFine, 0),
            ("Speed", ChannelRole.PanTiltSpeed, 0),
            ("Dimmer", ChannelRole.Dimmer, 0),
            ("Strobe", ChannelRole.Strobe, 0),
            ("Red", ChannelRole.Red, 0),
            ("Green", ChannelRole.Green, 0),
            ("Blue", ChannelRole.Blue, 0),
            ("White", ChannelRole.White, 0),
            ("Zoom", ChannelRole.Zoom, 128)
        );
        yield return Make(BlinderId, "2-Lite Blinder", BeamKind.Blinder, 50, ("Dimmer", ChannelRole.Dimmer, 0));
    }

    private static FixtureProfile Make(string id, string model, BeamKind beam, double angle, params (string Name, ChannelRole Role, byte Default)[] channels)
    {
        var profile = new FixtureProfile
        {
            Id = id,
            Manufacturer = "Lampion",
            Model = model,
            Mode = $"{channels.Length} Channel",
            Beam = beam,
            BeamAngle = angle,
        };
        foreach (var (name, role, value) in channels)
            profile.Channels.Add(new FixtureChannel { Name = name, Role = role, Default = value });
        return profile;
    }
}
