using Lampion.Core.Model.Stage;

namespace Lampion.Core.Model.Rig;

/// <summary>One patched fixture: a profile at a DMX address, placed on the stage. Edited in place by the inspector.</summary>
public sealed class Fixture
{
    /// <summary>Stable id tracks and groups refer to.</summary>
    public string Id { get; set; } = Ids.New();

    /// <summary>Name shown in the UI and the QLC+ export.</summary>
    public string Name { get; set; } = "Fixture";

    /// <summary>The <see cref="FixtureProfile.Id"/> of its type; a fixture whose profile is missing outputs nothing.</summary>
    public string ProfileId { get; set; } = "";

    /// <summary>1-based, like QLC+'s UI and most consoles.</summary>
    public int Universe { get; set; } = 1;

    /// <summary>1-based start address within the universe (1..512).</summary>
    public int Address { get; set; } = 1;

    /// <summary>Metres. X to the audience's right, Y up, Z toward the audience.</summary>
    public Vec3 Position { get; set; } = new(0, 4, 0);

    /// <summary>Degrees around Y; 0 aims at the audience.</summary>
    public double Yaw { get; set; }

    /// <summary>Degrees; 0 is horizontal, -90 straight down.</summary>
    public double Pitch { get; set; } = -60;

    /// <summary>Tint as <c>#RRGGBB</c> for fixtures with no colour channels (a dimmer behind a gel).</summary>
    public string GelColor { get; set; } = "#FFE6B0";

    /// <summary>When the profile has a white channel, move the common part of R/G/B into it.</summary>
    public bool ExtractWhite { get; set; } = true;

    /// <summary>A copy with a new id at the same address; the caller re-patches it.</summary>
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
