namespace Lampion.Core.Model.Stage;

/// <summary>The set and the look of the room around the rig.</summary>
public sealed class StageSettings
{
    /// <summary>Set pieces, in drawing order.</summary>
    public List<StageObject> Objects { get; } = [];

    /// <summary>How visible beams are in the air, 0..1.</summary>
    public double Haze { get; set; } = 0.6;

    /// <summary>House light level so the set is readable with every fixture off, 0..1.</summary>
    public double Ambient { get; set; } = 0.3;

    /// <summary>Lets the rig's combined output tint the room's ambient light, like real bounce off the deck.</summary>
    public bool LightBounce { get; set; }

    /// <summary>Snap step for placing things in the stage builder, in metres.</summary>
    public double GridStep { get; set; } = 0.25;

    /// <summary>The stage new projects start with: a deck, a back wall with an LED screen and two trusses.</summary>
    public static StageSettings CreateDefault() =>
        new()
        {
            Objects =
            {
                new StageObject { Name = "Deck", Kind = StageObjectKind.Platform, Position = new Vec3(0, 0.5, -1), Size = new Vec3(12, 1, 8), Color = "#2B2622" },
                new StageObject { Name = "Back wall", Kind = StageObjectKind.Wall, Position = new Vec3(0, 4, -5.1), Size = new Vec3(14, 8, 0.2), Color = "#1E1E24" },
                new StageObject { Name = "LED screen", Kind = StageObjectKind.Screen, Position = new Vec3(0, 3.6, -4.95), Size = new Vec3(6.4, 3.6, 0.08), Color = "#050505" },
                new StageObject { Name = "Front truss", Kind = StageObjectKind.Truss, Position = new Vec3(0, 6, 2), Size = new Vec3(13, 0.3, 0.3), Color = "#B8BCC4" },
                new StageObject { Name = "Back truss", Kind = StageObjectKind.Truss, Position = new Vec3(0, 6, -3.5), Size = new Vec3(13, 0.3, 0.3), Color = "#B8BCC4" },
            },
        };
}
