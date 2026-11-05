namespace Lampion.Core.Model;

/// <summary>Starter fixture profiles. Ids are stable so demo projects and tests can refer to them.</summary>
public static class BuiltInProfiles
{
    public const string RgbParId = "builtin-rgb-par";
    public const string RgbwParId = "builtin-rgbw-par";
    public const string DimRgbParId = "builtin-drgb-par";
    public const string HexParId = "builtin-hex-par";
    public const string MovingHeadId = "builtin-moving-head";
    public const string LedBarId = "builtin-led-bar";
    public const string StrobeId = "builtin-strobe";
    public const string DimmerId = "builtin-dimmer";

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

    private static FixtureProfile Make(string id, string model, BeamKind beam, double angle, params (string Name, ChannelRole Role, byte Default)[] channels) =>
        new()
        {
            Id = id,
            Manufacturer = "Generic",
            Model = model,
            Mode = $"{channels.Length} Channel",
            Beam = beam,
            BeamAngle = angle,
            Channels = channels.Select(c => new FixtureChannel { Name = c.Name, Role = c.Role, Default = c.Default }).ToList(),
        };
}
