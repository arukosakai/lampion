namespace Lampion.Core.Model.Rig;

/// <summary>Starter fixture profiles. Ids are stable so demo projects and tests can refer to them.</summary>
public static class BuiltInProfiles
{
    /// <summary>3ch R, G, B.</summary>
    public const string RgbParId = "builtin-rgb-par";

    /// <summary>4ch R, G, B, W.</summary>
    public const string RgbwParId = "builtin-rgbw-par";

    /// <summary>4ch Dimmer, R, G, B.</summary>
    public const string DimRgbParId = "builtin-drgb-par";

    /// <summary>8ch Dimmer, R, G, B, W, Amber, UV, Strobe.</summary>
    public const string HexParId = "builtin-hex-par";

    /// <summary>12ch moving head spot with 16-bit pan/tilt.</summary>
    public const string MovingHeadId = "builtin-moving-head";

    /// <summary>3ch RGB batten.</summary>
    public const string LedBarId = "builtin-led-bar";

    /// <summary>2ch Dimmer, Rate.</summary>
    public const string StrobeId = "builtin-strobe";

    /// <summary>1ch dimmer.</summary>
    public const string DimmerId = "builtin-dimmer";

    /// <summary>Fresh instances of every built-in profile; callers own them and may edit them.</summary>
    public static IReadOnlyList<FixtureProfile> All() =>
    [
        Make(RgbParId, "RGB Par", BeamKind.Par, 25, ("Red", ChannelRole.Red, 0), ("Green", ChannelRole.Green, 0), ("Blue", ChannelRole.Blue, 0)),
        Make(RgbwParId, "RGBW Par", BeamKind.Par, 25, ("Red", ChannelRole.Red, 0), ("Green", ChannelRole.Green, 0), ("Blue", ChannelRole.Blue, 0), ("White", ChannelRole.White, 0)),
        Make(DimRgbParId, "Dimmer RGB Par", BeamKind.Par, 25, ("Dimmer", ChannelRole.Dimmer, 0), ("Red", ChannelRole.Red, 0), ("Green", ChannelRole.Green, 0), ("Blue", ChannelRole.Blue, 0)),
        Make(
            HexParId,
            "RGBWA+UV Par",
            BeamKind.Wash,
            30,
            ("Dimmer", ChannelRole.Dimmer, 0),
            ("Red", ChannelRole.Red, 0),
            ("Green", ChannelRole.Green, 0),
            ("Blue", ChannelRole.Blue, 0),
            ("White", ChannelRole.White, 0),
            ("Amber", ChannelRole.Amber, 0),
            ("UV", ChannelRole.UV, 0),
            ("Strobe", ChannelRole.Strobe, 0)
        ),
        Make(
            MovingHeadId,
            "Moving Head Spot",
            BeamKind.Spot,
            14,
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
            ("Gobo", ChannelRole.Gobo, 0)
        ),
        Make(LedBarId, "LED Bar", BeamKind.Bar, 60, ("Red", ChannelRole.Red, 0), ("Green", ChannelRole.Green, 0), ("Blue", ChannelRole.Blue, 0)),
        Make(StrobeId, "Strobe", BeamKind.Strobe, 90, ("Dimmer", ChannelRole.Dimmer, 0), ("Rate", ChannelRole.Strobe, 0)),
        Make(DimmerId, "Dimmer (1ch)", BeamKind.Par, 30, ("Dimmer", ChannelRole.Dimmer, 0)),
    ];

    private static FixtureProfile Make(string id, string model, BeamKind beam, double angle, params (string Name, ChannelRole Role, byte Default)[] channels)
    {
        var profile = new FixtureProfile
        {
            Id = id,
            Manufacturer = "Generic",
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
