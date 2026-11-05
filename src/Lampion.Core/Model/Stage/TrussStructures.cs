namespace Lampion.Core.Model.Stage;

/// <summary>
/// Builds arches and cubes out of plain truss segments, standing on the floor. Legs are upright trusses at the
/// corners; spans sit on top of them, so outer width, depth and height are exactly what was asked for.
/// </summary>
public static class TrussStructures
{
    /// <summary>Default square truss section, metres (a common 30 cm box truss).</summary>
    public const double DefaultSection = 0.3;

    private const string Aluminium = "#B8BCC4";

    /// <summary>Builds <paramref name="shape"/> centred on <paramref name="centre"/> (its Y is ignored; legs stand on the floor).</summary>
    /// <param name="shape">Which structure.</param>
    /// <param name="name">Prefix for the pieces' names, e.g. "Arch" gives "Arch left leg".</param>
    /// <param name="centre">Floor point under the middle of the structure, metres.</param>
    /// <param name="width">Outer width along X before turning, metres; at least three sections.</param>
    /// <param name="depth">Outer depth along Z for a cube, metres; ignored for an arch.</param>
    /// <param name="height">Height to the top of the spans, metres; at least two sections.</param>
    /// <param name="yaw">Degrees the whole structure is turned around its centre.</param>
    /// <param name="section">Truss section, metres.</param>
    public static TrussStructure Build(TrussShape shape, string name, Vec3 centre, double width, double depth, double height, double yaw, double section = DefaultSection)
    {
        width = Math.Max(width, 3 * section);
        depth = Math.Max(depth, 3 * section);
        height = Math.Max(height, 2 * section);
        var frame = Basis.FromYawPitchRoll(yaw, 0, 0);
        var floor = centre with { Y = 0 };
        var pieces = new List<StageObject>();
        var spans = new List<StageObject>();
        var legX = width / 2 - section / 2;
        var spanY = height - section / 2;

        StageObject Piece(string part, Vec3 local, double length, double pieceYaw, double roll)
        {
            var piece = new StageObject
            {
                Name = $"{name} {part}",
                Kind = StageObjectKind.Truss,
                Position = StageMath.Round(floor + frame.ToWorld(local)),
                Size = new Vec3(Math.Round(length, 3), section, section),
                Yaw = Math.Round(yaw + pieceYaw, 2),
                Roll = roll,
                Color = Aluminium,
            };
            pieces.Add(piece);
            return piece;
        }

        void Leg(string part, double x, double z) => Piece(part, new Vec3(x, (height - section) / 2, z), height - section, 0, 90);

        if (shape == TrussShape.Arch)
        {
            Leg("left leg", -legX, 0);
            Leg("right leg", legX, 0);
            spans.Add(Piece("span", new Vec3(0, spanY, 0), width, 0, 0));
            return new TrussStructure(pieces, spans);
        }

        var legZ = depth / 2 - section / 2;
        Leg("front left leg", -legX, legZ);
        Leg("front right leg", legX, legZ);
        Leg("back left leg", -legX, -legZ);
        Leg("back right leg", legX, -legZ);
        spans.Add(Piece("front span", new Vec3(0, spanY, legZ), width, 0, 0));
        spans.Add(Piece("back span", new Vec3(0, spanY, -legZ), width, 0, 0));
        // Side spans fit between the front and back ones instead of overlapping them at the corners.
        spans.Add(Piece("left span", new Vec3(-legX, spanY, 0), depth - 2 * section, 90, 0));
        spans.Add(Piece("right span", new Vec3(legX, spanY, 0), depth - 2 * section, 90, 0));
        return new TrussStructure(pieces, spans);
    }
}
