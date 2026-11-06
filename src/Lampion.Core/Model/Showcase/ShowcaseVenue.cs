using Lampion.Core.Model.Stage;

namespace Lampion.Core.Model.Showcase;

/// <summary>
/// The showcase's set: a festival deck with a thrust and risers, a big LED wall flanked by angled side screens,
/// PA stacks, a ground-support truss cube with a mid truss, and two leaning pixel towers. Knows where fixtures hang.
/// </summary>
internal sealed class ShowcaseVenue
{
    /// <summary>Top of the deck, metres.</summary>
    public const double DeckTop = 1.2;

    private const string Aluminium = "#B8BCC4";

    private ShowcaseVenue(TrussStructure groundSupport, StageObject midTruss, StageObject towerLeft, StageObject towerRight)
    {
        GroundSupport = groundSupport;
        MidTruss = midTruss;
        TowerLeft = towerLeft;
        TowerRight = towerRight;
    }

    /// <summary>The 16.4 × 9.4 m truss cube over the deck, 8.5 m to the top of its spans.</summary>
    public TrussStructure GroundSupport { get; }

    /// <summary>Downstage span of the cube, over the audience edge of the deck.</summary>
    public StageObject FrontSpan => GroundSupport.Spans[0];

    /// <summary>Upstage span of the cube, in front of the LED wall.</summary>
    public StageObject BackSpan => GroundSupport.Spans[1];

    /// <summary>The cube's downstage-left leg (audience left).</summary>
    public StageObject FrontLeftLeg => GroundSupport.Pieces[0];

    /// <summary>The cube's downstage-right leg.</summary>
    public StageObject FrontRightLeg => GroundSupport.Pieces[1];

    /// <summary>A truss across the middle of the cube, between its side spans.</summary>
    public StageObject MidTruss { get; }

    /// <summary>Upright truss left of the LED wall, its top leaning out.</summary>
    public StageObject TowerLeft { get; }

    /// <summary>Upright truss right of the LED wall, its top leaning out.</summary>
    public StageObject TowerRight { get; }

    /// <summary>Puts the set into <paramref name="project"/>'s stage, replacing whatever was there.</summary>
    public static ShowcaseVenue Build(Project project)
    {
        var groundSupport = TrussStructures.Build(TrussShape.Cube, "Ground support", new Vec3(0, 0, -1), width: 16.4, depth: 9.4, height: 8.5, yaw: 0);
        var spanY = groundSupport.Spans[0].Position.Y;
        var midTruss = Truss("Mid truss", new Vec3(0, spanY, -1), 16.4 - 2 * TrussStructures.DefaultSection, roll: 0);
        // Roll turns the +X end up and over: past 90° the top leans to audience left, short of it to the right.
        var towerLeft = Truss("Pixel tower L", new Vec3(-5.6, DeckTop + 3, -5.85), 6, roll: 98);
        var towerRight = Truss("Pixel tower R", new Vec3(5.6, DeckTop + 3, -5.85), 6, roll: 82);

        project.Stage = new StageSettings
        {
            Haze = 0.8,
            Ambient = 0.12,
            LightBounce = true,
            Objects =
            {
                Solid("Deck", StageObjectKind.Platform, new Vec3(0, DeckTop / 2, -1), new Vec3(14, DeckTop, 9), "#1C1A19"),
                Solid("Thrust", StageObjectKind.Platform, new Vec3(0, DeckTop / 2, 5), new Vec3(3, DeckTop, 3), "#1C1A19"),
                Solid("Drum riser", StageObjectKind.Platform, new Vec3(0, DeckTop + 0.25, -3.2), new Vec3(4, 0.5, 2.6), "#2A2522"),
                Solid("Keys riser L", StageObjectKind.Platform, new Vec3(-4.6, DeckTop + 0.2, -3.6), new Vec3(2.4, 0.4, 1.8), "#2A2522"),
                Solid("Keys riser R", StageObjectKind.Platform, new Vec3(4.6, DeckTop + 0.2, -3.6), new Vec3(2.4, 0.4, 1.8), "#2A2522"),
                Solid("Back wall", StageObjectKind.Wall, new Vec3(0, 5, -6.6), new Vec3(22, 10, 0.2), "#121216"),
                Solid("LED wall", StageObjectKind.Screen, new Vec3(0, 5.2, -6.45), new Vec3(9, 5, 0.08), "#050507"),
                Solid("Side screen L", StageObjectKind.Screen, new Vec3(-9.6, 4.4, -5.9), new Vec3(2.4, 4.8, 0.08), "#050507", yaw: 18),
                Solid("Side screen R", StageObjectKind.Screen, new Vec3(9.6, 4.4, -5.9), new Vec3(2.4, 4.8, 0.08), "#050507", yaw: -18),
                Solid("PA stack L", StageObjectKind.Wall, new Vec3(-9.5, 1.7, 3), new Vec3(1.3, 3.4, 1.2), "#0D0D0F"),
                Solid("PA stack R", StageObjectKind.Wall, new Vec3(9.5, 1.7, 3), new Vec3(1.3, 3.4, 1.2), "#0D0D0F"),
                midTruss,
                towerLeft,
                towerRight,
            },
        };
        project.Stage.Objects.AddRange(groundSupport.Pieces);
        return new ShowcaseVenue(groundSupport, midTruss, towerLeft, towerRight);
    }

    private static StageObject Solid(string name, StageObjectKind kind, Vec3 position, Vec3 size, string color, double yaw = 0) =>
        new()
        {
            Name = name,
            Kind = kind,
            Position = position,
            Size = size,
            Yaw = yaw,
            Color = color,
        };

    private static StageObject Truss(string name, Vec3 position, double length, double roll) =>
        new()
        {
            Name = name,
            Kind = StageObjectKind.Truss,
            Position = position,
            Size = new Vec3(length, TrussStructures.DefaultSection, TrussStructures.DefaultSection),
            Roll = roll,
            Color = Aluminium,
        };
}
