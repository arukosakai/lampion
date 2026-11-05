using System.Numerics;
using Lampion.App.Rendering;
using Lampion.Core.Color;
using Lampion.Core.Model.Stage;

namespace Lampion.App.Controls.Preview;

/// <summary>
/// The little set the fixture preview stands in: a back wall, plus a truss to hang from unless the fixture
/// stands on the floor. Built once and shared; nothing may modify these objects.
/// </summary>
internal static class PreviewSet
{
    /// <summary>Where hung fixtures sit, metres.</summary>
    public static readonly Vector3 HangPoint = new(0, 2.8f, 0);

    /// <summary>Where floor fixtures (bars) sit, metres.</summary>
    public static readonly Vector3 FloorPoint = new(0, 0.08f, 0);

    private static readonly StageObject Wall = new() { Kind = StageObjectKind.Wall, Position = new Vec3(0, 4, -4.5), Size = new Vec3(14, 8, 0.15), Color = "#26262C" };
    private static readonly StageObject Truss = new() { Kind = StageObjectKind.Truss, Position = new Vec3(0, HangPoint.Y + 0.42, 0), Size = new Vec3(3, 0.3, 0.3), Color = "#B8BCC4" };
    private static readonly StageObject[] FloorObjects = [Wall];
    private static readonly StageObject[] HungObjects = [Wall, Truss];
    private static readonly ObjectVisual[] FloorVisuals = [Visual(Wall)];
    private static readonly ObjectVisual[] HungVisuals = [Visual(Wall), Visual(Truss)];

    /// <summary>The set pieces, for raycasting beams and aim clicks against.</summary>
    /// <param name="onFloor">True for the floor set (no truss).</param>
    public static IReadOnlyList<StageObject> Objects(bool onFloor) => onFloor ? FloorObjects : HungObjects;

    /// <summary>The same pieces ready to draw.</summary>
    /// <param name="onFloor">True for the floor set (no truss).</param>
    public static ReadOnlySpan<ObjectVisual> Visuals(bool onFloor) => onFloor ? FloorVisuals : HungVisuals;

    /// <summary>A model vector as a render vector.</summary>
    public static Vector3 ToVector(Vec3 v) => new((float)v.X, (float)v.Y, (float)v.Z);

    private static ObjectVisual Visual(StageObject o) => new(o.Id, o.Kind, ToVector(o.Position), ToVector(o.Size), Quaternion.Identity, Linear(o.Color), false);

    private static Vector3 Linear(string hex)
    {
        var c = Rgb.FromHex(hex);
        return new Vector3(MathF.Pow((float)c.R, 2.2f), MathF.Pow((float)c.G, 2.2f), MathF.Pow((float)c.B, 2.2f));
    }
}
