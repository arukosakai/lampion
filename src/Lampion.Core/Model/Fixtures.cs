using System.Text.Json.Serialization;

namespace Lampion.Core.Model;

/// <summary>What a DMX channel does. Lampion fills colour roles itself; the rest are driven by automation.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ChannelRole>))]
public enum ChannelRole
{
    Dimmer,
    Red,
    Green,
    Blue,
    White,
    Amber,
    UV,
    Strobe,
    Pan,
    PanFine,
    Tilt,
    TiltFine,
    PanTiltSpeed,
    Zoom,
    ColorWheel,
    Gobo,
    Effect,
    Generic,
}

[JsonConverter(typeof(JsonStringEnumConverter<BeamKind>))]
public enum BeamKind
{
    Par,
    Wash,
    Spot,
    Bar,
    Strobe,
    Blinder,
}

public sealed class FixtureChannel
{
    public string Name { get; set; } = "Channel";
    public ChannelRole Role { get; set; } = ChannelRole.Generic;

    /// <summary>Value written when nothing drives the channel (e.g. a shutter that must sit "open").</summary>
    public byte Default { get; set; }

    public FixtureChannel Clone() => new() { Name = Name, Role = Role, Default = Default };
}

/// <summary>A fixture type: its channel layout and how it looks in the preview.</summary>
public sealed class FixtureProfile
{
    public string Id { get; set; } = Ids.New();
    public string Manufacturer { get; set; } = "Generic";
    public string Model { get; set; } = "Fixture";
    public string Mode { get; set; } = "Default";
    public BeamKind Beam { get; set; } = BeamKind.Par;
    public double BeamAngle { get; set; } = 25;
    public double PanRange { get; set; } = 540;
    public double TiltRange { get; set; } = 270;
    public List<FixtureChannel> Channels { get; set; } = [];

    [JsonIgnore]
    public string DisplayName => $"{Manufacturer} {Model} ({Channels.Count}ch)";

    /// <summary>0-based offset of the first channel with this role, or -1.</summary>
    public int IndexOf(ChannelRole role) => Channels.FindIndex(c => c.Role == role);

    public bool Has(ChannelRole role) => IndexOf(role) >= 0;

    public bool HasColor => Has(ChannelRole.Red) || Has(ChannelRole.Green) || Has(ChannelRole.Blue);

    public bool HasPanTilt => Has(ChannelRole.Pan) || Has(ChannelRole.Tilt);

    public FixtureProfile Clone(bool newId = true) =>
        new()
        {
            Id = newId ? Ids.New() : Id,
            Manufacturer = Manufacturer,
            Model = Model,
            Mode = Mode,
            Beam = Beam,
            BeamAngle = BeamAngle,
            PanRange = PanRange,
            TiltRange = TiltRange,
            Channels = Channels.Select(c => c.Clone()).ToList(),
        };
}

public record struct Vec3(double X, double Y, double Z)
{
    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator *(Vec3 a, double k) => new(a.X * k, a.Y * k, a.Z * k);

    public readonly double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

    public readonly Vec3 Normalized()
    {
        var l = Length;
        return l < 1e-12 ? new Vec3(0, -1, 0) : this * (1 / l);
    }

    public static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    public static Vec3 Cross(Vec3 a, Vec3 b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
}

/// <summary>One patched fixture: a profile at a DMX address, placed on the stage.</summary>
public sealed class Fixture
{
    public string Id { get; set; } = Ids.New();
    public string Name { get; set; } = "Fixture";
    public string ProfileId { get; set; } = "";

    /// <summary>1-based, like QLC+'s UI and most consoles.</summary>
    public int Universe { get; set; } = 1;

    /// <summary>1-based start address within the universe.</summary>
    public int Address { get; set; } = 1;

    /// <summary>Metres. X to the audience's right, Y up, Z toward the audience.</summary>
    public Vec3 Position { get; set; } = new(0, 4, 0);

    /// <summary>Degrees around Y; 0 aims at the audience.</summary>
    public double Yaw { get; set; }

    /// <summary>Degrees; 0 is horizontal, -90 straight down.</summary>
    public double Pitch { get; set; } = -60;

    /// <summary>Tint for fixtures with no colour channels (a dimmer behind a gel).</summary>
    public string GelColor { get; set; } = "#FFE6B0";

    /// <summary>When the profile has a white channel, move the common part of R/G/B into it.</summary>
    public bool ExtractWhite { get; set; } = true;

    public Fixture Clone() =>
        new()
        {
            Id = Ids.New(),
            Name = Name,
            ProfileId = ProfileId,
            Universe = Universe,
            Address = Address,
            Position = Position,
            Yaw = Yaw,
            Pitch = Pitch,
            GelColor = GelColor,
            ExtractWhite = ExtractWhite,
        };
}

public sealed class FixtureGroup
{
    public string Id { get; set; } = Ids.New();
    public string Name { get; set; } = "Group";
    public string Color { get; set; } = "#3A8EE6";
    public List<string> FixtureIds { get; set; } = [];
}

public static class Ids
{
    public static string New() => Guid.NewGuid().ToString("N")[..12];
}
